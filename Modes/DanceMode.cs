using SkiaSharp;

namespace earhole.Modes;

/// <summary>
/// Stick figure dancer that moves to the music - legs with bass/beat, upper body with mid/high frequencies
/// </summary>
public class DanceMode : IVisualizerMode
{
    public string Name => "the dance";
    public string Emoji => "🕺";

    private class Dancer
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Scale { get; set; }
        public SKColor LighterColor { get; set; }
        public bool IsLighterUp { get; set; }
        public DateTime LastLighterUpTime { get; set; }
        public double LighterMinDuration { get; set; }
        
        // Variation properties for unique animation per dancer
        public float PhaseOffset { get; set; }      // Time offset for out-of-sync animation
        public float AnimationSpeed { get; set; }   // Speed multiplier (0.8-1.2)
        public float MovementScale { get; set; }    // Movement amplitude multiplier (0.7-1.3)
        
        // Per-dancer limb angles for independent movement
        public float LeftLegAngle { get; set; }
        public float RightLegAngle { get; set; }
        public float LeftArmAngle { get; set; }
        public float RightArmAngle { get; set; }
        public float LeftLegTarget { get; set; }
        public float RightLegTarget { get; set; }
        
        // Physical variation properties (subtle differences)
        public float TorsoHeight { get; set; }      // Multiplier for torso length (0.9-1.1)
        public float LegLength { get; set; }        // Multiplier for leg length (0.9-1.1)
        public float ArmLength { get; set; }        // Multiplier for arm length (0.9-1.1)
        public float HeadSize { get; set; }         // Multiplier for head radius (0.85-1.15)
        public float BodyWidth { get; set; }        // Multiplier for stroke width (0.85-1.15)
    }

    // Crowd of dancers
    private List<Dancer> dancers = new List<Dancer>();
    private bool initialized = false;

    // Animation state (shared by all dancers)
    private float legAngleLeft = 0f;
    private float legAngleRight = 0f;
    private float armAngleLeft = 0f;
    private float armAngleRight = 0f;
    private float torsoRotation = 0f;
    private float headBob = 0f;
    private float beatIntensity = 0f;
    private float highEnergy = 0f;

    // Global lighter state
    private float overallIntensity = 0f;
    private Random random = new Random();
    private const float LIGHTER_THRESHOLD_UP = 250f; // Threshold to raise lighter
    private const float LIGHTER_THRESHOLD_DOWN = 220f; // Threshold to lower lighter (hysteresis)
    private const float INTENSITY_SMOOTHING = 0.15f;
    private const double LIGHTER_COOLDOWN_SECONDS = 30.0; // Minimum time between lighter raises

    // Smoothing for movements
    private float legTargetLeft = 0f;
    private float legTargetRight = 0f;
    private const float LEG_SMOOTHING = 0.15f;
    private const float ARM_SMOOTHING = 0.2f;
    private const float TORSO_SMOOTHING = 0.1f;

    public void Render(SKCanvas canvas, int width, int height, float[] leftSpectrum, float[] rightSpectrum, bool isBeat)
    {
        canvas.Clear(SKColors.Black);

        // Initialize dancers on first render
        if (!initialized)
        {
            InitializeDancers(width, height);
            initialized = true;
        }

        // Calculate frequency bands
        float bassEnergy = GetBandEnergy(leftSpectrum, rightSpectrum, 0, 10); // 0-430 Hz
        float midEnergy = GetBandEnergy(leftSpectrum, rightSpectrum, 10, 40); // 430-1700 Hz
        highEnergy = GetBandEnergy(leftSpectrum, rightSpectrum, 40, 100); // 1700-4300 Hz

        // Calculate overall intensity with smoothing
        float instantIntensity = bassEnergy + midEnergy + highEnergy;
        overallIntensity += (instantIntensity - overallIntensity) * INTENSITY_SMOOTHING;

        // Update lighter state for each dancer
        foreach (var dancer in dancers)
        {
            // Can only raise lighter once every 30 seconds, and only very small chance when conditions are met
            if (!dancer.IsLighterUp && overallIntensity > LIGHTER_THRESHOLD_UP)
            {
                double secondsSinceLastRaise = (DateTime.Now - dancer.LastLighterUpTime).TotalSeconds;
                if (secondsSinceLastRaise >= LIGHTER_COOLDOWN_SECONDS)
                {
                    // Scale probability inversely with dancer count to maintain consistent overall rate
                    // Target: ~1 lighter every 4-5 seconds across entire crowd when intensity sustained
                    // At 60fps: 0.004 / dancerCount per frame = 0.24 / dancerCount per second
                    double probability = 0.004 / dancers.Count;
                    if (random.NextDouble() < probability)
                    {
                        dancer.IsLighterUp = true;
                        dancer.LastLighterUpTime = DateTime.Now;
                        // Set random minimum duration between 4 and 6 seconds
                        dancer.LighterMinDuration = 4.0 + random.NextDouble() * 2.0;
                        // Generate random flame color for this dancer
                        dancer.LighterColor = new SKColor(
                            (byte)random.Next(256),
                            (byte)random.Next(256),
                            (byte)random.Next(256)
                        );
                    }
                }
            }
            else if (dancer.IsLighterUp && overallIntensity < LIGHTER_THRESHOLD_DOWN)
            {
                // Only lower if minimum duration has elapsed
                double secondsSinceLighterUp = (DateTime.Now - dancer.LastLighterUpTime).TotalSeconds;
                if (secondsSinceLighterUp >= dancer.LighterMinDuration)
                {
                    dancer.IsLighterUp = false;
                }
            }
        }

        // Bass/beat drives leg movement - update each dancer independently
        if (isBeat)
        {
            beatIntensity = 1f;
            // Each dancer gets independent leg targets
            foreach (var dancer in dancers)
            {
                // Random chance to step with each leg independently
                if (random.NextDouble() < 0.6) // 60% chance to step with left
                {
                    dancer.LeftLegTarget = (random.NextDouble() < 0.5 ? 1 : -1) * 
                        Math.Min(35f, 20f + bassEnergy * 8f) * dancer.MovementScale;
                }
                if (random.NextDouble() < 0.6) // 60% chance to step with right
                {
                    dancer.RightLegTarget = (random.NextDouble() < 0.5 ? 1 : -1) * 
                        Math.Min(35f, 20f + bassEnergy * 8f) * dancer.MovementScale;
                }
            }
        }
        else
        {
            beatIntensity *= 0.9f;
        }

        // Smooth leg movement for each dancer
        foreach (var dancer in dancers)
        {
            // Return to neutral gradually
            dancer.LeftLegTarget *= 0.95f;
            dancer.RightLegTarget *= 0.95f;
            
            // Smooth towards target
            dancer.LeftLegAngle += (dancer.LeftLegTarget - dancer.LeftLegAngle) * LEG_SMOOTHING;
            dancer.RightLegAngle += (dancer.RightLegTarget - dancer.RightLegAngle) * LEG_SMOOTHING;
            
            // Enforce minimum stance width - legs must maintain separation
            // Left leg should be negative (to the left), right leg positive (to the right)
            if (dancer.LeftLegAngle > -10f)
                dancer.LeftLegAngle = -10f;
            if (dancer.RightLegAngle < 10f)
                dancer.RightLegAngle = 10f;
        }

        // Mid frequencies drive arm movement (constrained)
        // (Arm angles controlled per-dancer in DrawDancer method now)

        // High frequencies drive torso and head (constrained)
        float targetTorso = (float)Math.Sin(DateTime.Now.Ticks / 3000000.0) * Math.Min(10f, highEnergy * 8f);
        torsoRotation += (targetTorso - torsoRotation) * TORSO_SMOOTHING;
        headBob = (float)Math.Sin(DateTime.Now.Ticks / 1500000.0) * Math.Min(8f, 3f + highEnergy * 5f);

        // Draw each dancer
        foreach (var dancer in dancers)
        {
            DrawDancer(canvas, dancer, width, height);
        }
    }

    private void InitializeDancers(int width, int height)
    {
        int dancerCount = random.Next(40, 81); // 40 to 80 dancers
        
        for (int i = 0; i < dancerCount; i++)
        {
            float x = 0, yPosition = 0, depth = 0;
            bool validPosition = false;
            int attempts = 0;
            const int maxAttempts = 50;
            
            // Try to find a position that doesn't overlap with existing dancers
            while (!validPosition && attempts < maxAttempts)
            {
                attempts++;
                
                // Random depth (Z-axis simulation) - closer dancers are larger
                depth = 0.3f + (float)random.NextDouble() * 0.7f; // 0.3 to 1.0
                
                // Stage perspective: viewing from low angle looking into crowd
                // In screen coords: Y=0 is TOP, Y=height is BOTTOM
                // Closer dancers (depth=1.0) should be at bottom (large Y)
                // Further dancers (depth=0.3) should be at top (small Y)
                yPosition = height * (0.2f + depth * 0.65f); // Range: 20% (far) to 85% (close) of screen height
                x = (float)(random.NextDouble() * width);
                
                // Check minimum distance from all existing dancers
                // Minimum distance scales with depth (larger dancers need more space)
                float minDistance = 80f * depth; // Scales from 24 (far) to 80 (close) pixels
                validPosition = true;
                
                foreach (var existingDancer in dancers)
                {
                    float dx = x - existingDancer.X;
                    float dy = yPosition - existingDancer.Y;
                    float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                    
                    // Consider the size of both dancers
                    float requiredDistance = (minDistance + 80f * existingDancer.Scale) * 0.5f;
                    
                    if (distance < requiredDistance)
                    {
                        validPosition = false;
                        break;
                    }
                }
            }
            
            // If we couldn't find a valid position after max attempts, use the last attempt anyway
            
            dancers.Add(new Dancer
            {
                X = x,
                Y = yPosition,
                Scale = depth, // Closer = larger
                LighterColor = SKColors.Yellow,
                IsLighterUp = false,
                LastLighterUpTime = DateTime.MinValue,
                LighterMinDuration = 0,
                
                // Random variation for unique animation
                PhaseOffset = (float)(random.NextDouble() * Math.PI * 2), // 0 to 2π
                AnimationSpeed = 0.8f + (float)random.NextDouble() * 0.4f, // 0.8 to 1.2
                MovementScale = 0.7f + (float)random.NextDouble() * 0.6f,   // 0.7 to 1.3
                
                // Initialize limb angles with minimum stance width
                LeftLegAngle = -10f,  // Left leg starts at -10° (to the left)
                RightLegAngle = 10f,  // Right leg starts at 10° (to the right)
                LeftArmAngle = 0f,
                RightArmAngle = 0f,
                LeftLegTarget = -10f,
                RightLegTarget = 10f,
                
                // Physical variation (subtle differences in proportions)
                TorsoHeight = 0.9f + (float)random.NextDouble() * 0.2f,    // 0.9 to 1.1
                LegLength = 0.9f + (float)random.NextDouble() * 0.2f,      // 0.9 to 1.1
                ArmLength = 0.9f + (float)random.NextDouble() * 0.2f,      // 0.9 to 1.1
                HeadSize = 0.85f + (float)random.NextDouble() * 0.3f,      // 0.85 to 1.15
                BodyWidth = 0.85f + (float)random.NextDouble() * 0.3f      // 0.85 to 1.15
            });
        }
        
        // Sort by depth so further dancers are drawn first (painter's algorithm)
        dancers = dancers.OrderBy(d => d.Scale).ToList();
    }

    private void DrawDancer(SKCanvas canvas, Dancer dancer, int width, int height)
    {
        canvas.Save();
        canvas.Translate(dancer.X, dancer.Y);
        
        // Scale based on depth
        float scale = (Math.Min(width, height) / 600f) * dancer.Scale;
        canvas.Scale(scale, scale);

        // White paint for stick figure
        using var paint = new SKPaint
        {
            Color = SKColors.White,
            StrokeWidth = 8 * dancer.BodyWidth,  // Apply body width variation
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round
        };

        // Enhanced stroke on beat
        if (beatIntensity > 0.3f)
        {
            paint.StrokeWidth = (8 + beatIntensity * 4f) * dancer.BodyWidth;
            paint.Color = SKColors.White.WithAlpha((byte)(200 + beatIntensity * 55));
        }

        // Draw legs (connected to hip at 0, 0) - use per-dancer leg angles and length
        float legLength = 70 * dancer.LegLength;
        DrawLeg(canvas, paint, 0, 0, dancer.LeftLegAngle, legLength); // Left leg
        DrawLeg(canvas, paint, 0, 0, dancer.RightLegAngle, legLength); // Right leg

        // Draw torso with rotation - apply dancer variation
        canvas.Save();
        canvas.RotateDegrees(torsoRotation * dancer.MovementScale);
        
        // Hip to shoulder - apply torso height variation
        float torsoHeight = 120 * dancer.TorsoHeight;
        canvas.DrawLine(0, 0, 0, -torsoHeight, paint);
        
        // Calculate per-dancer arm angles with unique phase and speed - INDEPENDENT movement
        // Left arm uses one frequency, right arm uses different frequency for independent motion
        float dancerArmLeft = (float)Math.Sin((DateTime.Now.Ticks / 2000000.0) * dancer.AnimationSpeed + dancer.PhaseOffset) * 40f * dancer.MovementScale;
        float dancerArmRight = dancer.IsLighterUp ? 180f : 
            (float)Math.Sin((DateTime.Now.Ticks / 1800000.0) * dancer.AnimationSpeed + dancer.PhaseOffset + 1.3) * 35f * dancer.MovementScale; // Different frequency and amplitude
        
        // Draw arms from shoulders - apply arm length variation
        float shoulderY = -torsoHeight * 0.917f;  // Shoulders at ~91.7% of torso height
        float armLength = 60 * dancer.ArmLength;
        DrawArm(canvas, paint, 0, shoulderY, dancerArmLeft, armLength, true); // Left arm
        DrawArm(canvas, paint, 0, shoulderY, dancerArmRight, armLength, false); // Right arm

        // Draw lighter if raised for this dancer
        if (dancer.IsLighterUp)
        {
            DrawLighter(canvas, paint, 0, shoulderY, 180f, armLength, dancer.LighterColor);
        }

        // Draw head with bob (constrained to stay attached to neck) - apply dancer variation
        float neckY = -torsoHeight;
        float dancerHeadBob = (float)Math.Sin((DateTime.Now.Ticks / 1500000.0) * dancer.AnimationSpeed + dancer.PhaseOffset) * Math.Min(8f, 3f + highEnergy * 5f) * dancer.MovementScale;
        float headCenterY = neckY - 15 * dancer.HeadSize - Math.Abs(dancerHeadBob); // Head stays above neck, bobs vertically
        canvas.DrawCircle(0, headCenterY, 20 * dancer.HeadSize, paint);
        
        canvas.Restore();

        canvas.Restore();
    }

    private void DrawIntensityDebug(SKCanvas canvas, int width, int height)
    {
        int barWidth = 300;
        int barHeight = 30;
        int margin = 20;
        int x = margin;
        int y = margin;

        // Background box
        using var bgPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 180),
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRect(x - 5, y - 5, barWidth + 60, 90, bgPaint);

        // Text paint
        using var textPaint = new SKPaint
        {
            Color = SKColors.White,
            TextSize = 14,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        };

        // Draw intensity bar
        float maxDisplay = 350f;
        float barFillWidth = Math.Min(overallIntensity / maxDisplay, 1f) * barWidth;
        
        // Bar background
        using var barBgPaint = new SKPaint { Color = new SKColor(60, 60, 60), Style = SKPaintStyle.Fill };
        canvas.DrawRect(x, y + 20, barWidth, barHeight, barBgPaint);
        
        // Bar fill (color changes based on threshold)
        bool anyLighterUp = dancers.Any(d => d.IsLighterUp);
        var barColor = anyLighterUp ? SKColors.Orange : (overallIntensity > LIGHTER_THRESHOLD_DOWN ? SKColors.Yellow : SKColors.Green);
        using var barFillPaint = new SKPaint { Color = barColor, Style = SKPaintStyle.Fill };
        canvas.DrawRect(x, y + 20, barFillWidth, barHeight, barFillPaint);

        // Draw threshold lines
        float upThresholdX = x + (LIGHTER_THRESHOLD_UP / maxDisplay) * barWidth;
        float downThresholdX = x + (LIGHTER_THRESHOLD_DOWN / maxDisplay) * barWidth;
        
        using var thresholdPaint = new SKPaint
        {
            Color = SKColors.Red,
            StrokeWidth = 2,
            Style = SKPaintStyle.Stroke
        };
        canvas.DrawLine(upThresholdX, y + 20, upThresholdX, y + 20 + barHeight, thresholdPaint);
        
        using var thresholdDownPaint = new SKPaint
        {
            Color = SKColors.Blue,
            StrokeWidth = 2,
            Style = SKPaintStyle.Stroke
        };
        canvas.DrawLine(downThresholdX, y + 20, downThresholdX, y + 20 + barHeight, thresholdDownPaint);

        // Text labels
        canvas.DrawText($"Intensity: {overallIntensity:F2}", x, y + 15, textPaint);
        canvas.DrawText($"Lighters Up: {dancers.Count(d => d.IsLighterUp)}/{dancers.Count}", x, y + 70, textPaint);
        canvas.DrawText($"Up: {LIGHTER_THRESHOLD_UP:F1} Down: {LIGHTER_THRESHOLD_DOWN:F1}", x + 150, y + 70, textPaint);
    }

    private void DrawLeg(SKCanvas canvas, SKPaint paint, float startX, float startY, float angle, float length)
    {
        // Clamp angle to reasonable range to keep legs attached
        angle = Math.Clamp(angle, -45f, 45f);
        
        // Add minimum offset from center to ensure legs never overlap
        // Legs should always have at least 10° separation from center
        if (angle >= 0 && angle < 10f)
            angle = 10f; // Right leg minimum
        else if (angle < 0 && angle > -10f)
            angle = -10f; // Left leg minimum
        
        float rad = angle * (float)Math.PI / 180f;
        float upperLength = length * 0.55f;
        float lowerLength = length * 0.45f;

        // Determine which leg based on the angle (left leg goes left, right leg goes right)
        float hipOffset = angle > 0 ? 8f : -8f; // Small offset from center for natural hip width
        
        // Upper leg (thigh) - starts from hip offset
        float hipX = startX + hipOffset;
        float kneeX = hipX + (float)Math.Sin(rad) * upperLength;
        float kneeY = startY + (float)Math.Cos(rad) * upperLength;
        canvas.DrawLine(hipX, startY, kneeX, kneeY, paint);

        // Lower leg (shin) - bends forward naturally, constrained to reasonable angle
        float lowerAngle = Math.Clamp(rad * 0.5f, -0.6f, 0.6f);
        float ankleX = kneeX + (float)Math.Sin(lowerAngle) * lowerLength;
        float ankleY = kneeY + (float)Math.Cos(lowerAngle) * lowerLength;
        canvas.DrawLine(kneeX, kneeY, ankleX, ankleY, paint);

        // Foot
        canvas.DrawLine(ankleX, ankleY, ankleX + 15, ankleY, paint);
    }

    private void DrawArm(SKCanvas canvas, SKPaint paint, float startX, float startY, float angle, float length, bool isLeft)
    {
        // Clamp angle to keep arms looking natural (allow 180 for straight up lighter raise)
        angle = Math.Clamp(angle, -50f, 180f);
        
        float rad = angle * (float)Math.PI / 180f;
        float upperLength = length * 0.5f;
        float lowerLength = length * 0.5f;

        // Direction multiplier (left vs right)
        float dir = isLeft ? -1 : 1;

        // Upper arm (shoulder to elbow) - slight outward offset
        float shoulderOffsetX = dir * 15;
        float elbowX = startX + shoulderOffsetX + dir * (float)Math.Sin(rad) * upperLength * 0.7f;
        float elbowY = startY + (float)Math.Cos(rad) * upperLength;
        canvas.DrawLine(startX, startY, elbowX, elbowY, paint);

        // Lower arm (elbow to hand) - follows through with constrained bend
        // Special case: if arm is raised (angle > 150), keep it straight for lighter pose
        float lowerAngle;
        if (angle > 150f)
        {
            lowerAngle = rad; // Keep arm straight when raised for lighter
        }
        else
        {
            lowerAngle = rad * 0.6f; // More subtle bend for normal movement
        }
        float handX = elbowX + dir * (float)Math.Sin(lowerAngle) * lowerLength;
        float handY = elbowY + (float)Math.Cos(lowerAngle) * lowerLength;
        canvas.DrawLine(elbowX, elbowY, handX, handY, paint);
    }

    private void DrawLighter(SKCanvas canvas, SKPaint paint, float startX, float startY, float angle, float armLength, SKColor flameColor)
    {
        // Use exact same calculation as DrawArm to ensure lighter is at hand position
        angle = Math.Clamp(angle, -180f, 180f);
        float rad = angle * (float)Math.PI / 180f;
        float upperLength = armLength * 0.5f;
        float lowerLength = armLength * 0.5f;

        // Right arm calculations (matches DrawArm exactly)
        float dir = 1; // Right side
        float shoulderOffsetX = dir * 15;
        float elbowX = startX + shoulderOffsetX + dir * (float)Math.Sin(rad) * upperLength * 0.7f;
        float elbowY = startY + (float)Math.Cos(rad) * upperLength;

        // Lower arm calculation (matches DrawArm exactly)
        float lowerAngle;
        if (angle > 150f)
        {
            lowerAngle = rad; // Keep arm straight when raised for lighter
        }
        else
        {
            lowerAngle = rad * 0.6f;
        }
        float handX = elbowX + dir * (float)Math.Sin(lowerAngle) * lowerLength;
        float handY = elbowY + (float)Math.Cos(lowerAngle) * lowerLength;

        // Draw lighter body (small rectangle)
        using var lighterPaint = new SKPaint
        {
            Color = SKColors.Silver,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        
        var lighterRect = new SKRect(handX - 3, handY - 8, handX + 3, handY);
        canvas.DrawRect(lighterRect, lighterPaint);

        // Draw flame (flickering effect) - 300% more intense (4x total)
        float flicker = (float)Math.Sin(DateTime.Now.Ticks / 500000.0) * 2f;
        float flameHeight = (8f + flicker) * 4f; // 4x height for intensity
        float flameWidth = 8f; // 4x width for intensity
        
        // Create larger flame path
        using var flamePath = new SKPath();
        flamePath.MoveTo(handX, handY - 8);
        flamePath.LineTo(handX - flameWidth, handY - 8 - flameHeight * 0.5f);
        flamePath.LineTo(handX, handY - 8 - flameHeight);
        flamePath.LineTo(handX + flameWidth, handY - 8 - flameHeight * 0.5f);
        flamePath.Close();
        
        // Outermost glow layer (very soft)
        for (int i = 3; i >= 1; i--)
        {
            float glowSize = i * 8f;
            byte glowAlpha = (byte)(40 / i);
            
            using var outerGlowPaint = new SKPaint
            {
                Color = flameColor.WithAlpha(glowAlpha),
                Style = SKPaintStyle.Fill,
                IsAntialias = true,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, glowSize)
            };
            canvas.DrawPath(flamePath, outerGlowPaint);
        }
        
        // Outer flame (random color with alpha)
        using var glowPaint = new SKPaint
        {
            Color = flameColor.WithAlpha(200),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawPath(flamePath, glowPaint);
        
        // Inner bright core (full intensity)
        using var flamePaint = new SKPaint
        {
            Color = flameColor,
            IsAntialias = true
        };
        canvas.DrawPath(flamePath, glowPaint);
        
        // Inner bright core (full intensity)
        using var innerFlamePaint = new SKPaint
        {
            Color = flameColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        
        var innerFlamePath = new SKPath();
        innerFlamePath.MoveTo(handX, handY - 8);
        innerFlamePath.LineTo(handX - flameWidth * 0.5f, handY - 8 - flameHeight * 0.6f);
        innerFlamePath.LineTo(handX, handY - 8 - flameHeight * 0.8f);
        innerFlamePath.LineTo(handX + flameWidth * 0.5f, handY - 8 - flameHeight * 0.6f);
        innerFlamePath.Close();
        canvas.DrawPath(innerFlamePath, innerFlamePaint);
    }

    private float GetBandEnergy(float[] leftSpectrum, float[] rightSpectrum, int startBin, int endBin)
    {
        float energy = 0f;
        int count = 0;

        for (int i = startBin; i < endBin && i < leftSpectrum.Length && i < rightSpectrum.Length; i++)
        {
            energy += leftSpectrum[i] + rightSpectrum[i];
            count++;
        }

        return count > 0 ? (energy / count) * 2f : 0f; // Average and scale
    }
}
