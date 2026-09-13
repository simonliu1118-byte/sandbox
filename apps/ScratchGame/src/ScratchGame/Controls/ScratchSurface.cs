using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ScratchGame.Controls;

public sealed class ScratchSurface : Image
{
    private WriteableBitmap? _bitmap;
    private byte[]? _pixels;
    private int _pixelWidth;
    private int _pixelHeight;
    private int _stride;
    private long _erasedPixels;
    private bool _completionReported;

    public double BrushRadius { get; set; } = 24;
    public double CompletionThreshold { get; set; } = 0.78;
    public string? MaskImagePath { get; set; }
    public bool IsCompleted => _completionReported;

    public event EventHandler? Completed;

    public ScratchSurface()
    {
        Stretch = Stretch.Fill;
        Cursor = Cursors.Hand;
        IsHitTestVisible = false;
        Loaded += (_, _) => EnsureBitmap();
    }

    public void ResetMask() => EnsureBitmap(force: true);

    public void ErasePoints(IEnumerable<Point> points, bool checkCompletion = true)
    {
        EnsureBitmap();
        if (_bitmap is null || _pixels is null)
            return;

        var changed = false;
        foreach (var point in points)
            changed |= EraseCircle(point);

        if (!changed)
            return;

        FlushPixels();
        if (checkCompletion)
            CheckCompletion();
    }

    public void CheckCompletion()
    {
        if (_completionReported || _pixelWidth <= 0 || _pixelHeight <= 0)
            return;

        var total = (long)_pixelWidth * _pixelHeight;
        if (total > 0 && (double)_erasedPixels / total >= CompletionThreshold)
            ReportCompletionOnce();
    }

    public void RevealAll()
    {
        EnsureBitmap();
        if (_bitmap is null || _pixels is null)
            return;

        for (var i = 3; i < _pixels.Length; i += 4)
            _pixels[i] = 0;

        _erasedPixels = (long)_pixelWidth * _pixelHeight;
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
        _erasedPixels = 0;
        _completionReported = false;

        _bitmap = new WriteableBitmap(
            _pixelWidth,
            _pixelHeight,
            96,
            96,
            PixelFormats.Bgra32,
            null);
        FlushPixels();
        Source = _bitmap;
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
        {
            for (var x = 0; x < width; x++)
            {
                var offset = y * stride + x * 4;
                var variation = (byte)((x * 13 + y * 7) % 18);
                pixels[offset + 0] = (byte)(165 + variation);
                pixels[offset + 1] = (byte)(165 + variation);
                pixels[offset + 2] = (byte)(165 + variation);
                pixels[offset + 3] = 255;
            }
        }
        return pixels;
    }

    private bool EraseCircle(Point point)
    {
        if (_pixels is null)
            return false;

        var radius = Math.Max(1, (int)Math.Round(BrushRadius));
        var centerX = (int)Math.Round(point.X);
        var centerY = (int)Math.Round(point.Y);
        var minX = Math.Max(0, centerX - radius);
        var maxX = Math.Min(_pixelWidth - 1, centerX + radius);
        var minY = Math.Max(0, centerY - radius);
        var maxY = Math.Min(_pixelHeight - 1, centerY + radius);
        var r2 = radius * radius;
        var changed = false;

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var ox = x - centerX;
                var oy = y - centerY;
                if (ox * ox + oy * oy > r2)
                    continue;

                var alphaIndex = y * _stride + x * 4 + 3;
                if (_pixels[alphaIndex] == 0)
                    continue;

                _pixels[alphaIndex] = 0;
                _erasedPixels++;
                changed = true;
            }
        }

        return changed;
    }

    private void FlushPixels()
    {
        if (_bitmap is null || _pixels is null)
            return;

        _bitmap.WritePixels(
            new Int32Rect(0, 0, _pixelWidth, _pixelHeight),
            _pixels,
            _stride,
            0);
    }

    private void ReportCompletionOnce()
    {
        if (_completionReported)
            return;
        _completionReported = true;
        Completed?.Invoke(this, EventArgs.Empty);
    }
}
