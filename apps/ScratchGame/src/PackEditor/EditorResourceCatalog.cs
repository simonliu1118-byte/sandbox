using System.IO;
using System.Windows.Media.Imaging;
using ScratchGame.Services;

namespace PackEditor;

internal static class EditorResourceCatalog
{
    public static IReadOnlyList<string> GetBuiltInTicketRefs(string gameType)
    {
        var directory = Path.Combine(
            AppContext.BaseDirectory,
            "BuiltInAssets",
            "Tickets",
            $"gameType{gameType}");

        if (!Directory.Exists(directory))
            return Array.Empty<string>();

        return Directory.EnumerateFiles(directory, "*.png", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .Select(reference => reference!)
            .Where(reference => ScratchPackV1Loader.IsBuiltInTicketSupported(gameType, reference))
            .OrderBy(reference => reference, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<string> GetBuiltInFoilRefs()
    {
        var directory = Path.Combine(
            AppContext.BaseDirectory,
            "BuiltInAssets",
            "Foils");

        if (!Directory.Exists(directory))
            return Array.Empty<string>();

        return Directory.EnumerateFiles(directory, "*.png", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .Select(reference => reference!)
            .Where(ScratchPackV1Loader.IsBuiltInFoilSupported)
            .OrderBy(reference => reference, StringComparer.Ordinal)
            .ToArray();
    }

    public static BitmapImage LoadBitmap(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("找不到 PNG 資源。", path);
        if (!string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"資源必須是 PNG：{Path.GetFileName(path)}");

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(Path.GetFullPath(path), UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    public static (int Width, int Height) ReadPngDimensions(string path)
    {
        var bitmap = LoadBitmap(path);
        return (bitmap.PixelWidth, bitmap.PixelHeight);
    }
}
