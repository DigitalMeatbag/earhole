using SkiaSharp;

namespace earhole.Modes;

/// <summary>
/// Interface for visualizer modes that render audio spectrum data
/// </summary>
public interface IVisualizerMode
{
    /// <summary>
    /// Gets the name of this visualizer mode
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the emoji icon for this visualizer mode
    /// </summary>
    string Emoji { get; }

    /// <summary>
    /// Renders the visualizer on the canvas using the provided spectrum data
    /// </summary>
    /// <param name="canvas">The SkiaSharp canvas to draw on</param>
    /// <param name="width">Width of the canvas</param>
    /// <param name="height">Height of the canvas</param>
    /// <param name="leftSpectrum">Array of spectrum magnitudes from FFT for left channel</param>
    /// <param name="rightSpectrum">Array of spectrum magnitudes from FFT for right channel</param>
    /// <param name="isBeat">True if a beat was detected in the current frame</param>
    void Render(SKCanvas canvas, int width, int height, float[] leftSpectrum, float[] rightSpectrum, bool isBeat);
}
