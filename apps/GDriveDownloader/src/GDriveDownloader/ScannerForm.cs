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
    private readonly Action<string>? _log;
    private readonly TaskCompletionSource<bool> _readySignal = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private DevToolsSession? _dts;
    private int _totalRequestCount;
    private readonly HashSet<string> _seenHosts = new();
    private readonly List<string> _nearMissUrls = new();
    private readonly Dictionary<string, string> _pendingBodyRequests = new();
    private readonly List<string> _capturedBodies = new();

    public ScannerForm(string fileId, TimeSpan collectWindow, Action<string>? log = null)
    {
        _fileId = fileId;
        _collectWindow = collectWindow;
        _log = log;

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
        _webView.CoreWebView2.Settings.UserAgent = BrowserIdentity.DesktopChromeUserAgent;

        _dts = new DevToolsSession(_webView.CoreWebView2);

        // WebResourceRequested is a native WebView2 hook that covers the whole page
        // including cross-origin iframes/workers (Google Drive's video player runs
        // inside an embedded, possibly cross-origin iframe). A raw CDP Network
        // session on the top-level target alone does not reliably see that traffic.
        _webView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        _webView.CoreWebView2.WebResourceRequested += OnWebResourceRequested;

        // Also capture response BODIES for requests that look like Drive's newer
        // "workspacevideo" playback-info API, since that endpoint returns metadata
        // (likely containing the real media URLs) rather than being the media itself.
        await _dts.SendAsync("Network.enable");
        _dts.Subscribe("Network.responseReceived", OnNetworkResponseReceived);
        _dts.Subscribe("Network.loadingFinished", OnNetworkLoadingFinished);

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
            // Fall through to the synthetic click below (the video may live in a cross-origin iframe).
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

    private void OnNetworkResponseReceived(JsonElement evt)
    {
        try
        {
            var response = evt.GetProperty("response");
            var url = response.GetProperty("url").GetString() ?? string.Empty;

            if (!LooksLikePlaybackApi(url))
            {
                return;
            }

            var requestId = evt.GetProperty("requestId").GetString();
            if (requestId is null)
            {
                return;
            }

            lock (_pendingBodyRequests)
            {
                _pendingBodyRequests[requestId] = url;
            }
        }
        catch
        {
            // Malformed/unexpected event payload; skip this one.
        }
    }

    private async void OnNetworkLoadingFinished(JsonElement evt)
    {
        try
        {
            var requestId = evt.TryGetProperty("requestId", out var idProp) ? idProp.GetString() : null;
            if (requestId is null)
            {
                return;
            }

            string? url;
            lock (_pendingBodyRequests)
            {
                if (!_pendingBodyRequests.Remove(requestId, out url))
                {
                    return;
                }
            }

            var bodyResult = await _dts!.SendAsync("Network.getResponseBody", new { requestId });
            var base64Encoded = bodyResult.TryGetProperty("base64Encoded", out var b64Prop) && b64Prop.GetBoolean();
            var rawBody = bodyResult.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? string.Empty : string.Empty;
            var body = base64Encoded ? System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(rawBody)) : rawBody;

            var truncated = body.Length > 4000 ? body[..4000] + "...(截斷)" : body;

            lock (_capturedBodies)
            {
                _capturedBodies.Add($"URL: {url}\n內容：{truncated}");
            }

            ParsePlaybackApiResponse(body);
        }
        catch (Exception ex)
        {
            lock (_capturedBodies)
            {
                _capturedBodies.Add($"（讀取回應內容失敗：{ex.Message}）");
            }
        }
    }

    private static bool LooksLikePlaybackApi(string url)
    {
        return url.Contains("workspacevideo", StringComparison.OrdinalIgnoreCase) ||
               (url.Contains("clients6.google.com", StringComparison.OrdinalIgnoreCase) && url.Contains("playback", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Drive's "workspacevideo" playback API responds with
    /// {"mediaStreamingData":{"serializedHouseBrandPlayerResponse":"&lt;JSON string&gt;"}}
    /// where the inner JSON string has the classic YouTube-shaped
    /// streamingData.formats / streamingData.adaptiveFormats arrays, each entry
    /// carrying an itag and a real videoplayback URL. Parse that directly instead
    /// of waiting to passively see individual videoplayback network requests.
    /// </summary>
    private void ParsePlaybackApiResponse(string body)
    {
        try
        {
            using var outerDoc = JsonDocument.Parse(body);
            if (!outerDoc.RootElement.TryGetProperty("mediaStreamingData", out var mediaStreamingData))
            {
                return;
            }

            if (!mediaStreamingData.TryGetProperty("serializedHouseBrandPlayerResponse", out var serializedProp))
            {
                return;
            }

            var serialized = serializedProp.GetString();
            if (string.IsNullOrEmpty(serialized))
            {
                return;
            }

            using var innerDoc = JsonDocument.Parse(serialized);
            if (!innerDoc.RootElement.TryGetProperty("streamingData", out var streamingData))
            {
                return;
            }

            AddFormatsFromArray(streamingData, "formats");
            AddFormatsFromArray(streamingData, "adaptiveFormats");
        }
        catch
        {
            // Response body didn't match the expected schema; leave candidates as-is.
        }
    }

    private void AddFormatsFromArray(JsonElement streamingData, string propertyName)
    {
        if (!streamingData.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var entry in array.EnumerateArray())
        {
            try
            {
                if (!entry.TryGetProperty("itag", out var itagProp) || !entry.TryGetProperty("url", out var urlProp))
                {
                    continue;
                }

                var itag = itagProp.GetInt32();
                var url = urlProp.GetString();
                if (string.IsNullOrEmpty(url))
                {
                    continue;
                }

                var mimeType = entry.TryGetProperty("mimeType", out var mimeProp) ? mimeProp.GetString() : null;
                var info = ItagCatalog.Resolve(itag, mimeType);
                if (info.Kind == StreamKind.Unknown)
                {
                    continue;
                }

                var cleanedUrl = CleanUrl(url);

                lock (_candidates)
                {
                    if (_candidates.Any(c => c.Url == cleanedUrl))
                    {
                        continue;
                    }

                    _candidates.Add(new VideoSourceCandidate
                    {
                        Url = cleanedUrl,
                        Kind = info.Kind,
                        Height = info.Height,
                        Label = info.Label,
                        Itag = itag,
                        MimeType = mimeType,
                    });
                }
            }
            catch
            {
                // Skip malformed entries.
            }
        }
    }

    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        try
        {
            var url = e.Request.Uri;

            _totalRequestCount++;
            if (Uri.TryCreate(url, UriKind.Absolute, out var parsed))
            {
                lock (_seenHosts)
                {
                    _seenHosts.Add(parsed.Host);
                }
            }

            if (!url.Contains("videoplayback", StringComparison.OrdinalIgnoreCase))
            {
                if (url.Contains("playback", StringComparison.OrdinalIgnoreCase) ||
                    url.Contains("videodownload", StringComparison.OrdinalIgnoreCase) ||
                    url.Contains(".googlevideo.com", StringComparison.OrdinalIgnoreCase))
                {
                    lock (_nearMissUrls)
                    {
                        if (_nearMissUrls.Count < 10)
                        {
                            _nearMissUrls.Add(url);
                        }
                    }
                }

                return;
            }

            var itag = ExtractItag(url);
            var info = itag.HasValue
                ? ItagCatalog.Resolve(itag.Value, null)
                : new ItagInfo(StreamKind.Unknown, null, "未知來源");

            if (info.Kind == StreamKind.Unknown)
            {
                lock (_nearMissUrls)
                {
                    if (_nearMissUrls.Count < 10)
                    {
                        _nearMissUrls.Add($"[itag 無法辨識] {url}");
                    }
                }

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
                    MimeType = null,
                });
            }
        }
        catch
        {
            // Malformed/unexpected request; skip this one.
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

    /// <summary>
    /// Signals initial readiness after the first collection window, but does NOT
    /// close the window or stop listening: the video keeps playing muted in the
    /// background for the rest of the download session, which naturally produces
    /// more (and sometimes differently-edge-routed) videoplayback requests over
    /// time. The caller re-reads <see cref="Candidates"/> later to pick up
    /// anything new instead of re-opening a fresh scan from scratch.
    /// </summary>
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

        var hostSample = string.Join(", ", _seenHosts.Take(15));
        _log?.Invoke($"[診斷] 共攔截到 {_totalRequestCount} 筆請求，涉及 {_seenHosts.Count} 個 host：{hostSample}");
        if (_nearMissUrls.Count > 0)
        {
            _log?.Invoke($"[診斷] 疑似相關但未辨識的網址（最多列 10 筆）：");
            foreach (var nearMiss in _nearMissUrls)
            {
                _log?.Invoke($"[診斷]   {nearMiss}");
            }
        }

        if (_capturedBodies.Count > 0)
        {
            _log?.Invoke($"[診斷] 抓到 {_capturedBodies.Count} 筆疑似播放 API 的回應內容：");
            foreach (var body in _capturedBodies)
            {
                _log?.Invoke($"[診斷回應內容] {body}");
            }
        }

        if (!IsDisposed)
        {
            BeginInvoke(new Action(() => Text = "背景播放中（協助尋找下載來源，下載完成後會自動關閉）"));
        }

        _readySignal.TrySetResult(true);
    }
}
