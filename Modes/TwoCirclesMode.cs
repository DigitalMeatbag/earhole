using SkiaSharp;

namespace earhole.Modes;

/// <summary>
/// Two Circles - Dual circular audio visualizer with separate circles for left and right stereo channels
/// </summary>
public class TwoCirclesMode : IVisualizerMode
{
    private class CompressionWave
    {
        public float CenterX { get; set; }
        public float CenterY { get; set; }
        public float CurrentRadius { get; set; }
        public float StartRadius { get; set; }
        public float Age { get; set; }
        public float MaxAge { get; set; }
    }

    private float[] previousRadiiLeft = Array.Empty<float>();
    private float[] currentRadiiLeft = Array.Empty<float>();
    private float[] smoothedVelocitiesLeft = Array.Empty<float>();
    
    private float[] previousRadiiRight = Array.Empty<float>();
    private float[] currentRadiiRight = Array.Empty<float>();
    private float[] smoothedVelocitiesRight = Array.Empty<float>();
    
    private float baseRadius = 0;
    private float targetBaseRadius = 0; // Target for smooth compression/expansion
    private float currentBaseRadius = 0; // Current animated base radius
    private const float MaxGrowth = 150f; // Maximum distance spectrum can grow outward
    private const float VelocitySmoothing = 0.85f; // Higher = more smoothing
    
    // Trail effect using persistent bitmap
    private SKBitmap? trailBitmap;
    private int lastWidth = 0;
    private int lastHeight = 0;
    private const float TrailFadeRate = 0.88f; // Multiply alpha by this each frame (faster fade to prevent ghosting)
    private const byte AlphaCutoff = 10; // Clear pixels below this alpha threshold
    
    // Beat compression effect
    private const float CompressionAmount = 0.7f; // Compress to 70% of normal size
    private const float CompressionSpeed = 0.15f; // How quickly to return to normal
    private readonly List<CompressionWave> waves = new List<CompressionWave>();
    private const float WaveSpeed = 3f; // Pixels per frame
    private const float WaveMaxAge = 60f; // Frames before wave expires

    // Color cycling state
    private float hueOffset = 0f;
    private const float HueCycleSpeed = 0.3f; // Degrees per frame at ~60fps
    
    // Color configuration - dynamically updated each frame
    private SKColor LeftOutwardColor;
    private SKColor LeftInwardColor;
    private SKColor RightOutwardColor;
    private SKColor RightInwardColor;
    
    // Neutral color when not moving (always white)
    private static readonly SKColor NeutralColor = SKColors.White;

    public string Name => "two circles";
    public string Emoji => "♾️";

