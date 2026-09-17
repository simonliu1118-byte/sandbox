using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ScratchGame.Controls;

public sealed class ScratchSurface : Grid
{
    private readonly Image _maskImage = new();
    private readonly Canvas _debrisLayer = new() { IsHitTestVisible = false };
    private readonly Random _debrisRandom = new();
    private WriteableBitmap? _bitmap;
    private byte[]? _pixels;
    private int _pixelWidth;
    private int _pixelHeight;
    private int _stride;
    private long _activePixels;
    private long _erasedPixels;
    private bool _completionReported;
    private Point? _lastDebrisPoint;

    public double BrushRadius { get; set; } = 24;
    public double CompletionThreshold { get; set; } = 0.78;
    public string? MaskImagePath { get; set; }
    public string ZoneShape { get; set; } = "rectangle";
    public double CornerRadius { get; set; }
    public bool IsCompleted => _completionReported;

    public Stretch Stretch
    {
        get => _maskImage.Stretch;
        set => _maskImage.Stretch = value;
    }

    public ImageSource? Source => _maskImage.Source;

    public event EventHandler? Completed;

    public ScratchSurface()
    {
        ClipToBounds = false;
        IsHitTestVisible = false;
        _maskImage.Stretch = System.Windows.Media.Stretch.Fill;
        _maskImage.IsHitTestVisible = false;
        _debrisLayer.ClipToBounds = false;
        Children.Add(_maskImage);
        Children.Add(_debrisLayer);
        Panel.SetZIndex(_debrisLayer, 2);
        Loaded += (_, _) => EnsureBitmap();
    }

    public void ResetMask()
    {
        _debrisLayer.Children.Clear();
        _lastDebrisPoint = null;
        EnsureBitmap(force: true);
    }

    public void ErasePoints(IEnumerable<Point> points, bool checkCompletion = true)
    {
        EnsureBitmap();
        if (_bitmap is null || _pixels is null)
            return;

        var changedPoints = new List<Point>();
        foreach (var point in points)
        {
            if (!EraseCircle(point))
                continue;
            changedPoints.Add(point);
        }

        if (changedPoints.Count == 0)
            return;

        FlushPixels();
        EmitScratchDebris(changedPoints);
        if (checkCompletion)
            CheckCompletion();
    }

    public void CheckCompletion()
    {
        if (_completionReported || _activePixels <= 0)
            return;
        if ((double)_erasedPixels / _activePixels >= CompletionThreshold)
            ReportCompletionOnce();
    }

    public void RevealAll()
    {
        EnsureBitmap();
        if (_bitmap is null || _pixels is null)
            return;

        for (var i = 3; i < _pixels.Length; i += 4)
            _pixels[i] = 0;

        _erasedPixels = _activePixels;
        _debrisLayer.Children.Clear();
        FlushPixels();
        ReportCompletionOnce();
    }

    private void EnsureBitmap(bool force = false)
    {
        var width = Math.Max(1, (int)Math.Round(ActualWidth > 0 ? ActualWidth : Width));
        var height = Math.Max(1, (int)Math.Round(ActualHeight > 0 ? ActualHeight : Height));
        if (!force && _bitmap is not null && width == _pixelWidth && height == _pixelHeight)
            return;

        _pixelWidth = width;
        _pixelHeight = height;
        _stride = _pixelWidth * 4;
        _pixels = TryLoadTexture(width, height) ?? CreateFallbackTexture(width, height);
        ApplyZoneShapeMask(_pixels, width, height);
        _activePixels = CountVisiblePixels(_pixels);
        _erasedPixels = 0;
        _completionReported = false;
        _lastDebrisPoint = null;

        _bitmap = new WriteableBitmap(_pixelWidth, _pixelHeight, 96, 96, PixelFormats.Bgra32, null);
        FlushPixels();
        _maskImage.Source = _bitmap;
    }

