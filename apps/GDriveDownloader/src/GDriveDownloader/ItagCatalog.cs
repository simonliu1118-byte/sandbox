namespace GDriveDownloader;

internal enum StreamKind
{
    Video,
    Audio,
    Unknown,
}

internal readonly record struct ItagInfo(StreamKind Kind, int? Height, string Label);

internal static class ItagCatalog
{
    // Public, widely documented itag table (Google/YouTube adaptive + progressive formats).
    // Google Drive's video backend reuses the same itag scheme.
    private static readonly Dictionary<int, ItagInfo> Table = new()
    {
        // MP4 video-only (adaptive)
        [160] = new ItagInfo(StreamKind.Video, 144, "144p"),
        [133] = new ItagInfo(StreamKind.Video, 240, "240p"),
        [134] = new ItagInfo(StreamKind.Video, 360, "360p"),
        [135] = new ItagInfo(StreamKind.Video, 480, "480p"),
        [136] = new ItagInfo(StreamKind.Video, 720, "720p"),
        [137] = new ItagInfo(StreamKind.Video, 1080, "1080p"),
        [264] = new ItagInfo(StreamKind.Video, 1440, "1440p"),
        [266] = new ItagInfo(StreamKind.Video, 2160, "2160p"),

        // WebM video-only (adaptive)
        [242] = new ItagInfo(StreamKind.Video, 240, "240p (WebM)"),
        [243] = new ItagInfo(StreamKind.Video, 360, "360p (WebM)"),
        [244] = new ItagInfo(StreamKind.Video, 480, "480p (WebM)"),
        [247] = new ItagInfo(StreamKind.Video, 720, "720p (WebM)"),
        [248] = new ItagInfo(StreamKind.Video, 1080, "1080p (WebM)"),
        [271] = new ItagInfo(StreamKind.Video, 1440, "1440p (WebM)"),
        [313] = new ItagInfo(StreamKind.Video, 2160, "2160p (WebM)"),

        // Progressive (already contains audio + video)
        [18] = new ItagInfo(StreamKind.Video, 360, "360p (含音訊)"),
        [22] = new ItagInfo(StreamKind.Video, 720, "720p (含音訊)"),

        // Audio-only
        [139] = new ItagInfo(StreamKind.Audio, null, "48kbps AAC"),
        [140] = new ItagInfo(StreamKind.Audio, null, "128kbps AAC"),
        [141] = new ItagInfo(StreamKind.Audio, null, "256kbps AAC"),
        [171] = new ItagInfo(StreamKind.Audio, null, "128kbps Opus"),
        [249] = new ItagInfo(StreamKind.Audio, null, "50kbps Opus"),
        [250] = new ItagInfo(StreamKind.Audio, null, "70kbps Opus"),
        [251] = new ItagInfo(StreamKind.Audio, null, "160kbps Opus"),
    };

    public static ItagInfo Resolve(int itag, string? mimeType)
    {
        if (Table.TryGetValue(itag, out var info))
        {
            return info;
        }

        var guessedKind = GuessKindFromMime(mimeType);
        return new ItagInfo(guessedKind, null, guessedKind == StreamKind.Unknown ? $"itag {itag}" : $"itag {itag}（未知畫質）");
    }

    public static StreamKind GuessKindFromMime(string? mimeType)
    {
        if (string.IsNullOrEmpty(mimeType))
        {
            return StreamKind.Unknown;
        }

        if (mimeType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
        {
            return StreamKind.Audio;
        }

        if (mimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
        {
            return StreamKind.Video;
        }

        return StreamKind.Unknown;
    }
}
