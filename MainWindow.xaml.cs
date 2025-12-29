using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NAudio.Wave;
using MathNet.Numerics.IntegralTransforms;
using System.Numerics;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using earhole.Modes;

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
    private const float BEAT_THRESHOLD = 1.5f; // Increased for better precision
    private const float ENERGY_DECAY = 0.97f; // Slower decay for more stable average
    private const float VARIANCE_DECAY = 0.96f;
    private const int BEAT_BINS = 20; // Increased to capture more bass frequencies
    private const int BEAT_COOLDOWN_FRAMES = 8; // Minimum frames between beats (~133ms at 60fps)

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

        // Timer to force repaint
        timer = new System.Timers.Timer(100);
        timer.Elapsed += (s, e) => Dispatcher.BeginInvoke(() => SkiaView.InvalidateVisual());
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

    private void Window_KeyDown(object sender, KeyEventArgs e)
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

        // Ensure length is power of 2 for FFT
        int fftSize = NextPowerOfTwo(Math.Min(leftChannel.Length, 2048)); // Cap at 2048
        if (fftSize > leftChannel.Length) fftSize = leftChannel.Length;
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
            for (int i = 0; i < this.leftSpectrum.Length && i < leftComplex.Length / 2; i++)
            {
                this.leftSpectrum[i] = (float)leftComplex[i].Magnitude;
            }
            for (int i = 0; i < this.rightSpectrum.Length && i < rightComplex.Length / 2; i++)
            {
                this.rightSpectrum[i] = (float)rightComplex[i].Magnitude;
            }

            // Beat detection: monitor low-frequency energy with frequency weighting
            float currentEnergy = 0f;
            int binsToCheck = Math.Min(BEAT_BINS, Math.Min(this.leftSpectrum.Length, this.rightSpectrum.Length));
            
            // Apply frequency weighting (emphasize lower bass frequencies)
            for (int i = 0; i < binsToCheck; i++)
            {
                // Weight decreases linearly: first bin = 1.0, last bin = 0.5
                float weight = 1.0f - (i / (float)binsToCheck) * 0.5f;
                currentEnergy += ((this.leftSpectrum[i] + this.rightSpectrum[i]) / 2f) * weight;
            }

            // Normalize by total weight
            currentEnergy /= (binsToCheck * 0.75f);

            // Calculate instantaneous energy change (onset detection)
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

            // Detect beat using multiple criteria:
            // 1. Current energy exceeds threshold above average
            // 2. Positive onset (energy increasing)
            // 3. Energy exceeds average + standard deviation (adaptive threshold)
            // 4. Not in cooldown period
            float dynamicThreshold = averageEnergy + stdDev * 0.5f;
            if (beatCooldown == 0 && 
                currentEnergy > averageEnergy * BEAT_THRESHOLD &&
                energyDelta > 0 &&
                currentEnergy > dynamicThreshold)
            {
                this.isBeat = true;
                beatCooldown = BEAT_COOLDOWN_FRAMES;
            }
            else
            {
                this.isBeat = false;
            }
        }

        // Invalidate the view to trigger repaint
        Dispatcher.BeginInvoke(() => SkiaView.InvalidateVisual());
    }

    private void OnPaint(object sender, SkiaSharp.Views.Desktop.SKPaintSurfaceEventArgs e)
    {        // Update FPS counter
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
                Dispatcher.BeginInvoke(() => FpsText.Text = $"{currentFps:F1} fps");
            }
        }
                var canvas = e.Surface.Canvas;
        int width = e.Info.Width;
        int height = e.Info.Height;

        // Use the current mode to render the visualization
        lock (this.spectrumLock)
        {
            currentMode.Render(canvas, width, height, this.leftSpectrum, this.rightSpectrum, this.isBeat);
        }
    }
}