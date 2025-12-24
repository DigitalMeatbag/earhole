using SkiaSharp;

namespace earhole.Modes;

/// <summary>
/// Orbital particle ring visualizer with beat-reactive pulses
/// </summary>
public class ParticleMode : IVisualizerMode
{
    private class Particle
    {
        public int FrequencyBin;
        public float Angle;
        public float BaseRadius;
        public float AngularVelocity;
        public SKColor Color;
        public float Size;
        public float PulseOffset; // For beat pulse effect
    }

    private readonly List<Particle> particles = new List<Particle>();
    private readonly Random random = new Random();
    private float beatPulse = 0f; // 0 to 1, current beat pulse amount
    private const int MAX_PARTICLES_PER_BIN = 8;
    private const int FREQUENCY_BINS = 32;

    public string Name => "particles";
    public string Emoji => "✨";

    public void Render(SKCanvas canvas, int width, int height, float[] leftSpectrum, float[] rightSpectrum, bool isBeat)
    {
        canvas.Clear(SKColors.Black);

        int centerX = width / 2;
        int centerY = height / 2;
        float maxRadius = Math.Min(width, height) * 0.45f;

        // Mix stereo channels
        int length = Math.Min(leftSpectrum.Length, rightSpectrum.Length);
        float[] spectrum = new float[length];
        for (int i = 0; i < length; i++)
        {
            spectrum[i] = (leftSpectrum[i] + rightSpectrum[i]) / 2f;
        }

        // Trigger beat pulse
        if (isBeat)
        {
            beatPulse = 1.0f;
        }
        
        // Decay beat pulse
        beatPulse *= 0.85f;

        // Update particle count based on audio intensity per frequency bin
        for (int bin = 0; bin < Math.Min(FREQUENCY_BINS, spectrum.Length); bin++)
        {
            float intensity = (float)Math.Log(1 + spectrum[bin]) * 2.0f;
            int desiredCount = (int)(intensity * MAX_PARTICLES_PER_BIN);
            int currentCount = particles.Count(p => p.FrequencyBin == bin);

            // Add particles if needed
            while (currentCount < desiredCount && currentCount < MAX_PARTICLES_PER_BIN)
            {
                SpawnParticle(bin, maxRadius);
                currentCount++;
            }

            // Remove excess particles
            while (currentCount > desiredCount)
            {
                var toRemove = particles.LastOrDefault(p => p.FrequencyBin == bin);
                if (toRemove != null)
                {
                    particles.Remove(toRemove);
                    currentCount--;
                }
            }
        }

        // Update and render particles
        foreach (var particle in particles)
        {
            // Update angle for rotation
            particle.Angle += particle.AngularVelocity;
            if (particle.Angle > Math.PI * 2) particle.Angle -= (float)(Math.PI * 2);

            // Calculate radius with beat pulse
            float pulseAmount = beatPulse * (0.3f + particle.PulseOffset * 0.4f); // Stagger the pulse
            float radius = particle.BaseRadius * (1.0f + pulseAmount);

            // Calculate position
            float x = centerX + (float)Math.Cos(particle.Angle) * radius;
            float y = centerY + (float)Math.Sin(particle.Angle) * radius;

            // Get intensity for this bin
            float intensity = particle.FrequencyBin < spectrum.Length 
                ? (float)Math.Log(1 + spectrum[particle.FrequencyBin]) * 0.5f 
                : 0f;

            // Calculate alpha based on intensity and beat pulse
            float alpha = Math.Clamp(intensity + beatPulse * 0.5f, 0.1f, 1.0f);

            // Render particle with glow
            var paint = new SKPaint
            {
                IsAntialias = true,
                BlendMode = SKBlendMode.Plus // Additive blending for glow
            };

            // Draw outer glow
            paint.Color = particle.Color.WithAlpha((byte)(alpha * 60));
            canvas.DrawCircle(x, y, particle.Size * 2.5f, paint);

            // Draw middle glow
            paint.Color = particle.Color.WithAlpha((byte)(alpha * 120));
            canvas.DrawCircle(x, y, particle.Size * 1.5f, paint);

            // Draw core
            paint.Color = particle.Color.WithAlpha((byte)(alpha * 255));
            canvas.DrawCircle(x, y, particle.Size, paint);
        }

        // Draw subtle ring guides when quiet
        if (beatPulse < 0.1f)
        {
            var guidePaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1,
                Color = SKColors.White.WithAlpha(20),
                IsAntialias = true
            };

            for (int i = 0; i < 8; i++)
            {
                float ringRadius = maxRadius * ((i + 1) / 8f);
                canvas.DrawCircle(centerX, centerY, ringRadius, guidePaint);
            }
        }
    }

    private void SpawnParticle(int bin, float maxRadius)
    {
        // Map frequency bin to radius (low frequencies = inner rings, high = outer rings)
        float normalizedBin = bin / (float)FREQUENCY_BINS;
        float baseRadius = maxRadius * (0.2f + normalizedBin * 0.8f);

        // Color based on frequency bin (rainbow mapping)
        SKColor color = GetColorForBin(bin);

        // Random starting angle
        float angle = (float)(random.NextDouble() * Math.PI * 2);

        // Angular velocity varies by ring (inner faster, outer slower for visual interest)
        float angularVelocity = (0.02f + (1.0f - normalizedBin) * 0.03f) * (random.Next(2) == 0 ? 1 : -1);

        var particle = new Particle
        {
            FrequencyBin = bin,
            Angle = angle,
            BaseRadius = baseRadius,
            AngularVelocity = angularVelocity,
            Color = color,
            Size = 3f + (float)random.NextDouble() * 2f,
            PulseOffset = (float)random.NextDouble() // Stagger beat pulse timing
        };

        particles.Add(particle);
    }

    private SKColor GetColorForBin(int bin)
    {
        // Rainbow gradient across frequency spectrum
        float hue = (bin / (float)FREQUENCY_BINS) * 360f;
        return SKColor.FromHsv(hue, 100, 100);
    }
}
