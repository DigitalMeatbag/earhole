using SkiaSharp;

namespace earhole.Modes;

/// <summary>
/// Two Circles - Dual circular audio visualizer with separate circles for left and right stereo channels
/// </summary>
public class TwoCirclesMode : IVisualizerMode
{
    private float[] previousRadiiLeft = Array.Empty<float>();
    private float[] currentRadiiLeft = Array.Empty<float>();
    private float[] smoothedVelocitiesLeft = Array.Empty<float>();
    
    private float[] previousRadiiRight = Array.Empty<float>();
    private float[] currentRadiiRight = Array.Empty<float>();
    private float[] smoothedVelocitiesRight = Array.Empty<float>();
    
    private float baseRadius = 0;
    private const float MaxGrowth = 150f; // Maximum distance spectrum can grow outward
    private const float VelocitySmoothing = 0.85f; // Higher = more smoothing

    public string Name => "two circles";

    public void Render(SKCanvas canvas, int width, int height, float[] leftSpectrum, float[] rightSpectrum, bool isBeat)
    {
        canvas.Clear(SKColors.Black);

        // Calculate base radius - each circle takes up about 1/4 of the smaller dimension
        baseRadius = Math.Min(width, height) * 0.25f;
        
        // Position circles horizontally adjacent
        // Left circle at 1/4 of width, right circle at 3/4 of width
        float leftCenterX = width * 0.25f;
        float rightCenterX = width * 0.75f;
        float centerY = height / 2f;

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

        // Render both circles with interleaved segments for better blending
        RenderInterleavedCircles(canvas, leftCenterX, rightCenterX, centerY, 
                                leftSpectrum, rightSpectrum,
                                ref currentRadiiLeft, ref previousRadiiLeft, ref smoothedVelocitiesLeft,
                                ref currentRadiiRight, ref previousRadiiRight, ref smoothedVelocitiesRight);

        // Update previous radii for next frame
        Array.Copy(currentRadiiLeft, previousRadiiLeft, leftSpectrum.Length);
        Array.Copy(currentRadiiRight, previousRadiiRight, rightSpectrum.Length);
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
                                         ref float[] currentRadiiRight, ref float[] previousRadiiRight, ref float[] smoothedVelocitiesRight)
    {
        int length = Math.Max(leftSpectrum.Length, rightSpectrum.Length);
        
        // Interleave drawing segments from both circles
        for (int i = 0; i < length; i++)
        {
            // Draw left circle segment
            if (i < leftSpectrum.Length)
            {
                DrawSegment(canvas, leftCenterX, centerY, i, leftSpectrum.Length,
                           ref currentRadiiLeft, ref previousRadiiLeft, ref smoothedVelocitiesLeft, true);
            }
            
            // Draw right circle segment
            if (i < rightSpectrum.Length)
            {
                DrawSegment(canvas, rightCenterX, centerY, i, rightSpectrum.Length,
                           ref currentRadiiRight, ref previousRadiiRight, ref smoothedVelocitiesRight, false);
            }
        }
    }

    private void DrawSegment(SKCanvas canvas, float centerX, float centerY, int i, int spectrumLength,
                            ref float[] currentRadii, ref float[] previousRadii, ref float[] smoothedVelocities, bool isLeftCircle)
    {
        // Calculate instantaneous velocity (rate of change)
        float instantVelocity = currentRadii[i] - previousRadii[i];
        
        // Smooth the velocity over time for more stable color representation
        smoothedVelocities[i] = smoothedVelocities[i] * VelocitySmoothing + instantVelocity * (1f - VelocitySmoothing);
        
        // Amplify and normalize velocity to color range
        float normalizedVelocity = Math.Clamp(smoothedVelocities[i] * 2f, -1f, 1f);
        
        // Calculate color based on smoothed velocity with different color schemes per circle
        SKColor color;
        if (normalizedVelocity > 0.05f)
        {
            // Moving outward
            byte component = (byte)(255 * (1f - normalizedVelocity));
            if (isLeftCircle)
            {
                // Left circle: interpolate from white to red
                color = new SKColor(255, component, component);
            }
            else
            {
                // Right circle: interpolate from white to green
                color = new SKColor(component, 255, component);
            }
        }
        else if (normalizedVelocity < -0.05f)
        {
            // Moving inward
            byte component = (byte)(255 * (1f + normalizedVelocity));
            if (isLeftCircle)
            {
                // Left circle: interpolate from white to blue
                color = new SKColor(component, component, 255);
            }
            else
            {
                // Right circle: interpolate from white to orange
                color = new SKColor(255, (byte)(component * 0.65f), component);
            }
        }
        else
        {
            color = SKColors.White;
        }

        // Calculate angles for this segment
        float angle1 = -MathF.PI / 2f + (2f * MathF.PI * i / spectrumLength);
        float angle2 = -MathF.PI / 2f + (2f * MathF.PI * (i + 1) / spectrumLength);
        
        // Calculate inner and outer points for this segment
        float innerRadius = baseRadius;
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
                Color = color.WithAlpha(200),
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
