namespace GDriveDownloader;

internal enum QueueStatus
{
    Pending,
    Downloading,
    Completed,
    Failed,
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

    public ListViewItem? ListViewItem { get; set; }
}
