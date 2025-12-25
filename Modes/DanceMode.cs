using SkiaSharp;

namespace earhole.Modes;

/// <summary>
/// Stick figure dancer that moves to the music - legs with bass/beat, upper body with mid/high frequencies
/// </summary>
public class DanceMode : IVisualizerMode
{
    public string Name => "the dance";
    public string Emoji => "🕺";

    // Animation state
    private float legAngleLeft = 0f;
    private float legAngleRight = 0f;
    private float armAngleLeft = 0f;
    private float armAngleRight = 0f;
    private float torsoRotation = 0f;
    private float headBob = 0f;
    private float beatIntensity = 0f;

    // Lighter state
    private bool isLighterUp = false;
    private float overallIntensity = 0f;
    private DateTime lastLighterUpTime = DateTime.MinValue;
    private double currentLighterMinDuration = 0;
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

        // Calculate frequency bands
        float bassEnergy = GetBandEnergy(leftSpectrum, rightSpectrum, 0, 10); // 0-430 Hz
        float midEnergy = GetBandEnergy(leftSpectrum, rightSpectrum, 10, 40); // 430-1700 Hz
        float highEnergy = GetBandEnergy(leftSpectrum, rightSpectrum, 40, 100); // 1700-4300 Hz

        // Calculate overall intensity with smoothing
        float instantIntensity = bassEnergy + midEnergy + highEnergy;
        overallIntensity += (instantIntensity - overallIntensity) * INTENSITY_SMOOTHING;

        // Hysteresis for lighter state (prevents flickering)
        // Can only raise lighter once every 30 seconds, and only 5% chance when conditions are met
        if (!isLighterUp && overallIntensity > LIGHTER_THRESHOLD_UP)
        {
            double secondsSinceLastRaise = (DateTime.Now - lastLighterUpTime).TotalSeconds;
            if (secondsSinceLastRaise >= LIGHTER_COOLDOWN_SECONDS)
            {
                // 5% chance to actually raise the lighter
                if (random.NextDouble() < 0.05)
                {
                    isLighterUp = true;
                    lastLighterUpTime = DateTime.Now;
                    // Set random minimum duration between 4 and 6 seconds
                    currentLighterMinDuration = 4.0 + random.NextDouble() * 2.0; // 4.0 to 6.0 seconds
                }
            }
        }
        else if (isLighterUp && overallIntensity < LIGHTER_THRESHOLD_DOWN)
        {
            // Only lower if minimum duration has elapsed
            double secondsSinceLighterUp = (DateTime.Now - lastLighterUpTime).TotalSeconds;
            if (secondsSinceLighterUp >= currentLighterMinDuration)
            {
                isLighterUp = false;
            }
        }

        // Bass/beat drives leg movement
        if (isBeat)
        {
            // Alternate legs on each beat (constrained angles)
            if (legTargetLeft < 0)
            {
                legTargetLeft = Math.Min(35f, 25f + bassEnergy * 10f);
                legTargetRight = Math.Max(-30f, -20f - bassEnergy * 8f);
            }
            else
            {
                legTargetLeft = Math.Max(-30f, -20f - bassEnergy * 8f);
                legTargetRight = Math.Min(35f, 25f + bassEnergy * 10f);
            }
            beatIntensity = 1f;
        }
        else
        {
            // Return to neutral gradually
            legTargetLeft *= 0.95f;
            legTargetRight *= 0.95f;
            beatIntensity *= 0.9f;
        }

        // Smooth leg movement
        legAngleLeft += (legTargetLeft - legAngleLeft) * LEG_SMOOTHING;
        legAngleRight += (legTargetRight - legAngleRight) * LEG_SMOOTHING;

        // Mid frequencies drive arm movement (constrained)
        // Right arm holds lighter when intensity is high
        if (isLighterUp)
        {
            armAngleRight = 180f; // Arm raised straight up for lighter (180 degrees points upward in this coordinate system)
            armAngleLeft = (float)Math.Sin(DateTime.Now.Ticks / 2000000.0) * Math.Min(40f, 20f + midEnergy * 20f);
        }
        else
        {
            armAngleLeft = (float)Math.Sin(DateTime.Now.Ticks / 2000000.0) * Math.Min(40f, 20f + midEnergy * 20f);
            armAngleRight = (float)Math.Sin(DateTime.Now.Ticks / 2000000.0 + Math.PI) * Math.Min(40f, 20f + midEnergy * 20f);
        }

