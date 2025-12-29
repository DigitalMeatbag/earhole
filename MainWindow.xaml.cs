using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NAudio.Wave;
using MathNet.Numerics.IntegralTransforms;
using System.Numerics;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using earhole.Modes;
using SkiaSharp;

namespace earhole;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    static int NextPowerOfTwo(int n)
    {
        int p = 1;
        while (p < n) p <<= 1;
        return p;
    }

    private const int SPECTRUM_RESOLUTION = 1024; // Number of frequency bins for spectrum analysis (higher = smoother but more CPU)
    private float[] leftSpectrum = new float[SPECTRUM_RESOLUTION]; // FFT bins for left channel
    private float[] rightSpectrum = new float[SPECTRUM_RESOLUTION]; // FFT bins for right channel
    private object spectrumLock = new object();
    private WasapiLoopbackCapture capture;
    private Thread captureThread;
    private System.Timers.Timer timer;
    private System.Timers.Timer shuffleTimer;
    private volatile bool running = true;
    private volatile bool renderPending = false; // Prevent multiple render requests
    private bool isFullscreen = false;
    private bool audioDetected = false;
    private Storyboard fadeStoryboard;
    private bool isClosing = false;
    private IVisualizerMode currentMode;
    private List<IVisualizerMode> availableModes;
    private ShuffleMode shuffleMode;
    private bool isShuffleActive = true;
    private bool isModeMenuVisible = false;
    private MediaSessionManager? mediaSessionManager;
    private bool isTrackInfoPersistent = false;
    private DispatcherTimer? trackInfoFadeTimer;
    private DispatcherTimer? modeInfoFadeTimer;
    
    // FPS tracking
    private bool showFps = false;
    private DateTime lastFpsUpdate = DateTime.Now;
    private int frameCount = 0;
    private double currentFps = 0;

    // Beat detection
    private bool isBeat = false;
    private float averageEnergy = 0f;
    private float energyVariance = 0f;
    private float lastEnergy = 0f;
    private int beatCooldown = 0;
    private const float BEAT_THRESHOLD = 1.4f; // Threshold multiplier for beat detection
    private const float ENERGY_DECAY = 0.95f; // Slower decay to maintain sensitivity during loud passages
    private const float VARIANCE_DECAY = 0.93f; // Slower variance decay for better adaptive thresholding
    private const float ONSET_FLUX_THRESHOLD = 0.15f; // Minimum energy increase as fraction of average
    
    // Sub-band analysis (frequency ranges)
    private const int SUB_BASS_BINS = 8;   // ~20-60 Hz (kick drum)
    private const int BASS_BINS = 15;      // ~60-250 Hz (bass guitar, low toms)
    private const int LOW_MID_BINS = 10;   // ~250-500 Hz (snare transients)
    
    // BPM tracking for adaptive cooldown
    private readonly Queue<DateTime> beatTimestamps = new Queue<DateTime>();
    private const int BPM_HISTORY_SIZE = 8; // Track last 8 beats
    private float estimatedBPM = 120f; // Default assumption
    private int dynamicCooldownFrames = 8; // Adaptive cooldown based on BPM
    
    // Peak detection
    private float[] energyHistory = new float[3]; // Track last 3 energy values for peak detection
    private int energyHistoryIndex = 0;

    public MainWindow()
    {
        InitializeComponent();

        // Initialize available modes (non-shuffle modes)
        availableModes = new List<IVisualizerMode>
        {
            new SpectrumBarsMode(),
            new ParticleMode(),
            new CircleMode(),
            new TwoCirclesMode(),
            new FairiesMode(),
            new WaveMode(),
            new ColdWarMode(),
            new DanceMode()
        };

        // Initialize shuffle mode with all available modes
        shuffleMode = new ShuffleMode(availableModes);

        // Set shuffle as the default mode
        currentMode = shuffleMode;
        isShuffleActive = true;

        // Populate mode menu - shuffle mode first
        ModeListBox.Items.Add(shuffleMode.Name);
        foreach (var mode in availableModes)
        {
            ModeListBox.Items.Add(mode.Name);
        }

        // Set shuffle mode as the selected item in the menu
        ModeListBox.SelectedIndex = 0;

        // Setup shuffle timer (30 seconds)
        shuffleTimer = new System.Timers.Timer(30000);
        shuffleTimer.Elapsed += OnShuffleTimerElapsed;
        shuffleTimer.Start();

        // Show initial mode info
        ShowModeInfo(currentMode);

        // Setup fade storyboard
        fadeStoryboard = new Storyboard();
        var animation = new DoubleAnimation
        {
            From = 1.0,
            To = 0.0,
            Duration = TimeSpan.FromSeconds(1)
        };
        Storyboard.SetTarget(animation, StatusText);
        Storyboard.SetTargetProperty(animation, new PropertyPath(TextBlock.OpacityProperty));
        fadeStoryboard.Children.Add(animation);

        SkiaView.PaintSurface += OnPaint;

        capture = new WasapiLoopbackCapture();
        capture.DataAvailable += OnDataAvailable;
        captureThread = new Thread(AudioCapture);
        captureThread.IsBackground = true;
        captureThread.Start();

        // Timer to force repaint at ~60fps (16ms) with frame skip protection
        timer = new System.Timers.Timer(16);
        timer.Elapsed += (s, e) =>
        {
            if (!renderPending)
            {
                renderPending = true;
                Dispatcher.BeginInvoke(() =>
                {
                    SkiaView.InvalidateVisual();
                    renderPending = false;
                }, DispatcherPriority.Render);
            }
        };
        timer.Start();

        this.Closing += MainWindow_Closing;
        
        // Hook up mode selection handler
        ModeListBox.SelectionChanged += ModeListBox_SelectionChanged;

        // Initialize media session manager
        InitializeMediaSession();
    }

    private void StartTrackInfoFadeTimer()
    {
        trackInfoFadeTimer?.Stop();
        trackInfoFadeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        trackInfoFadeTimer.Tick += (s, e) =>
        {
            var fadeOut = new DoubleAnimation
            {
                From = 0.8,
                To = 0.0,
                Duration = TimeSpan.FromSeconds(0.5)
            };
            TrackInfoText.BeginAnimation(TextBlock.OpacityProperty, fadeOut);
            trackInfoFadeTimer?.Stop();
        };
        trackInfoFadeTimer.Start();
    }

    private async void InitializeMediaSession()
    {
        try
        {
            mediaSessionManager = new MediaSessionManager();
            mediaSessionManager.TrackChanged += OnTrackChanged;
            mediaSessionManager.MediaPlayerChanged += OnMediaPlayerChanged;
            await mediaSessionManager.InitializeAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize media session: {ex.Message}");
        }
    }

    private void OnTrackChanged(object? sender, MediaTrackInfo trackInfo)
    {
        Dispatcher.BeginInvoke(() =>
        {
            // Update track info text
            TrackInfoText.Text = $"🎵 {trackInfo}";
            
            if (!isTrackInfoPersistent)
            {
                // Fade in
                var fadeIn = new DoubleAnimation
                {
                    From = 0.0,
                    To = 0.8,
                    Duration = TimeSpan.FromSeconds(0.5)
                };
                TrackInfoText.BeginAnimation(TextBlock.OpacityProperty, fadeIn);
                
                // Setup fade out after 3 seconds
                StartTrackInfoFadeTimer();
            }
            else
            {
                // Keep persistent display visible
                TrackInfoText.Opacity = 0.8;
            }
        });
    }

    private void OnMediaPlayerChanged(object? sender, string appName)
    {
        // Just log for debugging, no UI notification
        var readableName = appName?.Split('\\').Last().Replace(".exe", "") ?? "Media Player";
        System.Diagnostics.Debug.WriteLine($"Media player changed to: {readableName}");
    }

    private void ModeListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModeListBox.SelectedIndex < 0) return;

        // Index 0 is shuffle mode, indices 1+ are regular modes
        if (ModeListBox.SelectedIndex == 0)
        {
            // User selected shuffle mode
            currentMode = shuffleMode;
            isShuffleActive = true;
            shuffleTimer.Start();
            ShowModeInfo(shuffleMode.CurrentMode);
        }
        else if (ModeListBox.SelectedIndex <= availableModes.Count)
        {
            // User selected a specific mode (subtract 1 because shuffle is at index 0)
            currentMode = availableModes[ModeListBox.SelectedIndex - 1];
            isShuffleActive = false;
            shuffleTimer.Stop();
            ShowModeInfo(currentMode);
        }

        // Hide the menu after selection
        isModeMenuVisible = false;
        ModeMenu.Visibility = Visibility.Collapsed;
        
        // Restore track info visibility based on mode
        if (!string.IsNullOrEmpty(TrackInfoText.Text))
        {
            if (isTrackInfoPersistent)
            {
                // Persistent mode: keep visible
                TrackInfoText.Opacity = 0.8;
            }
            else if (mediaSessionManager?.CurrentTrack != null)
            {
                // Temporary mode with active track: trigger new fade cycle
                var fadeIn = new DoubleAnimation
                {
                    From = 0.0,
                    To = 0.8,
                    Duration = TimeSpan.FromSeconds(0.5)
                };
                TrackInfoText.BeginAnimation(TextBlock.OpacityProperty, fadeIn);
                
                StartTrackInfoFadeTimer();
            }
        }
    }

    private void OnShuffleTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
    {
        if (isShuffleActive && currentMode == shuffleMode)
        {
            // Select a new random mode
            shuffleMode.SelectRandomMode();
            
            // Show the new mode info
            Dispatcher.Invoke(() => ShowModeInfo(shuffleMode.CurrentMode));
        }
    }

    private void ShowModeInfo(IVisualizerMode mode)
    {
        // Update mode info text with emoji and name
        ModeInfoText.Text = $"{mode.Emoji} {mode.Name}";
        
        // Fade in
        var fadeIn = new DoubleAnimation
        {
            From = 0.0,
            To = 0.8,
            Duration = TimeSpan.FromSeconds(0.5)
        };
        ModeInfoText.BeginAnimation(TextBlock.OpacityProperty, fadeIn);
        
        // Setup fade out after 3 seconds
        modeInfoFadeTimer?.Stop();
        modeInfoFadeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        modeInfoFadeTimer.Tick += (s, e) =>
        {
            var fadeOut = new DoubleAnimation
            {
                From = 0.8,
                To = 0.0,
                Duration = TimeSpan.FromSeconds(0.5)
            };
            ModeInfoText.BeginAnimation(TextBlock.OpacityProperty, fadeOut);
            modeInfoFadeTimer?.Stop();
        };
        modeInfoFadeTimer.Start();
    }

    private void ToggleFullscreen()
    {
        if (isFullscreen)
        {
            WindowStyle = WindowStyle.SingleBorderWindow;
            WindowState = WindowState.Normal;
            ResizeMode = ResizeMode.CanResize;
        }
        else
        {
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
            ResizeMode = ResizeMode.NoResize;
        }
        isFullscreen = !isFullscreen;

        // Show fullscreen status message
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = isFullscreen ? "fullscreen: on" : "fullscreen: off";
            StatusText.Opacity = 1;
            fadeStoryboard.Begin();
        });
    }

    private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!isClosing)
        {
            isClosing = true;
            e.Cancel = true;

            // Stop capture immediately to prevent further UI updates (e.g., overwriting "peace out")
            running = false;
            timer?.Stop();
            shuffleTimer?.Stop();
            if (capture != null)
            {
                capture.DataAvailable -= OnDataAvailable;
                capture.StopRecording();
            }

            // Show "peace out" message
            Dispatcher.Invoke(() =>
            {
                StatusText.Inlines.Clear();
                StatusText.Inlines.Add(new System.Windows.Documents.Run("peace out") 
                { 
                    FontSize = 48 
                });
                StatusText.Opacity = 1;
                // Force layout/render so the message appears even under heavier render loads
                StatusText.UpdateLayout();
                Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                // Start timer for 2 seconds
                var fadeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                fadeTimer.Tick += (s, e) =>
                {
                    fadeStoryboard.Begin();
                    fadeTimer.Stop();
                };
                fadeTimer.Start();
            });

            // Start a delay timer for 2 seconds before shutting down
            var delayTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            delayTimer.Tick += (s, args) =>
            {
                delayTimer.Stop();
                timer?.Dispose();
                shuffleTimer?.Dispose();
                if (capture != null)
                {
                    capture.Dispose();
                }
                mediaSessionManager?.Dispose();
                Application.Current.Shutdown();
            };
            delayTimer.Start();
        }
    }

    private async void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F || e.Key == Key.F11)
        {
            ToggleFullscreen();
            e.Handled = true;
        }
        else if (e.Key == Key.Q || e.Key == Key.Escape)
        {
            this.Close();
            e.Handled = true;
        }
        else if (e.Key == Key.Space) // Toggle play/pause
        {
            if (mediaSessionManager != null)
            {
                var result = await mediaSessionManager.TryTogglePlayPauseAsync();
                if (result != null)
                {
                    StatusText.Text = $"{result}";
                    StatusText.Opacity = 1;
                    fadeStoryboard.Begin();
                }
            }
            e.Handled = true;
        }
        else if (e.Key == Key.OemTilde) // Backtick key
        {
            isModeMenuVisible = !isModeMenuVisible;
            ModeMenu.Visibility = isModeMenuVisible ? Visibility.Visible : Visibility.Collapsed;
            e.Handled = true;
        }
        else if (e.Key == Key.F3) // Toggle FPS display
        {
            showFps = !showFps;
            FpsText.Opacity = showFps ? 0.8 : 0;
            if (!showFps)
            {
                FpsText.Text = "";
            }
            e.Handled = true;
        }
        else if (e.Key == Key.I) // Toggle persistent track info
        {
            isTrackInfoPersistent = !isTrackInfoPersistent;
            
            if (isTrackInfoPersistent)
            {
                // Stop any fade animation
                trackInfoFadeTimer?.Stop();
                TrackInfoText.BeginAnimation(TextBlock.OpacityProperty, null); // Cancel any ongoing animation
                
                // If we have current track info, show it immediately
                if (mediaSessionManager?.CurrentTrack != null)
                {
                    TrackInfoText.Text = $"🎵 {mediaSessionManager.CurrentTrack}";
                    TrackInfoText.Opacity = 0.8; // Always show it when we have a track
                }
                else if (!string.IsNullOrEmpty(TrackInfoText.Text))
                {
                    // We have text but no current session - still show what we have
                    TrackInfoText.Opacity = 0.8;
                }
                
                // Show status
                StatusText.Text = "track info: persistent";
                StatusText.Opacity = 1;
                fadeStoryboard.Begin();
            }
            else
            {
                // Switching to temporary mode - start fade out after 3 seconds if we have track info
                if (!string.IsNullOrEmpty(TrackInfoText.Text) && TrackInfoText.Text != "🎵 Unknown Track")
                {
                    // Start the temporary mode timer
                    StartTrackInfoFadeTimer();
                }
                else
                {
                    // No valid track info, fade out immediately
                    var fadeOut = new DoubleAnimation
                    {
                        From = TrackInfoText.Opacity,
                        To = 0.0,
                        Duration = TimeSpan.FromSeconds(0.5)
                    };
                    TrackInfoText.BeginAnimation(TextBlock.OpacityProperty, fadeOut);
                }
                
                // Show status
                StatusText.Text = "track info: temporary";
                StatusText.Opacity = 1;
                fadeStoryboard.Begin();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.D0 || e.Key == Key.NumPad0) // Shuffle mode
        {
            if (isShuffleActive)
            {
                // Already in shuffle mode, select a new random mode
                shuffleMode.SelectRandomMode();
            }
            currentMode = shuffleMode;
            isShuffleActive = true;
            shuffleTimer.Start();
            ModeListBox.SelectedIndex = 0;
            StatusText.Text = $"shuffle: {shuffleMode.CurrentMode.Name}";
            StatusText.Opacity = 1;
            fadeStoryboard.Begin();
            e.Handled = true;
        }
        else if (e.Key == Key.D1 || e.Key == Key.NumPad1) // Spectrum Bars
        {
            currentMode = availableModes[0]; // SpectrumBarsMode
            isShuffleActive = false;
            shuffleTimer.Stop();
            ModeListBox.SelectedIndex = 1;
            StatusText.Text = currentMode.Name;
            StatusText.Opacity = 1;
            fadeStoryboard.Begin();
            e.Handled = true;
        }
        else if (e.Key == Key.D2 || e.Key == Key.NumPad2) // Particle
        {
            currentMode = availableModes[1]; // ParticleMode
            isShuffleActive = false;
            shuffleTimer.Stop();
            ModeListBox.SelectedIndex = 2;
            StatusText.Text = currentMode.Name;
            StatusText.Opacity = 1;
            fadeStoryboard.Begin();
            e.Handled = true;
        }
        else if (e.Key == Key.D3 || e.Key == Key.NumPad3) // Circle
        {
            currentMode = availableModes[2]; // CircleMode
            isShuffleActive = false;
            shuffleTimer.Stop();
            ModeListBox.SelectedIndex = 3;
            StatusText.Text = currentMode.Name;
            StatusText.Opacity = 1;
            fadeStoryboard.Begin();
            e.Handled = true;
        }
        else if (e.Key == Key.D4 || e.Key == Key.NumPad4) // Two Circles
        {
            currentMode = availableModes[3]; // TwoCirclesMode
            isShuffleActive = false;
            shuffleTimer.Stop();
            ModeListBox.SelectedIndex = 4;
            StatusText.Text = currentMode.Name;
            StatusText.Opacity = 1;
            fadeStoryboard.Begin();
            e.Handled = true;
        }
        else if (e.Key == Key.D5 || e.Key == Key.NumPad5) // Fairies
        {
            currentMode = availableModes[4]; // FairiesMode
            isShuffleActive = false;
            shuffleTimer.Stop();
            ModeListBox.SelectedIndex = 5;
            StatusText.Text = currentMode.Name;
            StatusText.Opacity = 1;
            fadeStoryboard.Begin();
            e.Handled = true;
        }
        else if (e.Key == Key.D6 || e.Key == Key.NumPad6) // Wave
        {
            currentMode = availableModes[5]; // WaveMode
            isShuffleActive = false;
            shuffleTimer.Stop();
            ModeListBox.SelectedIndex = 6;
            StatusText.Text = currentMode.Name;
            StatusText.Opacity = 1;
            fadeStoryboard.Begin();
            e.Handled = true;
        }
        else if (e.Key == Key.D7 || e.Key == Key.NumPad7) // Cold War
        {
            currentMode = availableModes[6]; // ColdWarMode
            isShuffleActive = false;
            shuffleTimer.Stop();
            ModeListBox.SelectedIndex = 7;
            StatusText.Text = currentMode.Name;
            StatusText.Opacity = 1;
            fadeStoryboard.Begin();
            e.Handled = true;
        }
        else if (e.Key == Key.D8 || e.Key == Key.NumPad8) // Dance
        {
            currentMode = availableModes[7]; // DanceMode
            isShuffleActive = false;
            shuffleTimer.Stop();
            ModeListBox.SelectedIndex = 8;
            StatusText.Text = currentMode.Name;
            StatusText.Opacity = 1;
            fadeStoryboard.Begin();
            e.Handled = true;
        }
    }

    private void AudioCapture()
    {
        capture.StartRecording();
        while (running)
        {
            Thread.Sleep(100);
        }
        // No need to stop again, already stopped in Closing
    }

    private void OnDataAvailable(object sender, WaveInEventArgs e)
    {
        if (!running || isClosing) return;

        // Convert to float array
        int sampleCount = e.BytesRecorded / 4; // 32-bit float
        float[] samples = new float[sampleCount];
        System.Buffer.BlockCopy(e.Buffer, 0, samples, 0, e.BytesRecorded);

        if (samples.Any(f => f != 0))
        {
            if (!audioDetected)
            {
                audioDetected = true;
                Dispatcher.Invoke(() =>
                {
                    StatusText.Inlines.Clear();
                    StatusText.Inlines.Add(new System.Windows.Documents.Run("earhole") 
                    { 
                        FontSize = 48 
                    });
                    StatusText.Inlines.Add(new System.Windows.Documents.LineBreak());
                    StatusText.Inlines.Add(new System.Windows.Documents.Run("` for modes") 
                    { 
                        FontSize = 12 
                    });
                    StatusText.Opacity = 1;
                    // Start timer for 5 seconds
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                    timer.Tick += (s, e) =>
                    {
                        fadeStoryboard.Begin();
                        timer.Stop();
                    };
                    timer.Start();
                });
            }
        }
        else
        {
            return; // No valid audio data
        }

        // Separate left and right channels
        float[] leftChannel = new float[sampleCount / 2];
        float[] rightChannel = new float[sampleCount / 2];
        for (int i = 0; i < leftChannel.Length; i++)
        {
            leftChannel[i] = samples[i * 2];      // Left channel
            rightChannel[i] = samples[i * 2 + 1]; // Right channel
        }

        // Ensure FFT size is sufficient for SPECTRUM_RESOLUTION
        // FFT produces N/2 frequency bins, so we need at least SPECTRUM_RESOLUTION * 2
        int minFftSize = SPECTRUM_RESOLUTION * 2;
        int fftSize = NextPowerOfTwo(Math.Max(leftChannel.Length, minFftSize));
        if (fftSize < 2) return;
        Array.Resize(ref leftChannel, fftSize);
        Array.Resize(ref rightChannel, fftSize);

        // FFT for left channel
        var leftComplex = leftChannel.Select(x => new Complex(x, 0)).ToArray();
        Fourier.Forward(leftComplex, FourierOptions.NoScaling);

        // FFT for right channel
        var rightComplex = rightChannel.Select(x => new Complex(x, 0)).ToArray();
        Fourier.Forward(rightComplex, FourierOptions.NoScaling);

        lock (this.spectrumLock)
        {
            // Clear spectrum arrays first to avoid leftover data in unfilled bins
            Array.Clear(this.leftSpectrum, 0, this.leftSpectrum.Length);
            Array.Clear(this.rightSpectrum, 0, this.rightSpectrum.Length);
            
            int maxBins = Math.Min(this.leftSpectrum.Length, leftComplex.Length / 2);
            for (int i = 0; i < maxBins; i++)
            {
                this.leftSpectrum[i] = (float)leftComplex[i].Magnitude;
            }
            
            maxBins = Math.Min(this.rightSpectrum.Length, rightComplex.Length / 2);
            for (int i = 0; i < maxBins; i++)
            {
                this.rightSpectrum[i] = (float)rightComplex[i].Magnitude;
            }

            // Beat detection: Multi-band analysis with peak detection and BPM awareness
            
            // Calculate sub-band energies
            int beatMaxBins = Math.Min(this.leftSpectrum.Length, this.rightSpectrum.Length);
            float subBassEnergy = 0f;
            float bassEnergy = 0f;
            float lowMidEnergy = 0f;
            
            // Sub-bass (kick drum range) - highest weight
            int subBassEnd = Math.Min(SUB_BASS_BINS, beatMaxBins);
            for (int i = 0; i < subBassEnd; i++)
            {
                subBassEnergy += (this.leftSpectrum[i] + this.rightSpectrum[i]) / 2f;
            }
            subBassEnergy /= Math.Max(1, subBassEnd);
            
            // Bass range (bass guitar, low toms)
            int bassEnd = Math.Min(SUB_BASS_BINS + BASS_BINS, beatMaxBins);
            for (int i = subBassEnd; i < bassEnd; i++)
            {
                bassEnergy += (this.leftSpectrum[i] + this.rightSpectrum[i]) / 2f;
            }
            bassEnergy /= Math.Max(1, bassEnd - subBassEnd);
            
            // Low-mid range (snare transients)
            int lowMidEnd = Math.Min(SUB_BASS_BINS + BASS_BINS + LOW_MID_BINS, beatMaxBins);
            for (int i = bassEnd; i < lowMidEnd; i++)
            {
                lowMidEnergy += (this.leftSpectrum[i] + this.rightSpectrum[i]) / 2f;
            }
            lowMidEnergy /= Math.Max(1, lowMidEnd - bassEnd);
            
            // Combine sub-bands with weighting (emphasize sub-bass)
            float currentEnergy = subBassEnergy * 0.6f + bassEnergy * 0.3f + lowMidEnergy * 0.1f;

            // Store in circular buffer for peak detection
            energyHistory[energyHistoryIndex] = currentEnergy;
            energyHistoryIndex = (energyHistoryIndex + 1) % energyHistory.Length;
            
            // Calculate instantaneous energy change (onset flux)
            float energyDelta = currentEnergy - lastEnergy;
            lastEnergy = currentEnergy;

            // Update variance for adaptive thresholding
            float diff = currentEnergy - averageEnergy;
            energyVariance = energyVariance * VARIANCE_DECAY + (diff * diff) * (1 - VARIANCE_DECAY);
            float stdDev = (float)Math.Sqrt(Math.Max(0, energyVariance));

            // Update running average with decay
            averageEnergy = averageEnergy * ENERGY_DECAY + currentEnergy * (1 - ENERGY_DECAY);

            // Decrement cooldown
            if (beatCooldown > 0) beatCooldown--;
            
            // Check if we're at a local peak (current > previous AND current > next)
            // Use circular buffer indexing
            int prevIdx = (energyHistoryIndex + energyHistory.Length - 2) % energyHistory.Length;
            int currIdx = (energyHistoryIndex + energyHistory.Length - 1) % energyHistory.Length;
            bool isLocalPeak = currentEnergy >= energyHistory[prevIdx] && 
                               energyHistory[currIdx] >= energyHistory[prevIdx];

            // Detect beat using multiple criteria:
            // 1. Current energy exceeds threshold above average
            // 2. Strong positive onset flux (significant energy increase)
            // 3. Energy exceeds average + standard deviation (adaptive threshold)
            // 4. Local peak detection (prevents early/late triggering)
            // 5. Not in cooldown period
            float dynamicThreshold = averageEnergy + stdDev * 0.5f;
            float onsetFluxThreshold = averageEnergy * ONSET_FLUX_THRESHOLD;
            
            if (beatCooldown == 0 && 
                currentEnergy > averageEnergy * BEAT_THRESHOLD &&
                energyDelta > onsetFluxThreshold &&
                currentEnergy > dynamicThreshold &&
                isLocalPeak)
            {
                this.isBeat = true;
                
                // Track beat timing for BPM estimation
                DateTime now = DateTime.Now;
                beatTimestamps.Enqueue(now);
                if (beatTimestamps.Count > BPM_HISTORY_SIZE)
                {
                    beatTimestamps.Dequeue();
                }
                
                // Estimate BPM from recent beat intervals
                if (beatTimestamps.Count >= 3)
                {
                    var timestamps = beatTimestamps.ToArray();
                    double totalSeconds = (timestamps[timestamps.Length - 1] - timestamps[0]).TotalSeconds;
                    int intervals = timestamps.Length - 1;
                    if (totalSeconds > 0 && intervals > 0)
                    {
                        float beatsPerSecond = intervals / (float)totalSeconds;
                        float newBPM = beatsPerSecond * 60f;
                        
                        // Clamp to reasonable BPM range (60-200)
                        if (newBPM >= 60f && newBPM <= 200f)
                        {
                            // Smooth BPM estimation
                            estimatedBPM = estimatedBPM * 0.7f + newBPM * 0.3f;
                        }
                    }
                }
                
                // Set adaptive cooldown based on estimated BPM
                // At 120 BPM: 500ms between beats = 30 frames at 60fps
                // Scale cooldown to be ~40% of beat interval
                float secondsPerBeat = 60f / estimatedBPM;
                dynamicCooldownFrames = Math.Max(6, (int)(secondsPerBeat * 60f * 0.4f));
                beatCooldown = dynamicCooldownFrames;
            }
            else
            {
                this.isBeat = false;
            }
        }
    }

    private void OnPaint(object sender, SkiaSharp.Views.Desktop.SKPaintSurfaceEventArgs e)
    {        
        var canvas = e.Surface.Canvas;
        int width = e.Info.Width;
        int height = e.Info.Height;

        // Skip expensive rendering when menu is visible to keep UI responsive
        if (isModeMenuVisible)
        {
            canvas.Clear(SKColors.Black);
            return;
        }
        
        // Update FPS counter
        frameCount++;
        var now = DateTime.Now;
        var elapsed = (now - lastFpsUpdate).TotalSeconds;
        if (elapsed >= 0.5) // Update FPS display twice per second
        {
            currentFps = frameCount / elapsed;
            frameCount = 0;
            lastFpsUpdate = now;
            
            if (showFps)
            {
                FpsText.Text = $"{currentFps:F1} fps";
            }
        }
        
        // Copy spectrum data outside of lock to minimize lock contention
        float[] leftCopy = new float[SPECTRUM_RESOLUTION];
        float[] rightCopy = new float[SPECTRUM_RESOLUTION];
        bool beatCopy;
        
        lock (this.spectrumLock)
        {
            Array.Copy(this.leftSpectrum, leftCopy, SPECTRUM_RESOLUTION);
            Array.Copy(this.rightSpectrum, rightCopy, SPECTRUM_RESOLUTION);
            beatCopy = this.isBeat;
        }

        // Render without holding the lock
        currentMode.Render(canvas, width, height, leftCopy, rightCopy, beatCopy);
    }
}