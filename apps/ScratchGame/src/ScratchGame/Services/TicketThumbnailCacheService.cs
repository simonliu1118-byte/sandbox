using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScratchGame.Data;
using ScratchGame.Models;

namespace ScratchGame.Services;

/// <summary>
/// Runtime-only derived preview cache for installed ScratchPacks.
/// The cache is never authoritative: installed Pack/runtime definitions remain the source of truth.
/// </summary>
public sealed class TicketThumbnailCacheService
{
    public const int ThumbnailWidth = 360;
    public const int ThumbnailHeight = 294;

    // Bump only when the thumbnail rendering contract changes and old cache files should be rebuilt.
    private const int CacheRendererVersion = 1;
    private const double CanvasWidth = 1080.0;
    private const double CanvasHeight = 882.0;

    private readonly AppDatabase _database;
    private readonly ScratchPackRuntimeService _runtime;

    public TicketThumbnailCacheService(AppDatabase database)
        : this(new ScratchPackRuntimeService(database))
    {
    }

    public TicketThumbnailCacheService(ScratchPackRuntimeService runtime)
    {
        _runtime = runtime;
        _database = runtime.Database;
    }

    public string GetOrCreate(TicketDefinition ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket.SourcePackageId))
            throw new InvalidOperationException("只有 ScratchPack 彩券使用 runtime thumbnail cache。");

        var path = GetCachePath(ticket.SourcePackageId);
        if (IsUsableCache(path))
            return path;

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        void Render()
        {
            var resolved = _runtime.Load(ticket);
            RenderScratchPackThumbnail(resolved, ticket.Price, path);
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
            dispatcher.Invoke(Render);
        else
            Render();

        if (!IsUsableCache(path))
            throw new InvalidDataException("挑券縮圖產生完成後無法正常解碼。");

        return path;
    }

    public async Task TryEnsureForTicketIdAsync(
        string ticketId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var ticket = await new CatalogService(_database).GetTicketByIdAsync(ticketId, cancellationToken);
            if (ticket is null || string.IsNullOrWhiteSpace(ticket.SourcePackageId))
                return;
            _ = GetOrCreate(ticket);
        }
        catch (Exception ex)
        {
            // Thumbnail is a disposable cache. It must never make an otherwise valid Pack install fail.
            RuntimeAssetLog.Error(ticketId, "ticket thumbnail cache", ex);
        }
    }

    public string GetCachePath(string packageId)
    {
        var safeId = packageId.Trim().ToLowerInvariant();
        return Path.Combine(
            _database.DataDirectory,
            "cache",
            "thumbnails",
            $"v{CacheRendererVersion}",
            safeId + ".png");
    }

    private static bool IsUsableCache(string path)
    {
        if (!File.Exists(path))
            return false;

        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            var frame = decoder.Frames.FirstOrDefault();
            return frame is not null &&
                   frame.PixelWidth == ThumbnailWidth &&
                   frame.PixelHeight == ThumbnailHeight;
        }
        catch
        {
            try { File.Delete(path); } catch { }
            return false;
        }
    }

    private static void RenderScratchPackThumbnail(
        ResolvedScratchPackTicket resolved,
        long price,
        string outputPath)
    {
        var background = LoadBitmap(resolved.TicketImagePath);
        var foil = LoadBitmap(resolved.FoilImagePath);
        var scaleX = ThumbnailWidth / CanvasWidth;
        var scaleY = ThumbnailHeight / CanvasHeight;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(background, new Rect(0, 0, ThumbnailWidth, ThumbnailHeight));

            foreach (var zone in resolved.Definition.Zones)
            {
                var rect = new Rect(
                    zone.X * scaleX,
                    zone.Y * scaleY,
                    zone.Width * scaleX,
                    zone.Height * scaleY);

                var clip = CreateZoneGeometry(zone, rect, scaleX, scaleY);
                dc.PushClip(clip);
                // ScratchSurface uses Stretch.Fill. Thumbnail must represent the same runtime foil treatment.
                dc.DrawImage(foil, rect);
                dc.Pop();
            }

            if (resolved.Definition.PriceDisplay && resolved.Definition.PriceDisplayArea is { } area)
            {
                DrawPriceBadge(
                    dc,
                    new Rect(
                        area.X * scaleX,
                        area.Y * scaleY,
                        area.Width * scaleX,
                        area.Height * scaleY),
                    price,
                    scaleX,
                    scaleY);
            }
        }

        var bitmap = new RenderTargetBitmap(
            ThumbnailWidth,
            ThumbnailHeight,
            96,
            96,
            PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        var tempPath = outputPath + ".tmp";
        try
        {
            using (var stream = File.Create(tempPath))
                encoder.Save(stream);

            File.Move(tempPath, outputPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
        }
    }

    private static BitmapSource LoadBitmap(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("找不到縮圖所需美術資源。", path);

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static Geometry CreateZoneGeometry(
        ScratchPackZone zone,
        Rect rect,
        double scaleX,
        double scaleY)
    {
        return zone.Shape switch
        {
            "circle" or "ellipse" => new EllipseGeometry(rect),
            "roundedRectangle" => new RectangleGeometry(
                rect,
                (zone.CornerRadius ?? 0) * scaleX,
                (zone.CornerRadius ?? 0) * scaleY),
            _ => new RectangleGeometry(rect)
        };
    }

    private static void DrawPriceBadge(
        DrawingContext dc,
        Rect rect,
        long price,
        double scaleX,
        double scaleY)
    {
        var unit = Math.Min(scaleX, scaleY);
        var outerRadius = Math.Min(16.0, rect.Height / scaleY / 4.0) * unit;
        var innerRadius = Math.Min(12.0, rect.Height / scaleY / 5.0) * unit;
        var outerPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 225, 124)), 3 * unit);
        var innerPen = new Pen(new SolidColorBrush(Color.FromRgb(150, 77, 18)), 2 * unit);

        dc.DrawRoundedRectangle(
            new SolidColorBrush(Color.FromRgb(181, 119, 25)),
            outerPen,
            rect,
            outerRadius,
            outerRadius);

        var inset = 5 * unit;
        var inner = new Rect(
            rect.X + inset,
            rect.Y + inset,
            Math.Max(1, rect.Width - inset * 2),
            Math.Max(1, rect.Height - inset * 2));
        dc.DrawRoundedRectangle(
            new SolidColorBrush(Color.FromRgb(255, 246, 207)),
            innerPen,
            inner,
            innerRadius,
            innerRadius);

        var sourceHeight = rect.Height / scaleY;
        var sourceFontSize = Math.Clamp(sourceHeight * 0.43, 24, 38);
        var text = new FormattedText(
            $"NT${price:N0}",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(
                new FontFamily("Microsoft JhengHei UI"),
                FontStyles.Normal,
                FontWeights.Black,
                FontStretches.Normal),
            sourceFontSize * unit,
            new SolidColorBrush(Color.FromRgb(196, 25, 28)),
            1.0);

        dc.DrawText(
            text,
            new Point(
                rect.X + (rect.Width - text.Width) / 2.0,
                rect.Y + (rect.Height - text.Height) / 2.0));
    }
}
