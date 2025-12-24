using SkiaSharp;

namespace earhole.Modes;

/// <summary>
/// Simple waveform visualizer displaying the audio spectrum as a white line
/// </summary>
public class WaveMode : IVisualizerMode
{
    public string Name => "the wave";

    public void Render(SKCanvas canvas, int width, int height, float[] leftSpectrum, float[] rightSpectrum)
    {
        canvas.Clear(SKColors.Black);

        int length = Math.Min(leftSpectrum.Length, rightSpectrum.Length);
        if (length < 2) return;

        using var paint = new SKPaint
        {
            Color = SKColors.White,
            StrokeWidth = 2,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        // Calculate center line and maximum allowed amplitude
        float centerY = height / 2f;
        float maxAmplitude = height / 2f;
        
        // Create path for right channel (goes upward)
        using var rightPath = new SKPath();
        float firstRightValue = rightSpectrum[0];
        float firstRightAmplitude = Math.Min((float)Math.Log(1 + firstRightValue) * (height / 8f), maxAmplitude);
        rightPath.MoveTo(0, centerY - firstRightAmplitude);

        for (int i = 1; i < length; i++)
        {
            float amplitude = Math.Min((float)Math.Log(1 + rightSpectrum[i]) * (height / 8f), maxAmplitude);
            float x = (float)i / length * width;
            float y = centerY - amplitude;
            rightPath.LineTo(x, y);
        }

        // Create path for left channel (goes downward)
        using var leftPath = new SKPath();
        float firstLeftValue = leftSpectrum[0];
        float firstLeftAmplitude = Math.Min((float)Math.Log(1 + firstLeftValue) * (height / 8f), maxAmplitude);
        leftPath.MoveTo(0, centerY + firstLeftAmplitude);

        for (int i = 1; i < length; i++)
        {
            float amplitude = Math.Min((float)Math.Log(1 + leftSpectrum[i]) * (height / 8f), maxAmplitude);
            float x = (float)i / length * width;
            float y = centerY + amplitude;
            leftPath.LineTo(x, y);
        }

        // Draw both waveforms
        canvas.DrawPath(rightPath, paint);
        canvas.DrawPath(leftPath, paint);
    }
}
