namespace GDriveDownloader;

internal enum QueueStatus
{
    Pending,
    Downloading,
    Completed,
    Failed,
    Cancelled,
}

internal sealed class QueueItem
{
    public QueueItem(string url)
    {
        Url = url;
    }

    public string Url { get; }

    public QueueStatus Status { get; set; } = QueueStatus.Pending;

    public string Message { get; set; } = string.Empty;

    public CancellationTokenSource? Cts { get; set; }

    /// <summary>
    /// Set when the user stops a card that is actively downloading: the in-flight
    /// download is cancelled, and once DownloadOneAsync observes it finish it
    /// removes the card entirely instead of showing a "cancelled" status.
    /// </summary>
    public bool RemoveAfterCompletion { get; set; }

    public TaskCard? Card { get; set; }
}
