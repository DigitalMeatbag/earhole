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
    /// Renders the visualizer on the canvas using the provided spectrum data
    /// </summary>
    /// <param name="canvas">The SkiaSharp canvas to draw on</param>
    /// <param name="width">Width of the canvas</param>
    /// <param name="height">Height of the canvas</param>
    /// <param name="leftSpectrum">Array of spectrum magnitudes from FFT for left channel</param>
    /// <param name="rightSpectrum">Array of spectrum magnitudes from FFT for right channel</param>
    void Render(SKCanvas canvas, int width, int height, float[] leftSpectrum, float[] rightSpectrum);
}
