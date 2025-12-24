using SkiaSharp;
using System;
using System.Collections.Generic;

namespace earhole.Modes;

/// <summary>
/// A meta-mode that shuffles through all available visualizer modes at regular intervals.
/// This mode delegates rendering to a randomly selected mode and changes modes periodically.
/// </summary>
public class ShuffleMode : IVisualizerMode
{
    private List<IVisualizerMode> modes;
    private IVisualizerMode currentMode = null!;
    private Random random;
    private int lastModeIndex = -1;

    public string Name => "🔀 Shuffle";
    public string Emoji => currentMode?.Emoji ?? "🔀";

    public ShuffleMode(List<IVisualizerMode> availableModes)
    {
        if (availableModes == null || availableModes.Count == 0)
        {
            throw new ArgumentException("Must provide at least one visualizer mode", nameof(availableModes));
        }

        // Create a copy of the modes list (excluding shuffle itself if it's in there)
        modes = new List<IVisualizerMode>(availableModes);
        
        // Initialize random with a seed based on current time for better randomness
        random = new Random(Guid.NewGuid().GetHashCode());
        
        // Select the first mode randomly
        SelectRandomMode();
    }

    /// <summary>
    /// Selects a new random mode that is different from the current one
    /// </summary>
    public void SelectRandomMode()
    {
        if (modes.Count == 0) return;

        if (modes.Count == 1)
        {
            // Only one mode available, use it
            currentMode = modes[0];
            lastModeIndex = 0;
        }
        else
        {
            // Select a random mode that's different from the last one
            int newIndex;
            int attempts = 0;
            do
            {
                newIndex = random.Next(modes.Count);
                attempts++;
                // Prevent infinite loop if somehow all modes are the same
                if (attempts > 100) break;
            }
            while (newIndex == lastModeIndex);

            lastModeIndex = newIndex;
            currentMode = modes[newIndex];
        }

        System.Diagnostics.Debug.WriteLine($"Shuffle: Selected {currentMode.Name}");
    }

    public void Render(SKCanvas canvas, int width, int height, float[] leftSpectrum, float[] rightSpectrum, bool isBeat)
    {
        // Delegate rendering to the current mode
        currentMode?.Render(canvas, width, height, leftSpectrum, rightSpectrum, isBeat);
    }

    /// <summary>
    /// Gets the currently active mode being displayed
    /// </summary>
    public IVisualizerMode CurrentMode => currentMode;
}
