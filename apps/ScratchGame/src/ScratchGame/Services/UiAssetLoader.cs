using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace ScratchGame.Services;

public static class UiAssetLoader
{
    public static string UiPath(string fileName)
        => Path.Combine(AppContext.BaseDirectory, "UI", fileName);

    public static string TicketPath(string ticketFolder, string fileName)
        => Path.Combine(AppContext.BaseDirectory, "Tickets", ticketFolder, fileName);

    public static void TrySetImage(Image target, string path)
    {
        if (!File.Exists(path))
            return;

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        target.Source = bitmap;
    }
}
