using SkiaSharp;
using System.IO;

namespace earhole.Modes;

/// <summary>
/// Cold War themed visualizer mode
/// </summary>
public class ColdWarMode : IVisualizerMode
{
    private readonly Random random = new Random();
    private Svg.Skia.SKSvg? worldMapSvg;
    private SKBitmap? mapColorCache;
    private float scaleX = 1f;
    private float scaleY = 1f;
    private readonly List<Explosion> explosions = new List<Explosion>();
    private readonly List<Missile> missiles = new List<Missile>();

    // NATO countries (blue)
    private static readonly HashSet<string> NATOCountries = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "US", "USA", "United States", "Canada", "CA",
        "UK", "United Kingdom", "GB", "France", "FR", "Germany", "DE", "Italy", "IT",
        "Spain", "ES", "Portugal", "PT", "Netherlands", "NL", "Belgium", "BE",
        "Denmark", "DK", "Norway", "NO", "Iceland", "IS", "Luxembourg", "LU",
        "Greece", "GR", "Turkey", "TR"
    };

    // Warsaw Pact countries (red)
    private static readonly HashSet<string> WarsawPactCountries = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Soviet Union", "USSR", "Russia", "RU",
        "Poland", "PL", "East Germany", "DD", "Czechoslovakia", "CS", "CZ",
        "Hungary", "HU", "Romania", "RO", "Bulgaria", "BG",
        "Albania", "AL", "Mongolia", "MN", "Cuba", "CU", "Vietnam", "VN"
    };

    private enum Alliance { None, Western, Soviet }

    private class Explosion
    {
        public SKPoint Position { get; set; }
        public float Age { get; set; }
        public float MaxAge { get; set; } = 30f;
        public Alliance Alliance { get; set; }
        public float Size { get; set; } = 1.0f; // Scale multiplier for explosion size
        public float Brightness { get; set; } = 1.0f; // Brightness multiplier
    }

    private class Missile
    {
        public SKPoint Start { get; set; }
        public SKPoint End { get; set; }
        public float Progress { get; set; }
        public Alliance Alliance { get; set; }
        public SKPoint ControlPoint { get; set; }
    }

    public string Name => "cold war";
    public string Emoji => "💥";

    private void EnsureMapLoaded()
    {
        if (worldMapSvg == null)
        {
            var mapPath = Path.Combine("assets", "world-map.svg");
            if (File.Exists(mapPath))
            {
                worldMapSvg = new Svg.Skia.SKSvg();
                using var stream = File.OpenRead(mapPath);
                worldMapSvg.Load(stream);
            }
        }
    }

    private void CreateColorCache(int width, int height)
    {
        if (mapColorCache != null && mapColorCache.Width == width && mapColorCache.Height == height)
            return;

        mapColorCache?.Dispose();
        mapColorCache = new SKBitmap(width, height);

        if (worldMapSvg?.Picture != null)
        {
            using var surface = SKSurface.Create(new SKImageInfo(width, height));
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Black);

            scaleX = (float)width / worldMapSvg.Picture.CullRect.Width;
            scaleY = (float)height / worldMapSvg.Picture.CullRect.Height;

            canvas.Scale(scaleX, scaleY);
            canvas.DrawPicture(worldMapSvg.Picture);
            canvas.Flush();

            using var image = surface.Snapshot();
            using var pixmap = image.PeekPixels();
            pixmap.ReadPixels(mapColorCache.Info, mapColorCache.GetPixels(), mapColorCache.RowBytes);
        }
    }

    private Alliance GetAllianceAtPoint(SKPoint point, int width, int height)
    {
        if (mapColorCache == null)
            return Alliance.None;

        int x = Math.Clamp((int)point.X, 0, width - 1);
        int y = Math.Clamp((int)point.Y, 0, height - 1);

        SKColor color = mapColorCache.GetPixel(x, y);

        // Check if pixel is red (Soviet)
        if (color.Red > 150 && color.Red > color.Blue * 1.5f && color.Red > color.Green * 1.5f)
            return Alliance.Soviet;

        // Check if pixel is blue (Western)
        if (color.Blue > 150 && color.Blue > color.Red * 1.5f && color.Blue > color.Green * 1.5f)
            return Alliance.Western;

        return Alliance.None;
    }

    private SKPoint GetRandomPointInAlliance(Alliance alliance, int width, int height, int maxAttempts = 50)
    {
        for (int attempts = 0; attempts < maxAttempts; attempts++)
        {
            float x = (float)random.NextDouble() * width;
            float y = (float)random.NextDouble() * height;
            SKPoint point = new SKPoint(x, y);

            if (GetAllianceAtPoint(point, width, height) == alliance)
                return point;
        }

        // Fallback: return center of main territory
        if (alliance == Alliance.Western)
        {
            // Center of North America
            return new SKPoint(width * 0.17f, height * 0.32f);
        }
        else
        {
            // Center of USSR
            return new SKPoint(width * 0.70f, height * 0.22f);
        }
    }

    public void Render(SKCanvas canvas, int width, int height, float[] leftSpectrum, float[] rightSpectrum, bool isBeat)
    {
        canvas.Clear(SKColors.Black);
        
        EnsureMapLoaded();
        CreateColorCache(width, height);

        // Draw the world map background - fill the entire screen
        if (worldMapSvg?.Picture != null)
        {
            canvas.Save();
            canvas.Scale(scaleX, scaleY);
            canvas.DrawPicture(worldMapSvg.Picture);
            canvas.Restore();
        }

        // Process frequencies
        int step = Math.Max(1, leftSpectrum.Length / 20); // Sample ~20 frequencies
        for (int i = 0; i < leftSpectrum.Length; i += step)
        {
            // Pick random point
            float x = (float)random.NextDouble() * width;
            float y = (float)random.NextDouble() * height;
            SKPoint point = new SKPoint(x, y);

            Alliance alliance = GetAllianceAtPoint(point, width, height);
            if (alliance == Alliance.None)
                continue;

            float leftValue = leftSpectrum[i];
            float rightValue = rightSpectrum[i];

            if (leftValue > rightValue && leftValue > 0.3f)
            {
                // Left channel wins - create small explosion
                explosions.Add(new Explosion
                {
                    Position = point,
                    Age = 0,
                    Alliance = alliance,
                    Size = 0.5f, // Smaller explosion
                    Brightness = isBeat ? 2.0f : 1.0f
                });
            }
            else if (rightValue > leftValue && rightValue > 0.3f)
            {
                // Right channel wins - launch missile to opposite alliance
                Alliance targetAlliance = alliance == Alliance.Western ? Alliance.Soviet : Alliance.Western;
                SKPoint target = GetRandomPointInAlliance(targetAlliance, width, height);

                // Create arc control point
                SKPoint mid = new SKPoint((point.X + target.X) / 2, Math.Min(point.Y, target.Y) - 100);

                missiles.Add(new Missile
                {
                    Start = point,
                    End = target,
                    Progress = 0,
                    Alliance = alliance,
                    ControlPoint = mid
                });
            }
        }

        // Update and draw missiles
        for (int i = missiles.Count - 1; i >= 0; i--)
        {
            var missile = missiles[i];
            missile.Progress += 0.02f;

            if (missile.Progress >= 1.0f)
            {
                // Missile reached target - create large explosion
                explosions.Add(new Explosion
                {
                    Position = missile.End,
                    Age = 0,
                    Alliance = missile.Alliance == Alliance.Western ? Alliance.Soviet : Alliance.Western,
                    Size = 2.0f, // Larger explosion for missile impacts
                    Brightness = isBeat ? 2.0f : 1.0f
                });
                missiles.RemoveAt(i);
                continue;
            }

            // Draw missile arc (quadratic bezier)
            float t = missile.Progress;
            float x = (1 - t) * (1 - t) * missile.Start.X + 2 * (1 - t) * t * missile.ControlPoint.X + t * t * missile.End.X;
            float y = (1 - t) * (1 - t) * missile.Start.Y + 2 * (1 - t) * t * missile.ControlPoint.Y + t * t * missile.End.Y;

            SKColor missileColor = missile.Alliance == Alliance.Western ? new SKColor(100, 150, 255) : new SKColor(255, 100, 100);
            using var paint = new SKPaint
            {
                Color = missileColor,
                IsAntialias = true,
                StrokeWidth = 2,
                Style = SKPaintStyle.Stroke
            };

            canvas.DrawCircle(x, y, 3, paint);
        }

        // Update and draw explosions
        for (int i = explosions.Count - 1; i >= 0; i--)
        {
            var explosion = explosions[i];
            explosion.Age++;

            if (explosion.Age >= explosion.MaxAge)
            {
                explosions.RemoveAt(i);
                continue;
            }

            float progress = explosion.Age / explosion.MaxAge;
            float radius = progress * 40f * explosion.Size; // Apply size multiplier
            byte alpha = (byte)((1 - progress) * 255);

            // Apply brightness multiplier to colors
            byte baseR = (byte)(explosion.Alliance == Alliance.Western ? 100 : 255);
            byte baseG = (byte)(explosion.Alliance == Alliance.Western ? 150 : 100);
            byte baseB = (byte)(explosion.Alliance == Alliance.Western ? 255 : 100);
            
            byte brightR = (byte)Math.Min(255, baseR * explosion.Brightness);
            byte brightG = (byte)Math.Min(255, baseG * explosion.Brightness);
            byte brightB = (byte)Math.Min(255, baseB * explosion.Brightness);

            SKColor explosionColor = new SKColor(brightR, brightG, brightB, alpha);

            using var paint = new SKPaint
            {
                Color = explosionColor,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };

            canvas.DrawCircle(explosion.Position, radius, paint);

            // Inner bright core (also affected by brightness)
            byte coreAlpha = (byte)Math.Min(255, alpha * 0.8f * explosion.Brightness);
            using var corePaint = new SKPaint
            {
                Color = new SKColor(255, 255, 255, coreAlpha),
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawCircle(explosion.Position, radius * 0.3f, corePaint);
        }
    }
}
