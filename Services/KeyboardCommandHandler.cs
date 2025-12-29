using System.Windows.Input;
using earhole.Modes;

namespace earhole.Services;

/// <summary>
/// Handles keyboard commands for visualizer control
/// </summary>
public class KeyboardCommandHandler
{
    private readonly ModeManagementService modeService;
    private readonly MediaSessionManager? mediaSessionManager;

    public event EventHandler? ToggleFullscreenRequested;
    public event EventHandler? CloseRequested;
    public event EventHandler? ToggleModeMenuRequested;
    public event EventHandler? ToggleFpsRequested;
    public event EventHandler? ToggleTrackInfoPersistenceRequested;
    public event EventHandler<string>? StatusMessageRequested;

    public KeyboardCommandHandler(
        ModeManagementService modeService, 
        MediaSessionManager? mediaSessionManager)
    {
        this.modeService = modeService;
        this.mediaSessionManager = mediaSessionManager;
    }

    public async Task HandleKeyDownAsync(Key key)
    {
        switch (key)
        {
            case Key.F:
            case Key.F11:
                ToggleFullscreenRequested?.Invoke(this, EventArgs.Empty);
                break;

            case Key.Q:
            case Key.Escape:
                CloseRequested?.Invoke(this, EventArgs.Empty);
                break;

            case Key.Space:
                if (mediaSessionManager != null)
                {
                    var result = await mediaSessionManager.TryTogglePlayPauseAsync();
                    if (result != null)
                    {
                        StatusMessageRequested?.Invoke(this, result);
                    }
                }
                break;

            case Key.OemTilde: // Backtick key
                ToggleModeMenuRequested?.Invoke(this, EventArgs.Empty);
                break;

            case Key.F3:
                ToggleFpsRequested?.Invoke(this, EventArgs.Empty);
                break;

            case Key.I:
                ToggleTrackInfoPersistenceRequested?.Invoke(this, EventArgs.Empty);
                break;

            case Key.D0:
            case Key.NumPad0:
                HandleShuffleMode();
                break;

            case Key.D1:
            case Key.NumPad1:
                HandleSpecificMode(0, 1);
                break;

            case Key.D2:
            case Key.NumPad2:
                HandleSpecificMode(1, 2);
                break;

            case Key.D3:
            case Key.NumPad3:
                HandleSpecificMode(2, 3);
                break;

            case Key.D4:
            case Key.NumPad4:
                HandleSpecificMode(3, 4);
                break;

            case Key.D5:
            case Key.NumPad5:
                HandleSpecificMode(4, 5);
                break;

            case Key.D6:
            case Key.NumPad6:
                HandleSpecificMode(5, 6);
                break;

            case Key.D7:
            case Key.NumPad7:
                HandleSpecificMode(6, 7);
                break;

            case Key.D8:
            case Key.NumPad8:
                HandleSpecificMode(7, 8);
                break;
        }
    }

    private void HandleShuffleMode()
    {
        modeService.ActivateShuffleMode();
        var message = modeService.IsShuffleActive 
            ? $"shuffle: {modeService.ShuffleMode.CurrentMode.Name}" 
            : "shuffle";
        StatusMessageRequested?.Invoke(this, message);
    }

    private void HandleSpecificMode(int modeIndex, int listBoxIndex)
    {
        if (modeIndex < modeService.AvailableModes.Count)
        {
            modeService.SetMode(listBoxIndex);
            StatusMessageRequested?.Invoke(this, modeService.CurrentMode.Name);
        }
    }
}
