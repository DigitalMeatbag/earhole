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
    private float[] capVels = Array.Empty<float>(); // vertical velocity (pixels/frame)
    private SKColor[] capColors = Array.Empty<SKColor>(); // color for each cap
    private float[] capColorProgress = Array.Empty<float>(); // 0..1 progress where 1 = full color, 0 = white
    private const int CapColorDecayFrames = 12; // increased perceptible decay (doubled)
    private const float CapColorDecayStep = 1f / CapColorDecayFrames;
    private const float CapFallSpeed = 1.5f; // pixels per frame when falling
    private const float CapGravity = 0.25f; // acceleration (pixels per frame^2)
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
            capVels = new float[length];
            capColors = new SKColor[length];
            capColorProgress = new float[length];
            for (int i = 0; i < length; i++)
            {
                capYs[i] = height; // start at bottom (off-screen)
                capAlphas[i] = 0f;
                capHolds[i] = 0;
                capVels[i] = 0f;
                capColors[i] = SKColors.White;
                capColorProgress[i] = 0f;
            }
        }

        for (int i = 0; i < length; i++)
        {
            float mixedValue = (leftSpectrum[i] + rightSpectrum[i]) / 2f;
            float barHeight = (float)Math.Log(1 + mixedValue) * (height / 6f);
            float x = i * barWidth;
            SKColor baseColor = GetColorForHeight(barHeight, height);


            // On beat, bars turn fully white; otherwise use base color
            SKColor barPaintColor = isBeat ? SKColors.White : baseColor;

            // Draw the bar
            paint.Color = barPaintColor;
            canvas.DrawRect(x, height - barHeight, barWidth, barHeight, paint);

            // --- White-cap (peak) handling ---
            float barTopY = height - barHeight;

            // On beat, set cap to bar's color and reset its color-progress to full
            if (isBeat)
            {
                capColors[i] = baseColor;
                capColorProgress[i] = 1f;
                capYs[i] = barTopY;
                capAlphas[i] = 255f;
                capHolds[i] = CapHoldFrames;
                capVels[i] = 0f;
            }
            // If the bar has risen into or above the cap, snap cap to bar top and set full alpha
            else if (barTopY <= capYs[i])
            {
                capYs[i] = barTopY;
                capAlphas[i] = 255f;
                capHolds[i] = CapHoldFrames;
                capVels[i] = 0f; // reset velocity when re-attached to bar
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
                    // Apply gravity to velocity, then move cap by velocity
                    capVels[i] += CapGravity;
                    // Ensure a minimum starting fall speed for responsiveness
                    if (capVels[i] < CapFallSpeed) capVels[i] = Math.Max(capVels[i], CapFallSpeed * 0.25f);
                    capYs[i] += capVels[i];

                    // Fade faster as velocity increases
                    float fadeFactor = 1f + (capVels[i] * 0.12f);
                    capAlphas[i] = Math.Max(0f, capAlphas[i] - (CapFadePerFrame * fadeFactor));

                    // Clamp to bottom
                    if (capYs[i] > height) capYs[i] = height;
                }
            }

            // Decay cap color progress towards white if needed
            if (capColorProgress[i] > 0f && !isBeat)
            {
                capColorProgress[i] = Math.Max(0f, capColorProgress[i] - CapColorDecayStep);
            }

            // If cap still visible, compute display color (blend towards white based on progress) and draw
            if (capAlphas[i] > 0f && capYs[i] < height)
            {
                float progress = Math.Clamp(capColorProgress[i], 0f, 1f);
                SKColor display = progress > 0f ? BlendWithWhite(capColors[i], 1f - progress) : SKColors.White;
                paint.Color = display.WithAlpha((byte)Math.Clamp((int)capAlphas[i], 0, 255));
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
