using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace ScratchGame.Services;

public static class UiAssetLoader
{
    public static string ThemeFramePath(string fileName)
        => Path.Combine(AppContext.BaseDirectory, "Themes", "Default", "Frame", fileName);

    public static string ThemeStagePath(string fileName)
        => Path.Combine(AppContext.BaseDirectory, "Themes", "Default", "Stage", fileName);

    // Compatibility entry point used by existing MainWindow code. Theme art is external-only.
    public static string UiPath(string fileName)
        => fileName.ToLowerInvariant() switch
        {
            "topbar_bg.png" or "header_bg.png" => ThemeFramePath("header_bg.png"),
            "footer_bg.png" => ThemeFramePath("footer_bg.png"),
            "stage_bg.png" => ThemeStagePath("stage_bg.png"),
            _ => Path.Combine(AppContext.BaseDirectory, "UI", fileName)
        };

    public static string TicketPath(string ticketFolder, string fileName)
        => Path.Combine(AppContext.BaseDirectory, "Tickets", ticketFolder, fileName);

    public static void TrySetImage(Image target, string path)
    {
        if (!File.Exists(path))
        {
            RuntimeAssetLog.Missing(path, "image");
            target.Source = null;
            return;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            target.Source = bitmap;
        }
        catch (Exception ex)
        {
            RuntimeAssetLog.Error(path, "image", ex);
            target.Source = null;
        }
    }
}