    public void Render(SKCanvas canvas, int width, int height, float[] leftSpectrum, float[] rightSpectrum, bool isBeat)
    {
        canvas.Clear(SKColors.Black);

        // Initialize or resize trail bitmap if needed
        if (trailBitmap == null || lastWidth != width || lastHeight != height)
        {
            trailBitmap?.Dispose();
            trailBitmap = new SKBitmap(width, height);
            lastWidth = width;
            lastHeight = height;
            
            // Clear the new bitmap
            using var bitmapCanvas = new SKCanvas(trailBitmap);
            bitmapCanvas.Clear(SKColors.Transparent);
        }

        // Update color cycling - each color is offset by 90 degrees to keep them distinct
        hueOffset = (hueOffset + HueCycleSpeed) % 360f;
        LeftOutwardColor = SKColor.FromHsv(hueOffset, 100, 100);
        LeftInwardColor = SKColor.FromHsv((hueOffset + 90) % 360f, 100, 100);
        RightOutwardColor = SKColor.FromHsv((hueOffset + 180) % 360f, 100, 100);
        RightInwardColor = SKColor.FromHsv((hueOffset + 270) % 360f, 100, 100);

        // Calculate base radius - each circle takes up about 1/4 of the smaller dimension
        targetBaseRadius = Math.Min(width, height) * 0.25f;
        
        // Position circles horizontally adjacent
        float leftCenterX = width * 0.25f;
        float rightCenterX = width * 0.75f;
        float centerY = height / 2f;
        
        // Handle beat compression
        if (isBeat)
        {
            // Compress circles
            currentBaseRadius = targetBaseRadius * CompressionAmount;
            
            // Spawn compression waves at both circle centers
            waves.Add(new CompressionWave
            {
                CenterX = leftCenterX,
                CenterY = centerY,
                CurrentRadius = currentBaseRadius,
                StartRadius = currentBaseRadius,
                Age = 0,
                MaxAge = WaveMaxAge
            });
            
            waves.Add(new CompressionWave
            {
                CenterX = rightCenterX,
                CenterY = centerY,
                CurrentRadius = currentBaseRadius,
                StartRadius = currentBaseRadius,
                Age = 0,
                MaxAge = WaveMaxAge
            });
        }
        
        // Smoothly return to normal size
        currentBaseRadius += (targetBaseRadius - currentBaseRadius) * CompressionSpeed;
        baseRadius = currentBaseRadius;

        // Initialize or resize radius tracking arrays
        if (previousRadiiLeft.Length != leftSpectrum.Length)
        {
            previousRadiiLeft = new float[leftSpectrum.Length];
            currentRadiiLeft = new float[leftSpectrum.Length];
            smoothedVelocitiesLeft = new float[leftSpectrum.Length];
            
            Array.Fill(previousRadiiLeft, baseRadius);
            Array.Fill(currentRadiiLeft, baseRadius);
            Array.Fill(smoothedVelocitiesLeft, 0f);
        }
        
        if (previousRadiiRight.Length != rightSpectrum.Length)
        {
            previousRadiiRight = new float[rightSpectrum.Length];
            currentRadiiRight = new float[rightSpectrum.Length];
            smoothedVelocitiesRight = new float[rightSpectrum.Length];
            
            Array.Fill(previousRadiiRight, baseRadius);
            Array.Fill(currentRadiiRight, baseRadius);
            Array.Fill(smoothedVelocitiesRight, 0f);
        }

        // Update radii for both circles
        UpdateRadii(leftSpectrum, ref currentRadiiLeft);
        UpdateRadii(rightSpectrum, ref currentRadiiRight);

        // Fade the trail bitmap
        FadeTrailBitmap();

        // Clear the inner circles and draw current frame to trail bitmap
        using (var trailCanvas = new SKCanvas(trailBitmap))
        {
            // Clear inner circles from trail (preserve center for compression waves)
            using (var clearPaint = new SKPaint
            {
                Color = SKColors.Transparent,
                BlendMode = SKBlendMode.Src,
                IsAntialias = true
            })
            {
                trailCanvas.DrawCircle(leftCenterX, centerY, baseRadius, clearPaint);
                trailCanvas.DrawCircle(rightCenterX, centerY, baseRadius, clearPaint);
            }

            // Draw current frame at full alpha - ensures all bars blend properly with SKBlendMode.Plus
            RenderInterleavedCircles(trailCanvas, leftCenterX, rightCenterX, centerY,
                                    leftSpectrum, rightSpectrum,
                                    ref currentRadiiLeft, ref previousRadiiLeft, ref smoothedVelocitiesLeft,
                                    ref currentRadiiRight, ref previousRadiiRight, ref smoothedVelocitiesRight,
                                    baseRadius,
                                    LeftOutwardColor, LeftInwardColor,
                                    RightOutwardColor, RightInwardColor,
                                    1f); // Full alpha for current frame
        }

        // Draw the trail bitmap to the main canvas
        canvas.DrawBitmap(trailBitmap, 0, 0);

        // Update and render compression waves (drawn on top of trails)
        for (int i = waves.Count - 1; i >= 0; i--)
        {
            var wave = waves[i];
            wave.Age++;
            
            // Move wave inward toward center
            wave.CurrentRadius -= WaveSpeed;
            
            // Remove wave if it's expired or reached the center
            if (wave.Age >= wave.MaxAge || wave.CurrentRadius <= 0)
            {
                waves.RemoveAt(i);
                continue;
            }
            
            // Calculate alpha based on age and distance
            float ageProgress = wave.Age / wave.MaxAge;
            float distanceProgress = 1f - (wave.CurrentRadius / wave.StartRadius);
            float alpha = (1f - ageProgress) * (1f - distanceProgress); // Fade as it ages and approaches center
            
            // Draw the compression wave as a white ring
            using var wavePaint = new SKPaint
            {
                Color = SKColors.White.WithAlpha((byte)(alpha * 200)),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 3f,
                BlendMode = SKBlendMode.Plus
            };
            
            canvas.DrawCircle(wave.CenterX, wave.CenterY, wave.CurrentRadius, wavePaint);
        }

        // Update previous radii for next frame
        Array.Copy(currentRadiiLeft, previousRadiiLeft, leftSpectrum.Length);
        Array.Copy(currentRadiiRight, previousRadiiRight, rightSpectrum.Length);
    }

