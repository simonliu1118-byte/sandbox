namespace GDriveDownloader;

internal sealed class VideoSourceCandidate
{
    public required string Url { get; init; }

    public required StreamKind Kind { get; init; }

    public int? Height { get; init; }

    public required string Label { get; init; }

    public int Itag { get; init; }

    public string? MimeType { get; init; }
}
