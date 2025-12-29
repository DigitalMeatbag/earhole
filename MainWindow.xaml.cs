using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using earhole.Modes;
using earhole.Services;
using SkiaSharp;

namespace earhole;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const int SPECTRUM_RESOLUTION = 1024;
    
    // Services
    private readonly AudioCaptureService audioCaptureService;
    private readonly BeatDetectionService beatDetectionService;
    private readonly ModeManagementService modeManagementService;
    private readonly UINotificationService uiNotificationService;
    private readonly KeyboardCommandHandler keyboardCommandHandler;
    private MediaSessionManager? mediaSessionManager;
    
    // Rendering
    private System.Timers.Timer renderTimer;
    private volatile bool renderPending = false;
    
    // State
    private bool isFullscreen = false;
    private bool isClosing = false;
    private bool isModeMenuVisible = false;
    private bool showFps = false;
    
    // FPS tracking
    private DateTime lastFpsUpdate = DateTime.Now;
    private int frameCount = 0;
    private double currentFps = 0;

    public MainWindow()
    {
        InitializeComponent();

        // Initialize services
        audioCaptureService = new AudioCaptureService();
        beatDetectionService = new BeatDetectionService();
        modeManagementService = new ModeManagementService();
        uiNotificationService = new UINotificationService(StatusText, TrackInfoText, ModeInfoText, FpsText);
        
        // Setup event handlers
        audioCaptureService.AudioDetected += OnAudioDetected;
        audioCaptureService.SpectrumDataAvailable += OnSpectrumDataAvailable;
        modeManagementService.ModeChanged += OnModeChanged;
        modeManagementService.ShuffleModeChanged += OnShuffleModeChanged;

        // Initialize media session manager
        InitializeMediaSession();
        
        // Initialize keyboard handler after media session
        keyboardCommandHandler = new KeyboardCommandHandler(modeManagementService, mediaSessionManager);
        keyboardCommandHandler.ToggleFullscreenRequested += (s, e) => ToggleFullscreen();
        keyboardCommandHandler.CloseRequested += (s, e) => this.Close();
        keyboardCommandHandler.ToggleModeMenuRequested += (s, e) => ToggleModeMenu();
        keyboardCommandHandler.ToggleFpsRequested += (s, e) => ToggleFps();
        keyboardCommandHandler.ToggleTrackInfoPersistenceRequested += (s, e) => ToggleTrackInfoPersistence();
        keyboardCommandHandler.StatusMessageRequested += (s, message) => uiNotificationService.ShowStatusMessage(message);

        // Populate mode menu
        PopulateModeMenu();

        // Setup SkiaSharp painting
        SkiaView.PaintSurface += OnPaint;

        // Timer to force repaint at ~60fps (16ms) with frame skip protection
        renderTimer = new System.Timers.Timer(16);
        renderTimer.Elapsed += (s, e) =>
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
        renderTimer.Start();

        this.Closing += MainWindow_Closing;
        ModeListBox.SelectionChanged += ModeListBox_SelectionChanged;
        
        // Show initial mode info
        var currentMode = modeManagementService.CurrentMode;
        var displayMode = modeManagementService.IsShuffleActive 
            ? modeManagementService.ShuffleMode.CurrentMode 
            : currentMode;
        uiNotificationService.ShowModeInfo(displayMode.Emoji, displayMode.Name);
    }

    private void PopulateModeMenu()
    {
        // Add shuffle mode first
        ModeListBox.Items.Add(modeManagementService.ShuffleMode.Name);
        
        // Add all available modes
        foreach (var mode in modeManagementService.AvailableModes)
        {
            ModeListBox.Items.Add(mode.Name);
        }
        
        // Set shuffle mode as the selected item
        ModeListBox.SelectedIndex = 0;
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

    private void OnAudioDetected(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            uiNotificationService.ShowLargeMessage("earhole", "` for modes", 5);
        });
    }

    private void OnSpectrumDataAvailable(object? sender, SpectrumDataEventArgs e)
    {
        // Process beat detection with the spectrum data
        beatDetectionService.ProcessSpectrum(e.LeftSpectrum, e.RightSpectrum);
    }

    private void OnModeChanged(object? sender, ModeChangedEventArgs e)
    {
        var displayMode = e.IsShuffleActive 
            ? modeManagementService.ShuffleMode.CurrentMode 
            : e.NewMode;
        uiNotificationService.ShowModeInfo(displayMode.Emoji, displayMode.Name);
    }

    private void OnShuffleModeChanged(object? sender, IVisualizerMode mode)
    {
        Dispatcher.Invoke(() => uiNotificationService.ShowModeInfo(mode.Emoji, mode.Name));
    }

    private void OnTrackChanged(object? sender, MediaTrackInfo trackInfo)
    {
        Dispatcher.BeginInvoke(() =>
        {
            uiNotificationService.ShowTrackInfo(trackInfo.ToString());
        });
    }

    private void OnMediaPlayerChanged(object? sender, string appName)
    {
        var readableName = appName?.Split('\\').Last().Replace(".exe", "") ?? "Media Player";
        System.Diagnostics.Debug.WriteLine($"Media player changed to: {readableName}");
    }

    private void ModeListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModeListBox.SelectedIndex < 0) return;

        modeManagementService.SetMode(ModeListBox.SelectedIndex);

        // Hide the menu after selection
        isModeMenuVisible = false;
        ModeMenu.Visibility = Visibility.Collapsed;
        
        // Restore track info visibility
        uiNotificationService.RestoreTrackInfoAfterMenu();
    }

    private void ToggleModeMenu()
    {
        isModeMenuVisible = !isModeMenuVisible;
        ModeMenu.Visibility = isModeMenuVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ToggleFps()
    {
        showFps = !showFps;
        uiNotificationService.SetFpsVisibility(showFps);
    }

    private void ToggleTrackInfoPersistence()
    {
        var currentTrack = mediaSessionManager?.CurrentTrack?.ToString() ?? TrackInfoText.Text;
        uiNotificationService.ToggleTrackInfoPersistence(currentTrack);
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

        uiNotificationService.ShowStatusMessage(isFullscreen ? "fullscreen: on" : "fullscreen: off");
    }

    private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!isClosing)
        {
            isClosing = true;
            e.Cancel = true;

            // Stop services
            renderTimer?.Stop();
            audioCaptureService?.Stop();

            // Show "peace out" message
            Dispatcher.Invoke(() =>
            {
                uiNotificationService.ShowLargeMessage("peace out", null, 2);
                StatusText.UpdateLayout();
                Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            });

            // Delay before shutting down
            var delayTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            delayTimer.Tick += (s, args) =>
            {
                delayTimer.Stop();
                renderTimer?.Dispose();
                audioCaptureService?.Dispose();
                modeManagementService?.Dispose();
                mediaSessionManager?.Dispose();
                Application.Current.Shutdown();
            };
            delayTimer.Start();
        }
    }

    private async void Window_KeyDown(object sender, KeyEventArgs e)
    {
        await keyboardCommandHandler.HandleKeyDownAsync(e.Key);
        e.Handled = true;
    }

    private void OnPaint(object sender, SkiaSharp.Views.Desktop.SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        int width = e.Info.Width;
        int height = e.Info.Height;

        // Skip expensive rendering when menu is visible
        if (isModeMenuVisible)
        {
            canvas.Clear(SKColors.Black);
            return;
        }
        
        // Update FPS counter
        frameCount++;
        var now = DateTime.Now;
        var elapsed = (now - lastFpsUpdate).TotalSeconds;
        if (elapsed >= 0.5)
        {
            currentFps = frameCount / elapsed;
            frameCount = 0;
            lastFpsUpdate = now;
            
            if (showFps)
            {
                uiNotificationService.UpdateFpsDisplay(currentFps);
            }
        }
        
        // Get spectrum data and beat state
        float[] leftCopy = new float[SPECTRUM_RESOLUTION];
        float[] rightCopy = new float[SPECTRUM_RESOLUTION];
        audioCaptureService.GetSpectrumData(leftCopy, rightCopy);
        bool beatCopy = beatDetectionService.IsBeat;

        // Render current mode
        var currentMode = modeManagementService.CurrentMode;
        currentMode.Render(canvas, width, height, leftCopy, rightCopy, beatCopy);
    }
}