    private static SKColor InterpolateColor(SKColor from, SKColor to, float t)
    {
        return new SKColor(
            (byte)(from.Red + (to.Red - from.Red) * t),
            (byte)(from.Green + (to.Green - from.Green) * t),
            (byte)(from.Blue + (to.Blue - from.Blue) * t)
        );
    }

    private void FadeTrailBitmap()
    {
        if (trailBitmap == null) return;

        // Create a temporary canvas to apply fade effect
        using var tempSurface = SKSurface.Create(new SKImageInfo(trailBitmap.Width, trailBitmap.Height));
        var tempCanvas = tempSurface.Canvas;
        
        // Draw existing bitmap with reduced alpha
        using var fadePaint = new SKPaint
        {
            Color = SKColors.White.WithAlpha((byte)(255 * TrailFadeRate)),
            BlendMode = SKBlendMode.DstIn // Multiply destination alpha
        };
        
        tempCanvas.Clear(SKColors.Transparent);
        tempCanvas.DrawBitmap(trailBitmap, 0, 0);
        tempCanvas.DrawRect(0, 0, trailBitmap.Width, trailBitmap.Height, fadePaint);
        
        // Copy result back to trail bitmap
        using var snapshot = tempSurface.Snapshot();
        using var snapshotCanvas = new SKCanvas(trailBitmap);
        snapshotCanvas.Clear(SKColors.Transparent);
        snapshotCanvas.DrawImage(snapshot, 0, 0);
        
        // Clear very faint pixels to prevent ghosting
        using var cutoffCanvas = new SKCanvas(trailBitmap);
        using var cutoffPaint = new SKPaint
        {
            Color = SKColors.White.WithAlpha(AlphaCutoff),
            BlendMode = SKBlendMode.DstOut, // Remove pixels below threshold
            IsAntialias = false
        };
        
        // Apply threshold by drawing with DstOut mode
        cutoffCanvas.DrawRect(0, 0, trailBitmap.Width, trailBitmap.Height, cutoffPaint);
    }

    private void UpdateRadii(float[] spectrum, ref float[] currentRadii)
    {
        for (int i = 0; i < spectrum.Length; i++)
        {
            float targetRadius = baseRadius + (spectrum[i] * MaxGrowth);
            currentRadii[i] = currentRadii[i] * 0.7f + targetRadius * 0.3f;
        }
    }

