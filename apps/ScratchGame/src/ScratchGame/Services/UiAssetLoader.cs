using System.Reflection;
using System.Text;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace ScratchGame.Services;

public static class UiAssetLoader
{
    public const int HeaderWidth = 1920;
    public const int HeaderHeight = 144;
    public const int StageWidth = 1920;
    public const int StageHeight = 900;
    public const int FooterWidth = 1920;
    public const int FooterHeight = 156;

    public static string UiPath(string fileName)
        => Path.Combine(AppContext.BaseDirectory, "UI", fileName);

    public static string TicketPath(string ticketFolder, string fileName)
        => Path.Combine(AppContext.BaseDirectory, "Tickets", ticketFolder, fileName);

    public static void ApplyMainWindowBackgrounds(Image header, Image stage, Image footer)
    {
        ApplyFrameTheme(header, footer, externalThemeDirectory: null);
        ApplyStageTheme(stage, externalThemeDirectory: null);
    }

    public static bool ApplyFrameTheme(Image header, Image footer, string? externalThemeDirectory)
    {
        BitmapImage? headerBitmap = null;
        BitmapImage? footerBitmap = null;

        if (!string.IsNullOrWhiteSpace(externalThemeDirectory))
        {
            headerBitmap = TryLoadFile(Path.Combine(externalThemeDirectory, "header_bg.png"), HeaderWidth, HeaderHeight);
            footerBitmap = TryLoadFile(Path.Combine(externalThemeDirectory, "footer_bg.png"), FooterWidth, FooterHeight);
        }

        // Header + Footer are one atomic Frame Theme. Never mix an external half with builtin half.
        if (headerBitmap is null || footerBitmap is null)
        {
            headerBitmap = TryLoadEmbedded("Header", HeaderWidth, HeaderHeight);
            footerBitmap = TryLoadEmbedded("Footer", FooterWidth, FooterHeight);
        }

        if (headerBitmap is null || footerBitmap is null)
            return false;

        header.Source = headerBitmap;
        footer.Source = footerBitmap;
        return true;
    }

    public static bool ApplyStageTheme(Image stage, string? externalThemeDirectory)
    {
        BitmapImage? bitmap = null;
        if (!string.IsNullOrWhiteSpace(externalThemeDirectory))
            bitmap = TryLoadFile(Path.Combine(externalThemeDirectory, "stage_bg.png"), StageWidth, StageHeight);

        bitmap ??= TryLoadEmbedded("Stage", StageWidth, StageHeight);
        if (bitmap is null)
            return false;

        stage.Source = bitmap;
        return true;
    }

    public static void TrySetImage(Image target, string path)
    {
        var bitmap = TryLoadFile(path, expectedWidth: null, expectedHeight: null);
        if (bitmap is not null)
        {
            target.Source = bitmap;
            return;
        }

        // Compatibility path for the existing MainWindow constructor: if no loose legacy UI file
        // exists, fall back to the embedded shipping defaults.
        var fileName = Path.GetFileName(path);
        bitmap = fileName switch
        {
            "topbar_bg.png" => TryLoadEmbedded("Header", HeaderWidth, HeaderHeight),
            "stage_bg.png" => TryLoadEmbedded("Stage", StageWidth, StageHeight),
            "footer_bg.png" => TryLoadEmbedded("Footer", FooterWidth, FooterHeight),
            _ => null
        };

        if (bitmap is not null)
            target.Source = bitmap;
    }

    private static BitmapImage? TryLoadEmbedded(string assetName, int expectedWidth, int expectedHeight)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var suffix = $".Assets.EmbeddedUI.{assetName}.";
            var names = assembly.GetManifestResourceNames()
                .Where(name => name.Contains(suffix, StringComparison.Ordinal) && name.EndsWith(".b64", StringComparison.OrdinalIgnoreCase))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            if (names.Length == 0)
                return null;

            var encoded = new StringBuilder();
            foreach (var name in names)
            {
                using var stream = assembly.GetManifestResourceStream(name);
                if (stream is null)
                    return null;
                using var reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false);
                encoded.Append(reader.ReadToEnd());
            }

            var bytes = Convert.FromBase64String(encoded.ToString());
            using var imageStream = new MemoryStream(bytes, writable: false);
            return LoadBitmap(imageStream, expectedWidth, expectedHeight);
        }
        catch
        {
            return null;
        }
    }

    private static BitmapImage? TryLoadFile(string path, int? expectedWidth, int? expectedHeight)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            using var stream = File.OpenRead(path);
            return LoadBitmap(stream, expectedWidth, expectedHeight);
        }
        catch
        {
            return null;
        }
    }

    private static BitmapImage? LoadBitmap(Stream stream, int? expectedWidth, int? expectedHeight)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();

        if (expectedWidth.HasValue && bitmap.PixelWidth != expectedWidth.Value)
            return null;
        if (expectedHeight.HasValue && bitmap.PixelHeight != expectedHeight.Value)
            return null;

        return bitmap;
    }
}