        // High frequencies drive torso and head (constrained)
        float targetTorso = (float)Math.Sin(DateTime.Now.Ticks / 3000000.0) * Math.Min(10f, highEnergy * 8f);
        torsoRotation += (targetTorso - torsoRotation) * TORSO_SMOOTHING;
        headBob = (float)Math.Sin(DateTime.Now.Ticks / 1500000.0) * Math.Min(8f, 3f + highEnergy * 5f);

        // Scale figure to fit screen
        float scale = Math.Min(width, height) / 600f;
        float centerX = width / 2f;
        float baseY = height * 0.75f; // Position feet at 75% down the screen

        canvas.Save();
        canvas.Translate(centerX, baseY);
        canvas.Scale(scale, scale);

        // White paint for stick figure
        using var paint = new SKPaint
        {
            Color = SKColors.White,
            StrokeWidth = 8,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round
        };

        // Enhanced stroke on beat
        if (beatIntensity > 0.3f)
        {
            paint.StrokeWidth = 8 + beatIntensity * 4f;
            paint.Color = SKColors.White.WithAlpha((byte)(200 + beatIntensity * 55));
        }

        // Draw legs (connected to hip at 0, 0)
        DrawLeg(canvas, paint, 0, 0, legAngleLeft, 70); // Left leg
        DrawLeg(canvas, paint, 0, 0, legAngleRight, 70); // Right leg

        // Draw torso with rotation
        canvas.Save();
        canvas.RotateDegrees(torsoRotation);
        
        // Hip to shoulder
        canvas.DrawLine(0, 0, 0, -120, paint);
        
        // Draw arms from shoulders
        DrawArm(canvas, paint, 0, -110, armAngleLeft, 60, true); // Left arm
        DrawArm(canvas, paint, 0, -110, armAngleRight, 60, false); // Right arm

        // Draw lighter if raised
        if (isLighterUp)
        {
            DrawLighter(canvas, paint, 0, -110, armAngleRight, 60);
        }

        // Draw head with bob (constrained to stay attached to neck)
        float neckY = -120;
        float headCenterY = neckY - 15 - Math.Abs(headBob); // Head stays above neck, bobs vertically
        canvas.DrawCircle(0, headCenterY, 20, paint);
        
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
        var barColor = isLighterUp ? SKColors.Orange : (overallIntensity > LIGHTER_THRESHOLD_DOWN ? SKColors.Yellow : SKColors.Green);
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
        canvas.DrawText($"Lighter: {(isLighterUp ? "UP" : "down")}", x, y + 70, textPaint);
        canvas.DrawText($"Up: {LIGHTER_THRESHOLD_UP:F1} Down: {LIGHTER_THRESHOLD_DOWN:F1}", x + 150, y + 70, textPaint);
    }

    private void DrawLeg(SKCanvas canvas, SKPaint paint, float startX, float startY, float angle, float length)
    {
        // Clamp angle to reasonable range to keep legs attached
        angle = Math.Clamp(angle, -45f, 45f);
        
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

    private void DrawLighter(SKCanvas canvas, SKPaint paint, float startX, float startY, float angle, float armLength)
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

        // Draw flame (flickering effect)
        float flicker = (float)Math.Sin(DateTime.Now.Ticks / 500000.0) * 2f;
        float flameHeight = 8f + flicker;
        
        using var flamePath = new SKPath();
        flamePath.MoveTo(handX, handY - 8);
        flamePath.LineTo(handX - 2, handY - 8 - flameHeight * 0.5f);
        flamePath.LineTo(handX, handY - 8 - flameHeight);
        flamePath.LineTo(handX + 2, handY - 8 - flameHeight * 0.5f);
        flamePath.Close();
        
        // Outer orange glow
        using var glowPaint = new SKPaint
        {
            Color = SKColors.Orange.WithAlpha(150),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawPath(flamePath, glowPaint);
        
        // Inner yellow flame
        using var flamePaint = new SKPaint
        {
            Color = SKColors.Yellow,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        
        var innerFlamePath = new SKPath();
        innerFlamePath.MoveTo(handX, handY - 8);
        innerFlamePath.LineTo(handX - 1, handY - 8 - flameHeight * 0.6f);
        innerFlamePath.LineTo(handX, handY - 8 - flameHeight * 0.8f);
        innerFlamePath.LineTo(handX + 1, handY - 8 - flameHeight * 0.6f);
        innerFlamePath.Close();
        canvas.DrawPath(innerFlamePath, flamePaint);
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
