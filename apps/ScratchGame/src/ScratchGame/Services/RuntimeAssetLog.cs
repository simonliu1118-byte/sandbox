namespace ScratchGame.Services;

public static class RuntimeAssetLog
{
    private static readonly object Sync = new();

    public static void Missing(string path, string kind)
        => Write($"MISSING {kind}: {path}");

    public static void Error(string path, string kind, Exception ex)
        => Write($"ERROR {kind}: {path} | {ex.GetType().Name}: {ex.Message}");

    private static void Write(string message)
    {
        try
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ScratchGame",
                "logs");
            Directory.CreateDirectory(root);
            var logPath = Path.Combine(root, "runtime-assets.log");
            lock (Sync)
                File.AppendAllText(logPath, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
        catch
        {
            // Asset diagnostics must never crash gameplay.
        }
    }
}
