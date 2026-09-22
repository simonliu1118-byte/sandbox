namespace GDriveDownloader;

internal static class BrowserIdentity
{
    // WebView2's default UA includes an "Edg/" token identifying it as an embedded
    // browser control, which appears to make Google Drive serve a different video
    // backend (the newer "workspacevideo" playback API) than a plain desktop Chrome
    // gets (the classic videoplayback CDN, confirmed working via manual testing).
    // Presenting as ordinary desktop Chrome everywhere (login included, so the
    // session's fingerprint stays consistent) steers Drive back onto that path.
    public const string DesktopChromeUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36";
}