    private void RenderInterleavedCircles(SKCanvas canvas, float leftCenterX, float rightCenterX, float centerY,
                                         float[] leftSpectrum, float[] rightSpectrum,
                                         ref float[] currentRadiiLeft, ref float[] previousRadiiLeft, ref float[] smoothedVelocitiesLeft,
                                         ref float[] currentRadiiRight, ref float[] previousRadiiRight, ref float[] smoothedVelocitiesRight,
                                         float renderBaseRadius,
                                         SKColor leftOutColor, SKColor leftInColor,
                                         SKColor rightOutColor, SKColor rightInColor,
                                         float alphaMultiplier = 1f)
    {
        int length = Math.Max(leftSpectrum.Length, rightSpectrum.Length);
        
        // Interleave drawing segments from both circles
        for (int i = 0; i < length; i++)
        {
            // Draw left circle segment
            if (i < leftSpectrum.Length)
            {
                DrawSegment(canvas, leftCenterX, centerY, i, leftSpectrum.Length,
                           ref currentRadiiLeft, ref previousRadiiLeft, ref smoothedVelocitiesLeft,
                           renderBaseRadius, leftOutColor, leftInColor, alphaMultiplier);
            }
            
            // Draw right circle segment
            if (i < rightSpectrum.Length)
            {
                DrawSegment(canvas, rightCenterX, centerY, i, rightSpectrum.Length,
                           ref currentRadiiRight, ref previousRadiiRight, ref smoothedVelocitiesRight,
                           renderBaseRadius, rightOutColor, rightInColor, alphaMultiplier);
            }
        }
    }

    private void DrawSegment(SKCanvas canvas, float centerX, float centerY, int i, int spectrumLength,
                            ref float[] currentRadii, ref float[] previousRadii, ref float[] smoothedVelocities,
                            float renderBaseRadius, SKColor leftOutColor, SKColor leftInColor, float alphaMultiplier)
    {
        // Calculate instantaneous velocity (rate of change)
        float instantVelocity = currentRadii[i] - previousRadii[i];
        
        // Smooth the velocity over time for more stable color representation
        smoothedVelocities[i] = smoothedVelocities[i] * VelocitySmoothing + instantVelocity * (1f - VelocitySmoothing);
        
        // Amplify and normalize velocity to color range
        float normalizedVelocity = Math.Clamp(smoothedVelocities[i] * 2f, -1f, 1f);
        
        // Calculate color based on smoothed velocity - interpolate from white to target color
        SKColor color;
        if (normalizedVelocity > 0.05f)
        {
            // Moving outward
            color = InterpolateColor(NeutralColor, leftOutColor, normalizedVelocity);
        }
        else if (normalizedVelocity < -0.05f)
        {
            // Moving inward
            color = InterpolateColor(NeutralColor, leftInColor, -normalizedVelocity);
        }
        else
        {
            color = NeutralColor;
        }

        // Calculate angles for this segment
        float anglePerSegment = 2f * MathF.PI / spectrumLength;
        float angle1 = -MathF.PI / 2f + (anglePerSegment * i);
        float angle2 = angle1 + anglePerSegment;
        
        // Calculate inner and outer points for this segment
        float innerRadius = renderBaseRadius;
        float outerRadius = currentRadii[i];
        
        float x1Inner = centerX + innerRadius * MathF.Cos(angle1);
        float y1Inner = centerY + innerRadius * MathF.Sin(angle1);
        float x1Outer = centerX + outerRadius * MathF.Cos(angle1);
        float y1Outer = centerY + outerRadius * MathF.Sin(angle1);
        
        float x2Inner = centerX + innerRadius * MathF.Cos(angle2);
        float y2Inner = centerY + innerRadius * MathF.Sin(angle2);
        float x2Outer = centerX + outerRadius * MathF.Cos(angle2);
        float y2Outer = centerY + outerRadius * MathF.Sin(angle2);

        // Draw segment as a quad
        using (var path = new SKPath())
        {
            path.MoveTo(x1Inner, y1Inner);
            path.LineTo(x1Outer, y1Outer);
            path.LineTo(x2Outer, y2Outer);
            path.LineTo(x2Inner, y2Inner);
            path.Close();

            using (var paint = new SKPaint
            {
                Color = color.WithAlpha((byte)(200 * alphaMultiplier)),
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                BlendMode = SKBlendMode.Plus
            })
            {
                canvas.DrawPath(path, paint);
            }
        }
    }
}
