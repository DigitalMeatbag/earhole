using SkiaSharp;

namespace earhole.Modes;

/// <summary>
/// Particle-based audio visualizer with beat-reactive particle spawning
/// </summary>
public class ParticleMode : IVisualizerMode
{
    private class Particle
    {
        public SKPoint Position;
        public SKPoint Velocity;
        public SKColor Color;
        public float Lifetime;
        public float MaxLifetime;
        public float Size;
        public float Alpha => Math.Max(0, Lifetime / MaxLifetime);
    }

    private readonly List<Particle> particles = new List<Particle>();
    private readonly Random random = new Random();
    private int frameCount = 0;

    public string Name => "Particles";

    public void Render(SKCanvas canvas, int width, int height, float[] spectrum)
    {
        canvas.Clear(SKColors.Black);

        // Calculate average intensity and peak
        float avgIntensity = spectrum.Take(64).Average();
        float peakIntensity = spectrum.Take(64).Max();

        // Spawn particles based on audio intensity (reduced to half)
        int particlesToSpawn = (int)(peakIntensity * 2.5);
        for (int i = 0; i < particlesToSpawn; i++)
        {
            SpawnParticle(width, height, spectrum);
        }

        // Update and render particles
        for (int i = particles.Count - 1; i >= 0; i--)
        {
            var particle = particles[i];
            
            // Update lifetime
            particle.Lifetime -= 0.016f; // ~60fps
            
            if (particle.Lifetime <= 0)
            {
                particles.RemoveAt(i);
                continue;
            }

            // Update position
            particle.Position.X += particle.Velocity.X;
            particle.Position.Y += particle.Velocity.Y;
            
            // Apply gravity
            particle.Velocity.Y += 0.2f;

            // Render particle
            var paint = new SKPaint
            {
                Color = particle.Color.WithAlpha((byte)(particle.Alpha * 255)),
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                BlendMode = SKBlendMode.Plus // Additive blending for glow effect
            };

            canvas.DrawCircle(particle.Position, particle.Size, paint);
        }

        // Display particle count (optional debug info)
        frameCount++;
    }

    private void SpawnParticle(int width, int height, float[] spectrum)
    {
        // Pick a frequency bin to determine color
        int bin = random.Next(Math.Min(spectrum.Length, 128));
        float intensity = spectrum[bin];

        // Color based on frequency (low = red, mid = green, high = blue)
        SKColor color;
        if (bin < 32)
            color = new SKColor(255, (byte)(bin * 8), 0); // Red to orange
        else if (bin < 64)
            color = new SKColor((byte)(255 - (bin - 32) * 8), 255, 0); // Orange to green
        else
            color = new SKColor(0, (byte)(255 - (bin - 64) * 4), 255); // Green to blue

        // Spawn across full width - distribute evenly based on bin
        float spreadX = (bin / 128f) * width;
        
        var particle = new Particle
        {
            Position = new SKPoint(spreadX, height),
            Velocity = new SKPoint(
                (float)(random.NextDouble() - 0.5) * 5,
                -(float)(random.NextDouble() * 10 + 5 + intensity)
            ),
            Color = color,
            Lifetime = (float)(random.NextDouble() * 0.5 + 0.25),
            MaxLifetime = (float)(random.NextDouble() * 0.5 + 0.25),
            Size = (float)(random.NextDouble() * 4 + 2)
        };

        particles.Add(particle);
    }
}
