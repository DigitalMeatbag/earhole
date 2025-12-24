using SkiaSharp;

namespace earhole.Modes;

/// <summary>
/// Rainbow-colored vertical spectrum bars visualizer
/// </summary>
public class SpectrumBarsMode : IVisualizerMode
{
    private static readonly SKColor Indigo = SKColor.Parse("#4B0082");
    private static readonly SKColor Violet = SKColor.Parse("#8A2BE2");

    public string Name => "spectrum bars";

    public void Render(SKCanvas canvas, int width, int height, float[] spectrum)
    {
        canvas.Clear(SKColors.Black);

        for (int i = 0; i < spectrum.Length; i++)
        {
            float barHeight = (float)Math.Log(1 + spectrum[i]) * (height / 6f);
            float x = (float)i / spectrum.Length * width;
            var paint = new SKPaint { Color = GetColorForHeight(barHeight, height) };
            canvas.DrawRect(x, height - barHeight, width / (float)spectrum.Length, barHeight, paint);
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
}
