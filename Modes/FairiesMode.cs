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
        public float PreviousGlowIntensity;
        public float WanderAngle;
        public float FlutterPhase;
    }

    private readonly List<Fairy> fairies = new();
    private readonly Random random = new();
    private int width, height;
    private bool initialized = false;
    private float scatterTimer = 0f;
    private const float scatterDuration = 1f; // 1 second scatter time

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
    
    // Cached paint objects for performance
    private readonly SKPaint glowPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill,
        BlendMode = SKBlendMode.Plus
    };
    
    private readonly SKPaint corePaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill,
        BlendMode = SKBlendMode.Plus
    };

    public string Name => "fairies";
    public string Emoji => "🧚";

    public void Render(SKCanvas canvas, int width, int height, float[] leftSpectrum, float[] rightSpectrum, bool isBeat)
    {
        canvas.Clear(SKColors.Black);

        // Mix stereo channels for visualization
        int length = Math.Min(leftSpectrum.Length, rightSpectrum.Length);
        float[] spectrum = new float[length];
        for (int i = 0; i < length; i++)
        {
            spectrum[i] = (leftSpectrum[i] + rightSpectrum[i]) / 2f;
        }

        // Initialize fairies on first render
        if (!initialized || this.width != width || this.height != height)
        {
            InitializeFairies(width, height);
            this.width = width;
            this.height = height;
            initialized = true;
        }

        // Handle beat detection - trigger scatter
        if (isBeat && scatterTimer <= 0f)
        {
            scatterTimer = scatterDuration;
            
            // Apply immediate scatter impulse to each fairy
            foreach (var fairy in fairies)
            {
                float centerX = width / 2f;
                float centerY = height / 2f;
                float dx = fairy.Position.X - centerX;
                float dy = fairy.Position.Y - centerY;
                
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                if (dist > 0.1f)
                {
                    // Add impulse away from center
                    fairy.Velocity = new SKPoint(
                        fairy.Velocity.X + (dx / dist) * 8f,
                        fairy.Velocity.Y + (dy / dist) * 8f
                    );
                }
                else
                {
                    // Random direction if at center
                    float angle = (float)(random.NextDouble() * Math.PI * 2);
                    fairy.Velocity = new SKPoint(
                        fairy.Velocity.X + (float)Math.Cos(angle) * 8f,
                        fairy.Velocity.Y + (float)Math.Sin(angle) * 8f
                    );
                }
            }
        }

        // Decay scatter timer
        if (scatterTimer > 0f)
        {
            scatterTimer -= 0.016f; // Approximate frame time (60fps)
            if (scatterTimer < 0f)
                scatterTimer = 0f;
        }

        // Find active frequency ranges in the spectrum
        float activityThreshold = 0.05f; // Minimum intensity to be considered "active"
        
        // Find the lowest and highest active frequencies
        int lowestActive = -1;
        int highestActive = -1;
        
        for (int i = 0; i < spectrum.Length; i++)
        {
            if (spectrum[i] > activityThreshold)
            {
                if (lowestActive == -1)
                    lowestActive = i;
                highestActive = i;
            }
        }
        
        // If no activity, use minimal range at the low end
        if (lowestActive == -1)
        {
            lowestActive = 0;
            highestActive = Math.Min(spectrum.Length - 1, 70); // Default to low frequency range
        }
        
        // Ensure we have a reasonable range (at least 7 bins)
        int activeRange = highestActive - lowestActive + 1;
        if (activeRange < 7)
        {
            // Expand range to ensure we have enough for 7 fairies
            int expansion = (7 - activeRange) / 2;
            lowestActive = Math.Max(0, lowestActive - expansion);
            highestActive = Math.Min(spectrum.Length - 1, highestActive + expansion + (7 - activeRange) % 2);
        }
        
        // Divide the active range into 7 sections for the fairies
        // Fairies maintain order: red (lowest), orange, yellow, green, blue, indigo, violet (highest)
        float sectionSize = (highestActive - lowestActive + 1) / 7f;
        
        // Update each fairy's glow based on its dynamically assigned frequency range
        for (int i = 0; i < fairies.Count; i++)
        {
            var fairy = fairies[i];
            
            // Calculate this fairy's frequency range within the active spectrum
            int startIdx = lowestActive + (int)(i * sectionSize);
            int endIdx = (i == 6) ? highestActive + 1 : lowestActive + (int)((i + 1) * sectionSize);
            
            // Ensure valid range
            startIdx = Math.Max(0, Math.Min(spectrum.Length - 1, startIdx));
            endIdx = Math.Max(startIdx + 1, Math.Min(spectrum.Length, endIdx));
            
            // Calculate average intensity for this fairy's dynamic frequency range
            float avgIntensity = 0f;
            int count = 0;
            for (int j = startIdx; j < endIdx; j++)
            {
                avgIntensity += spectrum[j];
                count++;
            }
            if (count > 0)
                avgIntensity /= count;
            
            // Store previous glow for velocity calculation
            fairy.PreviousGlowIntensity = fairy.GlowIntensity;
            
            // Smooth the glow intensity
            fairy.GlowIntensity = fairy.GlowIntensity * 0.8f + avgIntensity * 0.2f;
            
            // Update fairy movement (organic flying motion)
            UpdateFairyMovement(fairy, width, height, scatterTimer > 0f);
            
            // Render the fairy
            DrawFairy(canvas, fairy, width, height);
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
                PreviousGlowIntensity = 0f,
                WanderAngle = (float)(random.NextDouble() * Math.PI * 2),
                FlutterPhase = (float)(random.NextDouble() * Math.PI * 2)
            };
            
            fairies.Add(fairy);
        }
    }

    private void UpdateFairyMovement(Fairy fairy, int width, int height, bool isScattering)
    {
        // Calculate current speed to influence direction changes
        float currentSpeed = (float)Math.Sqrt(fairy.Velocity.X * fairy.Velocity.X + fairy.Velocity.Y * fairy.Velocity.Y);
        float normalizedSpeed = currentSpeed / 4.5f; // Normalize against max possible speed
        
        // Organic wandering behavior
        // Update wander angle with changes proportional to speed
        // Faster fairies change direction more frequently (more erratic)
        // Slower fairies change direction less (more smooth/lazy movement)
        float wanderChangeAmount = 0.15f + (normalizedSpeed * 0.35f); // Range: 0.15 to 0.5
        fairy.WanderAngle += (float)(random.NextDouble() - 0.5) * wanderChangeAmount;
        
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
        
        // Calculate speed based on glow intensity change
        float glowChange = fairy.GlowIntensity - fairy.PreviousGlowIntensity;
        
        // Map glow change to speed multiplier
        // Increasing glow (positive change) = faster
        // Decreasing glow (negative change) = slower
        // Base speed: 2.0, range: 1.0 (slow) to 4.5 (fast)
        float baseMaxSpeed = 2.0f;
        float speedMultiplier = 1.0f + Math.Clamp(glowChange * 25f, -0.5f, 1.25f);
        float maxSpeed = baseMaxSpeed * speedMultiplier;
        
        // During scatter, allow higher speeds temporarily
        if (isScattering)
        {
            maxSpeed = Math.Max(maxSpeed, 10f);
        }
        
        // Limit speed
        float speed = (float)Math.Sqrt(fairy.Velocity.X * fairy.Velocity.X + fairy.Velocity.Y * fairy.Velocity.Y);
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

    private void DrawFairy(SKCanvas canvas, Fairy fairy, int width, int height)
    {
        // Calculate glow size based on intensity (minimum glow even when quiet)
        float baseSize = 8f;
        float glowSize = baseSize + (fairy.GlowIntensity * 30f);
        
        // Limit fairy size to 15% of the smaller screen dimension
        float maxDimension = Math.Min(width, height);
        float maxSize = maxDimension * 0.15f;
        glowSize = Math.Min(glowSize, maxSize);
        
        // Draw multiple circles with decreasing alpha for glow effect
        int glowLayers = 5;
        for (int i = glowLayers; i > 0; i--)
        {
            float layerSize = glowSize * (i / (float)glowLayers);
            byte alpha = (byte)(100 * (1f - i / (float)(glowLayers + 1)));
            
            glowPaint.Color = fairy.Color.WithAlpha(alpha);
            canvas.DrawCircle(fairy.Position, layerSize, glowPaint);
        }
        
        // Draw bright core
        corePaint.Color = fairy.Color.WithAlpha(255);
        canvas.DrawCircle(fairy.Position, baseSize * 0.4f, corePaint);
    }
}
