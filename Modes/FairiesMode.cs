using SkiaSharp;

namespace earhole.Modes;

/// <summary>
/// Fairies - Seven glowing fairies representing different frequency ranges
/// </summary>
public class FairiesMode : IVisualizerMode
{
    private class Fairy
    {
        public SKPoint Position;
        public SKPoint Velocity;
        public SKPoint Acceleration;
        public SKColor Color;
        public float GlowIntensity;
        public float WanderAngle;
        public float FlutterPhase;
    }

    private readonly List<Fairy> fairies = new List<Fairy>();
    private readonly Random random = new Random();
    private int width, height;
    private bool initialized = false;

    // ROYGBIV colors
    private readonly SKColor[] fairyColors = new[]
    {
        new SKColor(255, 0, 0),     // Red
        new SKColor(255, 127, 0),   // Orange
        new SKColor(255, 255, 0),   // Yellow
        new SKColor(0, 255, 0),     // Green
        new SKColor(0, 0, 255),     // Blue
        new SKColor(75, 0, 130),    // Indigo
        new SKColor(148, 0, 211)    // Violet
    };

    public string Name => "fairies";

    public void Render(SKCanvas canvas, int width, int height, float[] spectrum)
    {
        canvas.Clear(SKColors.Black);

        // Initialize fairies on first render
        if (!initialized || this.width != width || this.height != height)
        {
            InitializeFairies(width, height);
            this.width = width;
            this.height = height;
            initialized = true;
        }

        // Divide spectrum into 7 frequency ranges
        int rangeSize = spectrum.Length / 7;
        
        // Update each fairy's glow based on its frequency range
        for (int i = 0; i < fairies.Count; i++)
        {
            var fairy = fairies[i];
            
            // Calculate average intensity for this fairy's frequency range
            int startIdx = i * rangeSize;
            int endIdx = (i == 6) ? spectrum.Length : (i + 1) * rangeSize; // Last fairy gets remainder
            float avgIntensity = 0f;
            for (int j = startIdx; j < endIdx; j++)
            {
                avgIntensity += spectrum[j];
            }
            avgIntensity /= (endIdx - startIdx);
            
            // Smooth the glow intensity
            fairy.GlowIntensity = fairy.GlowIntensity * 0.8f + avgIntensity * 0.2f;
            
            // Update fairy movement (organic flying motion)
            UpdateFairyMovement(fairy, width, height);
            
            // Render the fairy
            DrawFairy(canvas, fairy);
        }
    }

    private void InitializeFairies(int width, int height)
    {
        fairies.Clear();
        
        for (int i = 0; i < 7; i++)
        {
            var fairy = new Fairy
            {
                Position = new SKPoint(
                    (float)random.NextDouble() * width,
                    (float)random.NextDouble() * height
                ),
                Velocity = new SKPoint(
                    (float)(random.NextDouble() - 0.5) * 2f,
                    (float)(random.NextDouble() - 0.5) * 2f
                ),
                Acceleration = new SKPoint(0, 0),
                Color = fairyColors[i],
                GlowIntensity = 0f,
                WanderAngle = (float)(random.NextDouble() * Math.PI * 2),
                FlutterPhase = (float)(random.NextDouble() * Math.PI * 2)
            };
            
            fairies.Add(fairy);
        }
    }

    private void UpdateFairyMovement(Fairy fairy, int width, int height)
    {
        // Organic wandering behavior
        // Update wander angle with small random changes
        fairy.WanderAngle += (float)(random.NextDouble() - 0.5) * 0.3f;
        
        // Calculate wander force
        float wanderX = (float)Math.Cos(fairy.WanderAngle) * 0.15f;
        float wanderY = (float)Math.Sin(fairy.WanderAngle) * 0.15f;
        
        // Add flutter effect (more erratic, flying-creature-like motion)
        fairy.FlutterPhase += 0.2f;
        float flutterX = (float)Math.Sin(fairy.FlutterPhase) * 0.1f;
        float flutterY = (float)Math.Cos(fairy.FlutterPhase * 1.3f) * 0.1f;
        
        // Boundary avoidance (soft repulsion from edges)
        float margin = 50f;
        float avoidX = 0f, avoidY = 0f;
        
        if (fairy.Position.X < margin)
            avoidX = (margin - fairy.Position.X) * 0.01f;
        else if (fairy.Position.X > width - margin)
            avoidX = (width - margin - fairy.Position.X) * 0.01f;
            
        if (fairy.Position.Y < margin)
            avoidY = (margin - fairy.Position.Y) * 0.01f;
        else if (fairy.Position.Y > height - margin)
            avoidY = (height - margin - fairy.Position.Y) * 0.01f;
        
        // Combine forces
        fairy.Acceleration = new SKPoint(
            wanderX + flutterX + avoidX,
            wanderY + flutterY + avoidY
        );
        
        // Update velocity
        fairy.Velocity = new SKPoint(
            fairy.Velocity.X + fairy.Acceleration.X,
            fairy.Velocity.Y + fairy.Acceleration.Y
        );
        
        // Limit speed
        float speed = (float)Math.Sqrt(fairy.Velocity.X * fairy.Velocity.X + fairy.Velocity.Y * fairy.Velocity.Y);
        float maxSpeed = 3f;
        if (speed > maxSpeed)
        {
            fairy.Velocity = new SKPoint(
                fairy.Velocity.X / speed * maxSpeed,
                fairy.Velocity.Y / speed * maxSpeed
            );
        }
        
        // Update position
        fairy.Position = new SKPoint(
            fairy.Position.X + fairy.Velocity.X,
            fairy.Position.Y + fairy.Velocity.Y
        );
        
        // Wrap around edges (as backup)
        if (fairy.Position.X < 0) fairy.Position.X = width;
        if (fairy.Position.X > width) fairy.Position.X = 0;
        if (fairy.Position.Y < 0) fairy.Position.Y = height;
        if (fairy.Position.Y > height) fairy.Position.Y = 0;
    }

    private void DrawFairy(SKCanvas canvas, Fairy fairy)
    {
        // Calculate glow size based on intensity (minimum glow even when quiet)
        float baseSize = 8f;
        float glowSize = baseSize + (fairy.GlowIntensity * 30f);
        
        // Draw multiple circles with decreasing alpha for glow effect
        int glowLayers = 5;
        for (int i = glowLayers; i > 0; i--)
        {
            float layerSize = glowSize * (i / (float)glowLayers);
            byte alpha = (byte)(100 * (1f - i / (float)(glowLayers + 1)));
            
            using (var paint = new SKPaint
            {
                Color = fairy.Color.WithAlpha(alpha),
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                BlendMode = SKBlendMode.Plus // Additive blending for glow
            })
            {
                canvas.DrawCircle(fairy.Position, layerSize, paint);
            }
        }
        
        // Draw bright core
        using (var corePaint = new SKPaint
        {
            Color = fairy.Color.WithAlpha(255),
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            BlendMode = SKBlendMode.Plus
        })
        {
            canvas.DrawCircle(fairy.Position, baseSize * 0.4f, corePaint);
        }
    }
}
