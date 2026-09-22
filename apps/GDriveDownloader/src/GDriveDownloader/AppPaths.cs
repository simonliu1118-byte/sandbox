namespace GDriveDownloader;

internal static class AppPaths
{
    public static string RootDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GDriveDownloader");

    public static string ToolsDir { get; } = Path.Combine(RootDir, "tools");

    public static string WebView2ProfileDir { get; } = Path.Combine(RootDir, "WebView2Profile");

    public static string DefaultDownloadDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Downloads",
        "GDriveDownloader");

    public static string FfmpegExe { get; } = Path.Combine(ToolsDir, "ffmpeg.exe");

    /// <summary>ffmpeg.exe shipped alongside GDriveDownloader.exe by the release build.</summary>
    public static string BundledFfmpegExe { get; } = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(RootDir);
        Directory.CreateDirectory(ToolsDir);
        Directory.CreateDirectory(WebView2ProfileDir);
        Directory.CreateDirectory(DefaultDownloadDir);
    }
}
