using System.Diagnostics;
using System.Text.RegularExpressions;

namespace GDriveDownloader;

internal sealed class DownloadEngineResult
{
    public required bool Success { get; init; }

    public string? OutputPath { get; init; }

    public string? ErrorMessage { get; init; }

    public static DownloadEngineResult Succeeded(string path) => new() { Success = true, OutputPath = path };

    public static DownloadEngineResult Failed(string message) => new() { Success = false, ErrorMessage = message };
}

internal static class DownloadEngine
{
    private const int MinAcceptableBytesPerSecond = 50 * 1024;
    private const int MaxRescans = 2;
    private static readonly TimeSpan WarmupBeforeSpeedCheck = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan ScanWindow = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan RescanWindow = TimeSpan.FromSeconds(10);

    public static async Task<DownloadEngineResult> RunAsync(
        string driveUrl,
        string outputDirectory,
        string? ffmpegPath,
        Action<string> log,
        CancellationToken token)
    {
        var fileId = ExtractFileId(driveUrl);
        if (fileId is null)
        {
            return DownloadEngineResult.Failed("無法從連結解析出 Google Drive 檔案 ID。");
        }

        log("正在開啟 Google Drive 播放頁面並分析可用畫質...");

        List<VideoSourceCandidate> candidates;
        string title;

        using (var scanner = new ScannerForm(fileId, ScanWindow, log))
        {
            scanner.Show();
            await scanner.WaitUntilDoneAsync();
            candidates = scanner.Candidates.ToList();
            title = string.IsNullOrWhiteSpace(scanner.VideoTitle) ? fileId : scanner.VideoTitle!;
        }

        if (token.IsCancellationRequested)
        {
            return DownloadEngineResult.Failed("已取消");
        }

        if (candidates.Count == 0)
        {
            return DownloadEngineResult.Failed("沒有偵測到任何 videoplayback 來源，可能尚未登入、沒有權限，或影片無法播放。");
        }

        var videoGroups = candidates
            .Where(c => c.Kind == StreamKind.Video)
            .GroupBy(c => c.Height ?? 0)
            .OrderByDescending(g => g.Key)
            .ToList();

        if (videoGroups.Count == 0)
        {
            return DownloadEngineResult.Failed("沒有偵測到視訊來源。");
        }

        var bestVideoGroup = videoGroups[0].ToList();
        var targetHeight = bestVideoGroup[0].Height;
        var audioCandidates = candidates.Where(c => c.Kind == StreamKind.Audio).ToList();

        log($"偵測到最高畫質：{bestVideoGroup[0].Label}（{bestVideoGroup.Count} 個候選來源，另有 {audioCandidates.Count} 個音訊來源）。");

        var safeTitle = SanitizeFileName(title);
        var videoTempPath = Path.Combine(outputDirectory, $"{safeTitle}.video.tmp");
        var audioTempPath = Path.Combine(outputDirectory, $"{safeTitle}.audio.tmp");

        using var fetcher = new StreamFetcher();
        await fetcher.InitializeAsync();

        Task<List<VideoSourceCandidate>> RescanAsync(CancellationToken ct) => RescanForCandidatesAsync(fileId, log, ct);

        var videoOutcome = await DownloadWithFallbackAsync(
            fetcher,
            bestVideoGroup,
            videoTempPath,
            "視訊",
            log,
            RescanAsync,
            c => c.Kind == StreamKind.Video && c.Height == targetHeight,
            token);
        if (videoOutcome.Result is not (FetchResult.Success or FetchResult.SlowAborted))
        {
            TryDelete(videoTempPath);
            return DownloadEngineResult.Failed($"視訊下載失敗：{videoOutcome.ErrorMessage}");
        }

        string? finalAudioPath = null;
        if (audioCandidates.Count > 0)
        {
            var audioOutcome = await DownloadWithFallbackAsync(
                fetcher,
                audioCandidates,
                audioTempPath,
                "音訊",
                log,
                RescanAsync,
                c => c.Kind == StreamKind.Audio,
                token);
            if (audioOutcome.Result is FetchResult.Success or FetchResult.SlowAborted)
            {
                finalAudioPath = audioTempPath;
            }
            else
            {
                log($"音訊下載失敗，將只輸出視訊：{audioOutcome.ErrorMessage}");
                TryDelete(audioTempPath);
            }
        }

        var finalPath = MakeUniquePath(Path.Combine(outputDirectory, $"{safeTitle}.mp4"));

        if (finalAudioPath != null && ffmpegPath != null)
        {
            log("正在合併視訊與音訊...");
            var muxed = await MuxAsync(ffmpegPath, videoTempPath, finalAudioPath, finalPath, token);
            if (muxed)
            {
                TryDelete(videoTempPath);
                TryDelete(finalAudioPath);
            }
            else
            {
                log("合併失敗，改為只保留視訊檔。");
                File.Move(videoTempPath, finalPath, overwrite: false);
                TryDelete(finalAudioPath);
            }
        }
        else
        {
            File.Move(videoTempPath, finalPath, overwrite: false);
            if (finalAudioPath != null)
            {
                log("找不到 ffmpeg，無法合併音訊，音訊已略過。");
                TryDelete(finalAudioPath);
            }
        }

        return DownloadEngineResult.Succeeded(finalPath);
    }

