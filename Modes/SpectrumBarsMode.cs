using SkiaSharp;

namespace earhole.Modes;

/// <summary>
/// Rainbow-colored vertical spectrum bars visualizer
/// </summary>
public class SpectrumBarsMode : IVisualizerMode
{
    private static readonly SKColor Indigo = SKColor.Parse("#4B0082");
    private static readonly SKColor Violet = SKColor.Parse("#8A2BE2");
    
    // Cached paint object for reuse
    private readonly SKPaint paint = new() { IsAntialias = true };
    
    // Color cycling state (mirrors approach used in TwoCirclesMode)
    private float hueOffset = 0f;
    private const float HueCycleSpeed = 0.3f;
    private int framesSinceColorUpdate = 0;
    private const int ColorUpdateInterval = 2; // Update colors every N frames
    private const int BinCount = 7; // number of discrete height bins
    private SKColor[] colorMap = new SKColor[BinCount];

    public string Name => "spectrum bars";
    public string Emoji => "📊";

    public void Render(SKCanvas canvas, int width, int height, float[] leftSpectrum, float[] rightSpectrum, bool isBeat)
    {
        canvas.Clear(SKColors.Black);

        // Mix stereo channels for visualization
        int length = Math.Min(leftSpectrum.Length, rightSpectrum.Length);
        float barWidth = width / (float)length;
        
        // Update color map periodically (and initialize on first render)
        framesSinceColorUpdate++;
        if (colorMap[0].Alpha == 0 || framesSinceColorUpdate >= ColorUpdateInterval)
        {
            framesSinceColorUpdate = 0;
            hueOffset = (hueOffset + (HueCycleSpeed * ColorUpdateInterval)) % 360f;
            for (int b = 0; b < BinCount; b++)
            {
                float hue = (hueOffset + (b * (360f / BinCount))) % 360f;
                colorMap[b] = SKColor.FromHsv(hue, 100, 100);
            }
        }

        for (int i = 0; i < length; i++)
        {
            float mixedValue = (leftSpectrum[i] + rightSpectrum[i]) / 2f;
            float barHeight = (float)Math.Log(1 + mixedValue) * (height / 6f);
            float x = i * barWidth;
            SKColor baseColor = GetColorForHeight(barHeight, height);
            
            // On beat, blend color 50% towards white for pulse effect
            SKColor color = isBeat ? BlendWithWhite(baseColor, 0.5f) : baseColor;
            
            paint.Color = color;
            canvas.DrawRect(x, height - barHeight, barWidth, barHeight, paint);
        }
    }

    private SKColor GetColorForHeight(float barHeight, float maxCanvasHeight)
    {
        float maxHeight = maxCanvasHeight;
        float clamped = Math.Min(barHeight, maxHeight);
        int bin = (int)(clamped / (maxHeight / BinCount));
        if (bin < 0) bin = 0;
        if (bin >= BinCount) bin = BinCount - 1;
        return (bin >= 0 && bin < colorMap.Length) ? colorMap[bin] : SKColors.White;
    }

    /// <summary>
    /// Blends a color towards white by the specified amount (0 = original color, 1 = white)
    /// </summary>
    private static SKColor BlendWithWhite(SKColor color, float amount)
    {
        byte r = (byte)(color.Red + (255 - color.Red) * amount);
        byte g = (byte)(color.Green + (255 - color.Green) * amount);
        byte b = (byte)(color.Blue + (255 - color.Blue) * amount);
        return new SKColor(r, g, b, color.Alpha);
    }
}
