using System.IO.Compression;
using System.Net.Http;

namespace GDownloader;

internal static class ToolManager
{
    private const string YtDlpUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
    private const string FfmpegZipUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";

    public static async Task<string> EnsureYtDlpAsync(Action<string> log)
    {
        if (File.Exists(AppPaths.YtDlpExe) && new FileInfo(AppPaths.YtDlpExe).Length > 0)
        {
            return AppPaths.YtDlpExe;
        }

        log("正在下載 yt-dlp.exe ...");
        using var http = new HttpClient();
        var bytes = await http.GetByteArrayAsync(YtDlpUrl);
        await File.WriteAllBytesAsync(AppPaths.YtDlpExe, bytes);
        log("yt-dlp.exe 下載完成。");
        return AppPaths.YtDlpExe;
    }

    public static async Task<string?> EnsureFfmpegAsync(Action<string> log)
    {
        if (File.Exists(AppPaths.FfmpegExe) && new FileInfo(AppPaths.FfmpegExe).Length > 0)
        {
            return AppPaths.FfmpegExe;
        }

        var tempZip = Path.Combine(AppPaths.ToolsDir, "ffmpeg_download.zip");

        try
        {
            log("正在下載 ffmpeg（用於合併最高畫質影音，若失敗將以次佳畫質繼續）...");
            using var http = new HttpClient();
            var zipBytes = await http.GetByteArrayAsync(FfmpegZipUrl);
            await File.WriteAllBytesAsync(tempZip, zipBytes);

            using (var archive = ZipFile.OpenRead(tempZip))
            {
                ZipArchiveEntry? entry = null;
                foreach (var candidate in archive.Entries)
                {
                    if (candidate.FullName.EndsWith("bin/ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        entry = candidate;
                        break;
                    }
                }

                entry?.ExtractToFile(AppPaths.FfmpegExe, overwrite: true);
            }

            if (File.Exists(AppPaths.FfmpegExe) && new FileInfo(AppPaths.FfmpegExe).Length > 0)
            {
                log("ffmpeg 準備完成。");
                return AppPaths.FfmpegExe;
            }

            log("ffmpeg 下載內容中找不到 ffmpeg.exe，將以不需合併的最高可用畫質繼續。");
        }
        catch (Exception ex)
        {
            log($"ffmpeg 下載失敗，將以不需合併的最高可用畫質繼續：{ex.Message}");
        }
        finally
        {
            if (File.Exists(tempZip))
            {
                File.Delete(tempZip);
            }
        }

        return null;
    }
}