    /// <summary>
    /// Tries each known candidate for a quality tier; when the pool is exhausted
    /// and the most recent attempt was too slow, re-scans (a fresh playback API
    /// call, which Google may route to a different edge server) for more
    /// candidates of the same kind/quality, up to <see cref="MaxRescans"/> times,
    /// before finally accepting the last candidate at whatever speed it gets.
    /// </summary>
    private static async Task<FetchOutcome> DownloadWithFallbackAsync(
        StreamFetcher fetcher,
        List<VideoSourceCandidate> initialCandidates,
        string destinationPath,
        string kindLabel,
        Action<string> log,
        Func<CancellationToken, Task<List<VideoSourceCandidate>>> rescan,
        Func<VideoSourceCandidate, bool> matches,
        CancellationToken token)
    {
        var candidates = new List<VideoSourceCandidate>(initialCandidates);
        var triedUrls = new HashSet<string>();
        var rescanCount = 0;
        VideoSourceCandidate? lastCandidate = null;

        while (true)
        {
            var next = candidates.FirstOrDefault(c => !triedUrls.Contains(c.Url));

            if (next is null)
            {
                if (rescanCount < MaxRescans)
                {
                    rescanCount++;
                    log($"{kindLabel}目前候選來源都太慢，重新分析尋找其他下載點（第 {rescanCount}/{MaxRescans} 次）...");
                    var fresh = await rescan(token);
                    var newOnes = fresh.Where(matches).Where(c => candidates.All(existing => existing.Url != c.Url)).ToList();

                    if (newOnes.Count > 0)
                    {
                        log($"{kindLabel}找到 {newOnes.Count} 個新的候選來源。");
                        candidates.AddRange(newOnes);
                    }
                    else
                    {
                        log($"{kindLabel}重新分析沒有找到新的來源。");
                    }

                    continue;
                }

                if (lastCandidate is null)
                {
                    return new FetchOutcome { Result = FetchResult.HttpError, ErrorMessage = "沒有可用來源" };
                }

                log($"{kindLabel}沒有找到較快的來源，改用慢速來源完整下載...");
                return await fetcher.DownloadAsync(lastCandidate.Url, destinationPath, null, minAcceptableBytesPerSecond: 0, WarmupBeforeSpeedCheck, token);
            }

            triedUrls.Add(next.Url);
            lastCandidate = next;

            log($"正在下載{kindLabel}來源（{next.Label}）...");

            var lastLoggedBucket = -1;
            var outcome = await fetcher.DownloadAsync(
                next.Url,
                destinationPath,
                (bytes, seconds) =>
                {
                    var kbps = seconds > 0 ? bytes / 1024.0 / seconds : 0;
                    var bucket = (int)(bytes / (512 * 1024));
                    if (bucket != lastLoggedBucket)
                    {
                        lastLoggedBucket = bucket;
                        log($"{kindLabel}下載中：{bytes / 1024} KB，約 {kbps:0} KB/s");
                    }
                },
                MinAcceptableBytesPerSecond,
                WarmupBeforeSpeedCheck,
                token);

            if (outcome.Result == FetchResult.Success || token.IsCancellationRequested)
            {
                return outcome;
            }

            log(outcome.Result == FetchResult.SlowAborted
                ? $"{kindLabel}來源速度過慢，嘗試下一個來源..."
                : $"{kindLabel}來源失敗（{outcome.ErrorMessage}），嘗試下一個來源...");
        }
    }

    private static async Task<List<VideoSourceCandidate>> RescanForCandidatesAsync(string fileId, Action<string> log, CancellationToken token)
    {
        if (token.IsCancellationRequested)
        {
            return new List<VideoSourceCandidate>();
        }

        using var scanner = new ScannerForm(fileId, RescanWindow, log);
        scanner.Show();
        await scanner.WaitUntilDoneAsync();
        return scanner.Candidates.ToList();
    }

    private static async Task<bool> MuxAsync(string ffmpegPath, string videoPath, string audioPath, string outputPath, CancellationToken token)
    {
        var psi = new ProcessStartInfo(ffmpegPath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(videoPath);
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(audioPath);
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add("copy");
        psi.ArgumentList.Add(outputPath);

        using var process = new Process { StartInfo = psi };
        process.Start();
        await process.WaitForExitAsync(token);
        return process.ExitCode == 0 && File.Exists(outputPath) && new FileInfo(outputPath).Length > 0;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        var result = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(result) ? "GDriveVideo" : result;
    }

    private static string MakeUniquePath(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path)!;
        var nameWithoutExt = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);

        for (var i = 1; ; i++)
        {
            var candidate = Path.Combine(directory, $"{nameWithoutExt} ({i}){ext}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    private static void TryDelete(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // best effort cleanup
        }
    }

    private static string? ExtractFileId(string url)
    {
        var match = Regex.Match(url, @"/d/([a-zA-Z0-9_-]{10,})");
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        match = Regex.Match(url, @"[?&]id=([a-zA-Z0-9_-]{10,})");
        return match.Success ? match.Groups[1].Value : null;
    }
}
