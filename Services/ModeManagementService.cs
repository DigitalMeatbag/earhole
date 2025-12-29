using earhole.Modes;

namespace earhole.Services;

/// <summary>
/// Manages visualizer mode selection, switching, and shuffle functionality
/// </summary>
public class ModeManagementService : IDisposable
{
    private readonly List<IVisualizerMode> availableModes;
    private readonly ShuffleMode shuffleMode;
    private readonly System.Timers.Timer shuffleTimer;
    private bool isShuffleActive = true;

    public event EventHandler<ModeChangedEventArgs>? ModeChanged;
    public event EventHandler<IVisualizerMode>? ShuffleModeChanged;

    public IVisualizerMode CurrentMode { get; private set; }
    public bool IsShuffleActive => isShuffleActive;
    public IReadOnlyList<IVisualizerMode> AvailableModes => availableModes;
    public ShuffleMode ShuffleMode => shuffleMode;

    public ModeManagementService()
    {
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
        CurrentMode = shuffleMode;
        isShuffleActive = true;

        // Setup shuffle timer (30 seconds)
        shuffleTimer = new System.Timers.Timer(30000);
        shuffleTimer.Elapsed += OnShuffleTimerElapsed;
        shuffleTimer.Start();
    }

    private void OnShuffleTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (isShuffleActive && CurrentMode == shuffleMode)
        {
            // Select a new random mode
            shuffleMode.SelectRandomMode();
            ShuffleModeChanged?.Invoke(this, shuffleMode.CurrentMode);
        }
    }

    public void SetMode(int modeIndex)
    {
        IVisualizerMode? previousMode = CurrentMode;
        
        // Index 0 is shuffle mode, indices 1+ are regular modes
        if (modeIndex == 0)
        {
            // User selected shuffle mode
            CurrentMode = shuffleMode;
            isShuffleActive = true;
            shuffleTimer.Start();
            ModeChanged?.Invoke(this, new ModeChangedEventArgs(previousMode, CurrentMode, isShuffleActive));
        }
        else if (modeIndex <= availableModes.Count)
        {
            // User selected a specific mode (subtract 1 because shuffle is at index 0)
            CurrentMode = availableModes[modeIndex - 1];
            isShuffleActive = false;
            shuffleTimer.Stop();
            ModeChanged?.Invoke(this, new ModeChangedEventArgs(previousMode, CurrentMode, isShuffleActive));
        }
    }

    public void SetModeByType<T>() where T : IVisualizerMode
    {
        var mode = availableModes.FirstOrDefault(m => m is T);
        if (mode != null)
        {
            var index = availableModes.IndexOf(mode);
            SetMode(index + 1); // +1 because index 0 is shuffle
        }
    }

    public void ActivateShuffleMode()
    {
        if (isShuffleActive)
        {
            // Already in shuffle mode, select a new random mode
            shuffleMode.SelectRandomMode();
            ShuffleModeChanged?.Invoke(this, shuffleMode.CurrentMode);
        }
        else
        {
            SetMode(0); // Set to shuffle mode
        }
    }

    public void TriggerNewShuffleMode()
    {
        if (CurrentMode == shuffleMode)
        {
            shuffleMode.SelectRandomMode();
            ShuffleModeChanged?.Invoke(this, shuffleMode.CurrentMode);
        }
    }

    public void Dispose()
    {
        shuffleTimer?.Stop();
        shuffleTimer?.Dispose();
        GC.SuppressFinalize(this);
    }
}

public class ModeChangedEventArgs : EventArgs
{
    public IVisualizerMode? PreviousMode { get; }
    public IVisualizerMode NewMode { get; }
    public bool IsShuffleActive { get; }

    public ModeChangedEventArgs(IVisualizerMode? previousMode, IVisualizerMode newMode, bool isShuffleActive)
    {
        PreviousMode = previousMode;
        NewMode = newMode;
        IsShuffleActive = isShuffleActive;
    }
}
