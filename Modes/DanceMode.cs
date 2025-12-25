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
        armAngleLeft = (float)Math.Sin(DateTime.Now.Ticks / 2000000.0) * Math.Min(40f, 20f + midEnergy * 20f);
        armAngleRight = (float)Math.Sin(DateTime.Now.Ticks / 2000000.0 + Math.PI) * Math.Min(40f, 20f + midEnergy * 20f);

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

        // Draw head with bob (constrained to stay attached to neck)
        float neckY = -120;
        float headCenterY = neckY - 15 - Math.Abs(headBob); // Head stays above neck, bobs vertically
        canvas.DrawCircle(0, headCenterY, 20, paint);
        
        canvas.Restore();

        canvas.Restore();
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
        // Clamp angle to keep arms looking natural
        angle = Math.Clamp(angle, -50f, 50f);
        
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
        float lowerAngle = rad * 0.6f; // More subtle bend
        float handX = elbowX + dir * (float)Math.Sin(lowerAngle) * lowerLength;
        float handY = elbowY + (float)Math.Cos(lowerAngle) * lowerLength;
        canvas.DrawLine(elbowX, elbowY, handX, handY, paint);
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