    private byte[]? TryLoadTexture(int width, int height)
    {
        if (string.IsNullOrWhiteSpace(MaskImagePath) || !File.Exists(MaskImagePath))
            return null;
        try
        {
            var source = new BitmapImage();
            source.BeginInit();
            source.CacheOption = BitmapCacheOption.OnLoad;
            source.UriSource = new Uri(MaskImagePath, UriKind.Absolute);
            source.EndInit();
            source.Freeze();

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
                dc.DrawImage(source, new Rect(0, 0, width, height));

            var rendered = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rendered.Render(visual);
            var converted = new FormatConvertedBitmap(rendered, PixelFormats.Bgra32, null, 0);
            var pixels = new byte[width * height * 4];
            converted.CopyPixels(pixels, width * 4, 0);
            return pixels;
        }
        catch
        {
            return null;
        }
    }

    private static byte[] CreateFallbackTexture(int width, int height)
    {
        var stride = width * 4;
        var pixels = new byte[stride * height];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var offset = y * stride + x * 4;
            var variation = (byte)((x * 13 + y * 7) % 18);
            pixels[offset + 0] = (byte)(165 + variation);
            pixels[offset + 1] = (byte)(165 + variation);
            pixels[offset + 2] = (byte)(165 + variation);
            pixels[offset + 3] = 255;
        }
        return pixels;
    }

    private void ApplyZoneShapeMask(byte[] pixels, int width, int height)
    {
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            if (IsInsideZoneShape(x + 0.5, y + 0.5, width, height))
                continue;
            pixels[y * width * 4 + x * 4 + 3] = 0;
        }
    }

    private bool IsInsideZoneShape(double x, double y, double width, double height)
        => ZoneShape switch
        {
            "circle" => IsInsideEllipse(x, y, width, height),
            "ellipse" => IsInsideEllipse(x, y, width, height),
            "roundedRectangle" => IsInsideRoundedRectangle(x, y, width, height, CornerRadius),
            _ => true
        };

    private static bool IsInsideEllipse(double x, double y, double width, double height)
    {
        var rx = width / 2.0;
        var ry = height / 2.0;
        if (rx <= 0 || ry <= 0)
            return false;
        var dx = (x - rx) / rx;
        var dy = (y - ry) / ry;
        return dx * dx + dy * dy <= 1.0;
    }

    private static bool IsInsideRoundedRectangle(double x, double y, double width, double height, double cornerRadius)
    {
        var radius = Math.Clamp(cornerRadius, 0, Math.Min(width, height) / 2.0);
        if (radius <= 0)
            return true;
        if (x >= radius && x <= width - radius)
            return true;
        if (y >= radius && y <= height - radius)
            return true;
        var cx = x < radius ? radius : width - radius;
        var cy = y < radius ? radius : height - radius;
        var dx = x - cx;
        var dy = y - cy;
        return dx * dx + dy * dy <= radius * radius;
    }

    private static long CountVisiblePixels(byte[] pixels)
    {
        long count = 0;
        for (var i = 3; i < pixels.Length; i += 4)
            if (pixels[i] != 0)
                count++;
        return count;
    }

    private bool EraseCircle(Point point)
    {
        if (_pixels is null)
            return false;

        var radius = Math.Max(1, (int)Math.Round(BrushRadius));
        var centerX = (int)Math.Round(point.X);
        var centerY = (int)Math.Round(point.Y);
        var minX = Math.Max(0, centerX - radius - 3);
        var maxX = Math.Min(_pixelWidth - 1, centerX + radius + 3);
        var minY = Math.Max(0, centerY - radius - 3);
        var maxY = Math.Min(_pixelHeight - 1, centerY + radius + 3);
        var changed = false;

        for (var y = minY; y <= maxY; y++)
        for (var x = minX; x <= maxX; x++)
        {
            var ox = x - centerX;
            var oy = y - centerY;
            var hash = unchecked((x * 73856093) ^ (y * 19349663));
            var jitter = (hash & 7) - 3;
            var localRadius = Math.Max(1, radius + jitter);
            var distanceSquared = ox * ox + oy * oy;
            if (distanceSquared > localRadius * localRadius)
                continue;

            var alphaIndex = y * _stride + x * 4 + 3;
            var currentAlpha = _pixels[alphaIndex];
            if (currentAlpha == 0)
                continue;

            var edgeRadius = Math.Max(1, localRadius - 3);
            var edgeBand = distanceSquared > edgeRadius * edgeRadius;
            if (edgeBand && (hash & 3) == 0)
            {
                var softened = (byte)Math.Min((int)currentAlpha, 88);
                if (softened != currentAlpha)
                {
                    _pixels[alphaIndex] = softened;
                    changed = true;
                }
                continue;
            }

            _pixels[alphaIndex] = 0;
            _erasedPixels++;
            changed = true;
        }
        return changed;
    }

    private void EmitScratchDebris(IReadOnlyList<Point> points)
    {
        foreach (var point in points)
        {
            if (_lastDebrisPoint is Point last)
            {
                var dx = point.X - last.X;
                var dy = point.Y - last.Y;
                if (dx * dx + dy * dy < 70)
                    continue;
            }
            _lastDebrisPoint = point;
            EmitDebrisBurst(point);
        }
    }

    private void EmitDebrisBurst(Point point)
    {
        while (_debrisLayer.Children.Count > 84)
            _debrisLayer.Children.RemoveAt(0);

        var count = _debrisRandom.Next(3, 7);
        for (var i = 0; i < count; i++)
        {
            var size = 3.5 + _debrisRandom.NextDouble() * 6.0;
            var tone = (byte)_debrisRandom.Next(170, 232);
            var shard = new Rectangle
            {
                Width = size * (1.2 + _debrisRandom.NextDouble()),
                Height = Math.Max(2.2, size * 0.58),
                RadiusX = 0.8,
                RadiusY = 0.8,
                Fill = new SolidColorBrush(Color.FromArgb((byte)_debrisRandom.Next(185, 246), tone, tone, tone)),
                RenderTransformOrigin = new Point(0.5, 0.5),
                IsHitTestVisible = false,
                Opacity = 0.95
            };

            var rotate = new RotateTransform(_debrisRandom.Next(-60, 61));
            var translate = new TranslateTransform();
            var transforms = new TransformGroup();
            transforms.Children.Add(rotate);
            transforms.Children.Add(translate);
            shard.RenderTransform = transforms;

            Canvas.SetLeft(shard, point.X + _debrisRandom.NextDouble() * 14 - 7);
            Canvas.SetTop(shard, point.Y + _debrisRandom.NextDouble() * 10 - 5);
            _debrisLayer.Children.Add(shard);

            var duration = TimeSpan.FromMilliseconds(_debrisRandom.Next(360, 650));
            var horizontal = _debrisRandom.NextDouble() * 40 - 20;
            var vertical = 10 + _debrisRandom.NextDouble() * 34;
            translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, horizontal, duration)
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, vertical, duration)
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            });
            rotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(rotate.Angle, rotate.Angle + _debrisRandom.Next(-170, 171), duration));

            var fade = new DoubleAnimation(0.95, 0, duration)
            {
                BeginTime = TimeSpan.FromMilliseconds(90),
                FillBehavior = FillBehavior.Stop
            };
            fade.Completed += (_, _) => _debrisLayer.Children.Remove(shard);
            shard.BeginAnimation(OpacityProperty, fade);
        }
    }

    private void FlushPixels()
    {
        if (_bitmap is null || _pixels is null)
            return;
        _bitmap.WritePixels(new Int32Rect(0, 0, _pixelWidth, _pixelHeight), _pixels, _stride, 0);
    }

    private void ReportCompletionOnce()
    {
        if (_completionReported)
            return;
        _completionReported = true;
        Completed?.Invoke(this, EventArgs.Empty);
    }
}
