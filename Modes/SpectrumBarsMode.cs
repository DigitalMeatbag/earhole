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
    private readonly SKPaint paint = new SKPaint { IsAntialias = true };

    public string Name => "spectrum bars";
    public string Emoji => "📊";

    public void Render(SKCanvas canvas, int width, int height, float[] leftSpectrum, float[] rightSpectrum, bool isBeat)
    {
        canvas.Clear(SKColors.Black);

        // Mix stereo channels for visualization
        int length = Math.Min(leftSpectrum.Length, rightSpectrum.Length);
        float barWidth = width / (float)length;
        
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

    private static SKColor GetColorForHeight(float barHeight, float maxCanvasHeight)
    {
        float maxHeight = maxCanvasHeight;
        float clamped = Math.Min(barHeight, maxHeight);
        int bin = (int)(clamped / (maxHeight / 7));
        switch (bin)
        {
            case 0: return SKColors.Red;
            case 1: return SKColors.Orange;
            case 2: return SKColors.Yellow;
            case 3: return SKColors.Green;
            case 4: return SKColors.Blue;
            case 5: return Indigo;
            case 6: return Violet;
            default: return Violet;
        }
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
