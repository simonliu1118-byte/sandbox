using System.IO.Compression;
using System.Net.Http;

namespace GDriveDownloader;

internal static class ToolManager
{
    private const string FfmpegZipUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";

    public static async Task<string?> EnsureFfmpegAsync(Action<string> log)
    {
        if (File.Exists(AppPaths.FfmpegExe) && new FileInfo(AppPaths.FfmpegExe).Length > 0)
        {
            return AppPaths.FfmpegExe;
        }

        var tempZip = Path.Combine(AppPaths.ToolsDir, "ffmpeg_download.zip");

        try
        {
            log("正在下載 ffmpeg（用於合併視訊與音訊）...");
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

            log("ffmpeg 下載內容中找不到 ffmpeg.exe，將只輸出視訊，不合併音訊。");
        }
        catch (Exception ex)
        {
            log($"ffmpeg 下載失敗，將只輸出視訊，不合併音訊：{ex.Message}");
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
