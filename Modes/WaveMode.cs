using SkiaSharp;

namespace earhole.Modes;

/// <summary>
/// Simple waveform visualizer displaying the audio spectrum as a white line
/// </summary>
public class WaveMode : IVisualizerMode
{
    private class BeatParticle
    {
        public SKPoint Position;
        public SKColor Color;
        public float Lifetime;
        public float MaxLifetime;
        public float Size;
        public float Alpha => Math.Max(0, Lifetime / MaxLifetime);
    }

    private readonly List<BeatParticle> particles = new List<BeatParticle>();
    private readonly Random random = new Random();

    public string Name => "the wave";    public string Emoji => "🌊";
    public void Render(SKCanvas canvas, int width, int height, float[] leftSpectrum, float[] rightSpectrum, bool isBeat)
    {
        canvas.Clear(SKColors.Black);

        // Spawn particles on beat
        if (isBeat)
        {
            int particleCount = random.Next(10, 30); // Random number between 10-30
            for (int i = 0; i < particleCount; i++)
            {
                particles.Add(new BeatParticle
                {
                    Position = new SKPoint(random.Next(0, width), random.Next(0, height)),
                    Color = GetRandomColor(),
                    Lifetime = 0.5f, // 0.5 seconds
                    MaxLifetime = 0.5f,
                    Size = random.Next(5, 15)
                });
            }
        }

        // Update and render particles with glow effect
        for (int i = particles.Count - 1; i >= 0; i--)
        {
            var particle = particles[i];
            particle.Lifetime -= 0.016f; // ~60fps

            if (particle.Lifetime <= 0)
            {
                particles.RemoveAt(i);
                continue;
            }

            // Draw glow layers (outer to inner)
            for (int layer = 3; layer >= 1; layer--)
            {
                float glowSize = particle.Size * (1 + layer * 0.3f);
                byte glowAlpha = (byte)(particle.Alpha * 50 / layer);
                
                using var glowPaint = new SKPaint
                {
                    Color = particle.Color.WithAlpha(glowAlpha),
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill
                };
                canvas.DrawCircle(particle.Position, glowSize, glowPaint);
            }

            // Draw core particle
            using var particlePaint = new SKPaint
            {
                Color = particle.Color.WithAlpha((byte)(particle.Alpha * 255)),
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawCircle(particle.Position, particle.Size, particlePaint);
        }

        int length = Math.Min(leftSpectrum.Length, rightSpectrum.Length);
        if (length < 2) return;

        // Colors: red for right, blue for left, white on beat
        var rightColor = isBeat ? SKColors.White : SKColors.Red;
        var leftColor = isBeat ? SKColors.White : SKColors.Blue;
        var strokeWidth = isBeat ? 3f : 2f; // Slightly thicker on beat

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

        // Draw both waveforms with separate colors
        using (var rightPaint = new SKPaint
        {
            Color = rightColor,
            StrokeWidth = strokeWidth,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        })
        {
            canvas.DrawPath(rightPath, rightPaint);
        }

        using (var leftPaint = new SKPaint
        {
            Color = leftColor,
            StrokeWidth = strokeWidth,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        })
        {
            canvas.DrawPath(leftPath, leftPaint);
        }
    }

    private SKColor GetRandomColor()
    {
        var colors = new[]
        {
            SKColors.Cyan,
            SKColors.Magenta,
            SKColors.Yellow,
            SKColors.LimeGreen,
            SKColors.Orange,
            SKColors.Purple,
            SKColors.Pink,
            SKColors.DeepSkyBlue
        };
        return colors[random.Next(colors.Length)];
    }
}
