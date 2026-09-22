using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace GDriveDownloader;

internal sealed class ScannerForm : Form
{
    private readonly WebView2 _webView = new() { Dock = DockStyle.Fill };
    private readonly List<VideoSourceCandidate> _candidates = new();
    private readonly string _fileId;
    private readonly TimeSpan _collectWindow;
    private readonly TaskCompletionSource<bool> _readySignal = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private DevToolsSession? _dts;
    private IDisposable? _subscription;

    public ScannerForm(string fileId, TimeSpan collectWindow)
    {
        _fileId = fileId;
        _collectWindow = collectWindow;

        Text = "正在分析影片畫質...";
        Width = 900;
        Height = 650;
        StartPosition = FormStartPosition.CenterScreen;

        Controls.Add(_webView);
        Load += async (_, _) => await InitializeAsync();
    }

    public IReadOnlyList<VideoSourceCandidate> Candidates => _candidates;

    public string? VideoTitle { get; private set; }

    public Task WaitUntilDoneAsync() => _readySignal.Task;

    private async Task InitializeAsync()
    {
        var env = await CoreWebView2Environment.CreateAsync(userDataFolder: AppPaths.WebView2ProfileDir);
        await _webView.EnsureCoreWebView2Async(env);

        _dts = new DevToolsSession(_webView.CoreWebView2);
        await _dts.SendAsync("Network.enable");
        _subscription = _dts.Subscribe("Network.responseReceived", OnResponseReceived);

        _webView.CoreWebView2.NavigationCompleted += async (_, e) =>
        {
            if (!e.IsSuccess)
            {
                return;
            }

            await Task.Delay(2500);
            await TryStartPlaybackAsync();
        };

        _webView.CoreWebView2.Navigate($"https://drive.google.com/file/d/{_fileId}/view");

        _ = RunCollectionWindowAsync();
    }

    private async Task TryStartPlaybackAsync()
    {
        try
        {
            await _webView.CoreWebView2.ExecuteScriptAsync(
                "document.querySelectorAll('video').forEach(v => { v.muted = true; v.play().catch(() => {}); });");
        }
        catch
        {
            // Fall through to the synthetic click below.
        }

        try
        {
            var x = _webView.ClientSize.Width / 2;
            var y = _webView.ClientSize.Height / 2;

            await _dts!.SendAsync("Input.dispatchMouseEvent", new { type = "mousePressed", x, y, button = "left", clickCount = 1 });
            await _dts.SendAsync("Input.dispatchMouseEvent", new { type = "mouseReleased", x, y, button = "left", clickCount = 1 });
        }
        catch
        {
            // Best effort; some pages autoplay without needing this.
        }
    }

    private void OnResponseReceived(JsonElement evt)
    {
        try
        {
            var response = evt.GetProperty("response");
            var url = response.GetProperty("url").GetString() ?? string.Empty;
            if (!url.Contains("videoplayback", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var mimeType = response.TryGetProperty("mimeType", out var mimeProp) ? mimeProp.GetString() : null;
            var itag = ExtractItag(url);
            var info = itag.HasValue
                ? ItagCatalog.Resolve(itag.Value, mimeType)
                : new ItagInfo(ItagCatalog.GuessKindFromMime(mimeType), null, mimeType ?? "未知來源");

            if (info.Kind == StreamKind.Unknown)
            {
                return;
            }

            var cleanedUrl = CleanUrl(url);

            lock (_candidates)
            {
                if (_candidates.Any(c => c.Url == cleanedUrl))
                {
                    return;
                }

                _candidates.Add(new VideoSourceCandidate
                {
                    Url = cleanedUrl,
                    Kind = info.Kind,
                    Height = info.Height,
                    Label = info.Label,
                    Itag = itag ?? 0,
                    MimeType = mimeType,
                });
            }
        }
        catch
        {
            // Malformed/unexpected event payload; skip this one.
        }
    }

    private static int? ExtractItag(string url)
    {
        var match = Regex.Match(url, @"[?&]itag=(\d+)");
        return match.Success ? int.Parse(match.Groups[1].Value) : null;
    }

    private static string CleanUrl(string url)
    {
        var separatorIndex = url.IndexOf('?');
        if (separatorIndex < 0)
        {
            return url;
        }

        var baseUrl = url[..separatorIndex];
        var query = url[(separatorIndex + 1)..];
        var keptParams = new List<string>();

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var name = pair.Split('=', 2)[0];
            if (name is "range" or "ump")
            {
                continue;
            }

            keptParams.Add(pair);
        }

        return keptParams.Count > 0 ? $"{baseUrl}?{string.Join('&', keptParams)}" : baseUrl;
    }

    private async Task RunCollectionWindowAsync()
    {
        await Task.Delay(_collectWindow);

        try
        {
            var result = await _webView.CoreWebView2.ExecuteScriptAsync("document.title");
            var title = JsonSerializer.Deserialize<string>(result);
            if (!string.IsNullOrWhiteSpace(title))
            {
                var suffixIndex = title.IndexOf(" - Google", StringComparison.Ordinal);
                VideoTitle = suffixIndex > 0 ? title[..suffixIndex] : title;
            }
        }
        catch
        {
            // Title stays null; caller falls back to the file id.
        }

        _subscription?.Dispose();
        _readySignal.TrySetResult(true);

        if (!IsDisposed)
        {
            BeginInvoke(new Action(Close));
        }
    }
}
