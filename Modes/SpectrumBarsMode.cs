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
    
    // White-cap (peak) state per bar
    private float[] capYs = Array.Empty<float>(); // Y position of cap (pixels from top)
    private float[] capAlphas = Array.Empty<float>(); // alpha (0-255)
    private int[] capHolds = Array.Empty<int>(); // frames to hold full opacity before falling
    private const float CapFallSpeed = 1.5f; // pixels per frame when falling
    private const float CapFadePerFrame = 4f; // alpha decrease per frame
    private const int CapHoldFrames = 1; // number of frames to hold at full opacity
    private const float CapThickness = 4f; // visual thickness of the cap in pixels

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

        // Ensure cap arrays match spectrum length
        if (capYs.Length != length)
        {
            capYs = new float[length];
            capAlphas = new float[length];
            capHolds = new int[length];
            for (int i = 0; i < length; i++)
            {
                capYs[i] = height; // start at bottom (off-screen)
                capAlphas[i] = 0f;
                capHolds[i] = 0;
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

            // Draw the bar
            paint.Color = color;
            canvas.DrawRect(x, height - barHeight, barWidth, barHeight, paint);

            // --- White-cap (peak) handling ---
            float barTopY = height - barHeight;

            // If the bar has risen into or above the cap, snap cap to bar top and set full alpha
            if (barTopY <= capYs[i])
            {
                capYs[i] = barTopY;
                capAlphas[i] = 255f;
                capHolds[i] = CapHoldFrames;
            }
            else
            {
                // If we're still in the hold period, just decrement the counter
                if (capHolds[i] > 0)
                {
                    capHolds[i]--;
                }
                else
                {
                    // Start falling and fading
                    capYs[i] += CapFallSpeed;
                    capAlphas[i] = Math.Max(0f, capAlphas[i] - CapFadePerFrame);
                    // Clamp to bottom
                    if (capYs[i] > height) capYs[i] = height;
                }
            }

            // If cap still visible, draw it as a white rectangle with current alpha
            if (capAlphas[i] > 0f && capYs[i] < height)
            {
                paint.Color = SKColors.White.WithAlpha((byte)Math.Clamp((int)capAlphas[i], 0, 255));
                canvas.DrawRect(x, capYs[i] - CapThickness, barWidth, CapThickness, paint);
            }
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
