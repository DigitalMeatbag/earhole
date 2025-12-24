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

    public void Render(SKCanvas canvas, int width, int height, float[] leftSpectrum, float[] rightSpectrum)
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

        // Render left circle with left channel spectrum
        RenderCircle(canvas, leftCenterX, centerY, leftSpectrum, 
                    ref currentRadiiLeft, ref previousRadiiLeft, ref smoothedVelocitiesLeft);

        // Render right circle with right channel spectrum
        RenderCircle(canvas, rightCenterX, centerY, rightSpectrum,
                    ref currentRadiiRight, ref previousRadiiRight, ref smoothedVelocitiesRight);

        // Update previous radii for next frame
        Array.Copy(currentRadiiLeft, previousRadiiLeft, leftSpectrum.Length);
        Array.Copy(currentRadiiRight, previousRadiiRight, rightSpectrum.Length);
    }

    private void RenderCircle(SKCanvas canvas, float centerX, float centerY, float[] spectrum,
                             ref float[] currentRadii, ref float[] previousRadii, ref float[] smoothedVelocities)
    {
        // Update current radii based on spectrum
        for (int i = 0; i < spectrum.Length; i++)
        {
            float targetRadius = baseRadius + (spectrum[i] * MaxGrowth);
            
            // Smooth the radius change
            currentRadii[i] = currentRadii[i] * 0.7f + targetRadius * 0.3f;
        }

        // Draw the circle as connected segments
        using (var path = new SKPath())
        {
            bool firstPoint = true;

            for (int i = 0; i <= spectrum.Length; i++)
            {
                // Wrap around to close the circle
                int index = i % spectrum.Length;
                
                // Calculate angle (starting at top, going clockwise)
                // -PI/2 starts at top (12 o'clock), and we go clockwise
                float angle = -MathF.PI / 2f + (2f * MathF.PI * i / spectrum.Length);
                
                // Calculate position
                float radius = currentRadii[index];
                float x = centerX + radius * MathF.Cos(angle);
                float y = centerY + radius * MathF.Sin(angle);

                if (firstPoint)
                {
                    path.MoveTo(x, y);
                    firstPoint = false;
                }
                else
                {
                    path.LineTo(x, y);
                }
            }

            path.Close();

            // Draw the filled circle shape with white outline
            using (var fillPaint = new SKPaint
            {
                Color = SKColors.Black,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            })
            {
                canvas.DrawPath(path, fillPaint);
            }
        }

        // Draw individual colored segments based on velocity
        for (int i = 0; i < spectrum.Length; i++)
        {
            // Calculate instantaneous velocity (rate of change)
            float instantVelocity = currentRadii[i] - previousRadii[i];
            
            // Smooth the velocity over time for more stable color representation
            smoothedVelocities[i] = smoothedVelocities[i] * VelocitySmoothing + instantVelocity * (1f - VelocitySmoothing);
            
            // Amplify and normalize velocity to color range
            // Amplification makes small changes more visible
            float normalizedVelocity = Math.Clamp(smoothedVelocities[i] * 2f, -1f, 1f);
            
            // Calculate color based on smoothed velocity
            SKColor color;
            if (normalizedVelocity > 0.05f) // Small threshold to avoid flickering at rest
            {
                // Moving outward: interpolate from white to red
                byte component = (byte)(255 * (1f - normalizedVelocity));
                color = new SKColor(255, component, component);
            }
            else if (normalizedVelocity < -0.05f)
            {
                // Moving inward: interpolate from white to blue
                byte component = (byte)(255 * (1f + normalizedVelocity));
                color = new SKColor(component, component, 255);
            }
            else
            {
                // No significant change: white
                color = SKColors.White;
            }

            // Calculate angles for this segment
            float angle1 = -MathF.PI / 2f + (2f * MathF.PI * i / spectrum.Length);
            float angle2 = -MathF.PI / 2f + (2f * MathF.PI * (i + 1) / spectrum.Length);
            
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
                    Color = color,
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill
                })
                {
                    canvas.DrawPath(path, paint);
                }
            }
        }
    }
}
