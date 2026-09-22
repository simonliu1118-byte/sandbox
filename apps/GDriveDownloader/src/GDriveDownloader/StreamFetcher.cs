using System.Diagnostics;
using System.Drawing;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace GDriveDownloader;

internal enum FetchResult
{
    Success,
    HttpError,
    Timeout,
    Cancelled,
    SlowAborted,
}

internal sealed class FetchOutcome
{
    public required FetchResult Result { get; init; }

    public string? ErrorMessage { get; init; }

    public long BytesWritten { get; init; }
}

internal sealed class StreamFetcher : IDisposable
{
    private readonly WebView2 _webView = new();
    private readonly Form _hostForm;
    private DevToolsSession? _dts;

    public StreamFetcher()
    {
        _hostForm = new Form
        {
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.FixedToolWindow,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-3000, -3000),
            Size = new Size(400, 300),
        };
        _hostForm.Controls.Add(_webView);
        _hostForm.Show();
    }

    public async Task InitializeAsync()
    {
        if (_dts != null)
        {
            return;
        }

        var env = await CoreWebView2Environment.CreateAsync(userDataFolder: AppPaths.WebView2ProfileDir);
        await _webView.EnsureCoreWebView2Async(env);
        _dts = new DevToolsSession(_webView.CoreWebView2);
    }

    public async Task<FetchOutcome> DownloadAsync(
        string url,
        string destinationPath,
        Action<long, double>? onProgress,
        int minAcceptableBytesPerSecond,
        TimeSpan warmupBeforeSpeedCheck,
        CancellationToken token)
    {
        await InitializeAsync();

        await _dts!.SendAsync("Network.enable");
        await _dts.SendAsync("Fetch.enable", new
        {
            patterns = new[] { new { urlPattern = "*videoplayback*", requestStage = "Response" } },
        });

        var tcs = new TaskCompletionSource<FetchOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = false;

        using var subscription = _dts.Subscribe("Fetch.requestPaused", async evt =>
        {
            if (started)
            {
                return;
            }

            try
            {
                var requestUrl = evt.GetProperty("request").GetProperty("url").GetString() ?? string.Empty;
                if (!requestUrl.Contains("videoplayback", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var requestId = evt.GetProperty("requestId").GetString()!;
                var status = evt.TryGetProperty("responseStatusCode", out var statusProp) ? statusProp.GetInt32() : 0;

                if (status is >= 300 and < 400)
                {
                    await _dts.SendAsync("Fetch.continueRequest", new { requestId });
                    return;
                }

                if (status is 0 or >= 400)
                {
                    started = true;
                    tcs.TrySetResult(new FetchOutcome { Result = FetchResult.HttpError, ErrorMessage = $"HTTP {status}" });
                    return;
                }

                started = true;
                var streamResult = await _dts.SendAsync("Fetch.takeResponseBodyAsStream", new { requestId });
                var handle = streamResult.GetProperty("stream").GetString()!;
                var outcome = await PumpStreamAsync(handle, destinationPath, onProgress, minAcceptableBytesPerSecond, warmupBeforeSpeedCheck, token);
                tcs.TrySetResult(outcome);
            }
            catch (Exception ex)
            {
                tcs.TrySetResult(new FetchOutcome { Result = FetchResult.HttpError, ErrorMessage = ex.Message });
            }
        });

        using var registration = token.Register(() => tcs.TrySetResult(new FetchOutcome { Result = FetchResult.Cancelled }));

        _webView.CoreWebView2.Navigate(url);

        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30), CancellationToken.None);
        var completed = await Task.WhenAny(tcs.Task, timeoutTask);
        if (completed == timeoutTask && !tcs.Task.IsCompleted)
        {
            tcs.TrySetResult(new FetchOutcome { Result = FetchResult.Timeout, ErrorMessage = "等待回應逾時" });
        }

        try
        {
            await _dts.SendAsync("Fetch.disable");
        }
        catch
        {
            // best effort cleanup
        }

        return await tcs.Task;
    }

    private async Task<FetchOutcome> PumpStreamAsync(
        string handle,
        string destinationPath,
        Action<long, double>? onProgress,
        int minAcceptableBytesPerSecond,
        TimeSpan warmupBeforeSpeedCheck,
        CancellationToken token)
    {
        long total = 0;
        var stopwatch = Stopwatch.StartNew();
        var checkpointBytes = 0L;
        var checkpointTime = TimeSpan.Zero;
        var checkpointStarted = false;

        await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);

        while (true)
        {
            if (token.IsCancellationRequested)
            {
                await CloseStreamHandleAsync(handle);
                return new FetchOutcome { Result = FetchResult.Cancelled, BytesWritten = total };
            }

            var chunk = await _dts!.SendAsync("IO.read", new { handle, size = 1048576 });
            var eof = chunk.TryGetProperty("eof", out var eofProp) && eofProp.GetBoolean();
            var base64Encoded = chunk.TryGetProperty("base64Encoded", out var b64Prop) && b64Prop.GetBoolean();
            var data = chunk.TryGetProperty("data", out var dataProp) ? dataProp.GetString() ?? string.Empty : string.Empty;

            if (data.Length > 0)
            {
                var bytes = base64Encoded ? Convert.FromBase64String(data) : System.Text.Encoding.UTF8.GetBytes(data);
                await fileStream.WriteAsync(bytes, token);
                total += bytes.Length;
                onProgress?.Invoke(total, stopwatch.Elapsed.TotalSeconds);
            }

            if (minAcceptableBytesPerSecond > 0)
            {
                if (!checkpointStarted && stopwatch.Elapsed >= warmupBeforeSpeedCheck)
                {
                    checkpointStarted = true;
                    checkpointBytes = total;
                    checkpointTime = stopwatch.Elapsed;
                }
                else if (checkpointStarted && stopwatch.Elapsed - checkpointTime >= TimeSpan.FromSeconds(5))
                {
                    var intervalBytes = total - checkpointBytes;
                    var intervalSeconds = (stopwatch.Elapsed - checkpointTime).TotalSeconds;
                    var bytesPerSecond = intervalSeconds > 0 ? intervalBytes / intervalSeconds : 0;

                    if (bytesPerSecond < minAcceptableBytesPerSecond)
                    {
                        await CloseStreamHandleAsync(handle);
                        return new FetchOutcome { Result = FetchResult.SlowAborted, BytesWritten = total };
                    }

                    checkpointBytes = total;
                    checkpointTime = stopwatch.Elapsed;
                }
            }

            if (eof)
            {
                await CloseStreamHandleAsync(handle);
                return new FetchOutcome { Result = FetchResult.Success, BytesWritten = total };
            }
        }
    }

    private async Task CloseStreamHandleAsync(string handle)
    {
        try
        {
            await _dts!.SendAsync("IO.close", new { handle });
        }
        catch
        {
            // best effort
        }
    }

    public void Dispose()
    {
        _hostForm.Close();
        _hostForm.Dispose();
    }
}
