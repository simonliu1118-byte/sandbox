namespace GDriveDownloader;

/// <summary>
/// One row in the task list: a self-contained card showing a single queue item's
/// URL, overall status, and independent video/audio progress bars, with its own
/// stop/remove button. Replaces the old flat ListView rows (which had no room for
/// per-stream progress or a per-item stop action).
/// </summary>
internal sealed class TaskCard : Panel
{
    public QueueItem Item { get; }

    public event EventHandler? StopRequested;

    private readonly Label _urlLabel = new() { AutoSize = false, Left = 12, Top = 10, Height = 18 };
    private readonly Label _statusBadge = new() { AutoSize = false, Width = 78, Height = 22, Top = 9 };
    private readonly Button _stopButton = new() { Width = 64, Height = 24, Top = 8 };

    private readonly Label _videoKindLabel = new() { Text = "視訊", AutoSize = true, Left = 12, Top = 42 };
    private readonly ProgressBar _videoBar = new() { Left = 50, Top = 40, Height = 16, Minimum = 0, Maximum = 1000 };
    private readonly Label _videoStatusLabel = new() { AutoSize = false, Left = 50, Top = 58, Height = 16 };

    private readonly Label _audioKindLabel = new() { Text = "音訊", AutoSize = true, Left = 12, Top = 80 };
    private readonly ProgressBar _audioBar = new() { Left = 50, Top = 78, Height = 16, Minimum = 0, Maximum = 1000 };
    private readonly Label _audioStatusLabel = new() { AutoSize = false, Left = 50, Top = 96, Height = 16 };

    public TaskCard(QueueItem item)
    {
        Item = item;

        Height = 118;
        Margin = new Padding(0, 0, 0, 8);
        BackColor = Theme.Surface;
        BorderStyle = BorderStyle.FixedSingle;

        Theme.StyleLabel(_urlLabel);
        _urlLabel.Font = Theme.SectionFont;
        _urlLabel.Text = ShortenUrl(item.Url);
        _urlLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        Theme.StylePill(_statusBadge, active: false);
        _statusBadge.Text = "等待中";
        _statusBadge.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        Theme.StyleDangerButton(_stopButton);
        _stopButton.Text = "移除";
        _stopButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _stopButton.Click += (_, _) => StopRequested?.Invoke(this, EventArgs.Empty);

        Theme.StyleCaption(_videoKindLabel);
        Theme.StyleCaption(_videoStatusLabel);
        _videoStatusLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _videoBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        Theme.StyleCaption(_audioKindLabel);
        Theme.StyleCaption(_audioStatusLabel);
        _audioStatusLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _audioBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        Controls.AddRange(new Control[]
        {
            _urlLabel, _statusBadge, _stopButton,
            _videoKindLabel, _videoBar, _videoStatusLabel,
            _audioKindLabel, _audioBar, _audioStatusLabel,
        });

        Resize += (_, _) => LayoutRight();
        LayoutRight();
    }

    public void SetWidth(int width)
    {
        Width = width;
    }

    private void LayoutRight()
    {
        var right = ClientSize.Width - 12;
        _stopButton.Left = right - _stopButton.Width;
        _statusBadge.Left = _stopButton.Left - 8 - _statusBadge.Width;
        _urlLabel.Width = Math.Max(40, _statusBadge.Left - 8 - _urlLabel.Left);

        _videoBar.Width = Math.Max(40, right - _videoBar.Left);
        _videoStatusLabel.Width = Math.Max(40, right - _videoStatusLabel.Left);
        _audioBar.Width = Math.Max(40, right - _audioBar.Left);
        _audioStatusLabel.Width = Math.Max(40, right - _audioStatusLabel.Left);
    }

    private static string ShortenUrl(string url)
    {
        return url.Length > 76 ? url[..40] + "..." + url[^30..] : url;
    }

    public void SetStatus(QueueStatus status, string message)
    {
        var (text, active) = status switch
        {
            QueueStatus.Pending => ("等待中", false),
            QueueStatus.Downloading => ("下載中", true),
            QueueStatus.Completed => ("已完成", true),
            QueueStatus.Failed => ("失敗", false),
            QueueStatus.Cancelled => ("已取消", false),
            _ => (status.ToString(), false),
        };

        _statusBadge.Text = text;
        Theme.StylePill(_statusBadge, active);
        _stopButton.Text = status == QueueStatus.Downloading ? "停止" : "移除";

        if (status is QueueStatus.Completed or QueueStatus.Failed or QueueStatus.Cancelled)
        {
            _videoBar.Style = ProgressBarStyle.Continuous;
            _audioBar.Style = ProgressBarStyle.Continuous;
        }

        if (status is QueueStatus.Failed or QueueStatus.Cancelled)
        {
            _videoStatusLabel.Text = message;
        }
    }

    public void SetVideoProgress(double? fraction, string label)
    {
        SetBarProgress(_videoBar, fraction);
        _videoStatusLabel.Text = label;
    }

    public void SetAudioProgress(double? fraction, string label)
    {
        SetBarProgress(_audioBar, fraction);
        _audioStatusLabel.Text = label;
    }

    public void SetAudioUnavailable()
    {
        _audioBar.Style = ProgressBarStyle.Continuous;
        _audioBar.Value = 0;
        _audioStatusLabel.Text = "無音訊來源";
    }

    private static void SetBarProgress(ProgressBar bar, double? fraction)
    {
        if (fraction is double f)
        {
            bar.Style = ProgressBarStyle.Continuous;
            bar.Value = Math.Clamp((int)(f * bar.Maximum), bar.Minimum, bar.Maximum);
        }
        else
        {
            bar.Style = ProgressBarStyle.Marquee;
            bar.MarqueeAnimationSpeed = 30;
        }
    }
}
