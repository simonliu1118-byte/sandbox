namespace GDriveDownloader;

internal sealed class MainForm : Form
{
    private readonly TextBox _urlBox = new() { Left = 12, Top = 14, Width = 520 };
    private readonly Button _enqueueButton = new() { Text = "加入佇列", Left = 540, Top = 12, Width = 100 };
    private readonly Button _loginButton = new() { Text = "登入 Google 帳號", Left = 650, Top = 12, Width = 140 };
    private readonly Label _loginStatusLabel = new() { Left = 800, Top = 17, Width = 160, Text = "登入狀態：檢查中..." };

    private readonly TextBox _outputDirBox = new() { Left = 12, Top = 50, Width = 520, ReadOnly = true };
    private readonly Button _browseButton = new() { Text = "選擇下載資料夾", Left = 540, Top = 48, Width = 140 };

    private readonly ListView _queueList = new()
    {
        Left = 12,
        Top = 86,
        Width = 930,
        Height = 260,
        View = View.Details,
        FullRowSelect = true,
    };

    private readonly Button _startButton = new() { Text = "開始下載佇列", Left = 12, Top = 356, Width = 140 };
    private readonly Button _stopButton = new() { Text = "停止", Left = 160, Top = 356, Width = 100, Enabled = false };

    private readonly TextBox _logBox = new()
    {
        Left = 12,
        Top = 396,
        Width = 930,
        Height = 200,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
    };

    private readonly List<QueueItem> _queue = new();
    private CancellationTokenSource? _cts;

    public MainForm()
    {
        Text = "GDriveDownloader - Google Drive 影片下載器";
        Width = 980;
        Height = 650;
        StartPosition = FormStartPosition.CenterScreen;

        Theme.ApplyForm(this);
        Theme.StyleTextBox(_urlBox);
        Theme.StyleTextBox(_outputDirBox);
        Theme.StyleTextBox(_logBox);
        Theme.StyleListView(_queueList);
        Theme.StyleLabel(_loginStatusLabel, secondary: true);
        Theme.StylePrimaryButton(_enqueueButton);
        Theme.StylePrimaryButton(_loginButton);
        Theme.StyleSecondaryButton(_browseButton);
        Theme.StylePrimaryButton(_startButton);
        Theme.StyleDangerButton(_stopButton);

        _queueList.Columns.Add("連結", 480);
        _queueList.Columns.Add("狀態", 100);
        _queueList.Columns.Add("訊息", 330);

        _outputDirBox.Text = AppPaths.DefaultDownloadDir;

        Controls.AddRange(new Control[]
        {
            _urlBox,
            _enqueueButton,
            _loginButton,
            _loginStatusLabel,
            _outputDirBox,
            _browseButton,
            _queueList,
            _startButton,
            _stopButton,
            _logBox,
        });

        _enqueueButton.Click += (_, _) => EnqueueCurrentUrl();
        _loginButton.Click += async (_, _) => await OpenLoginAsync();
        _browseButton.Click += (_, _) => BrowseOutputDir();
        _startButton.Click += async (_, _) => await StartQueueAsync();
        _stopButton.Click += (_, _) => StopQueue();

        Load += async (_, _) => await RefreshLoginStatusAsync();
    }

    private void Log(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => Log(message)));
            return;
        }

        _logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private async Task RefreshLoginStatusAsync()
    {
        _loginStatusLabel.Text = "登入狀態：檢查中...";
        var loggedIn = await LoginStatusChecker.IsLoggedInAsync();
        _loginStatusLabel.Text = loggedIn ? "登入狀態：已登入" : "登入狀態：未登入";
    }

    private async Task OpenLoginAsync()
    {
        using var loginForm = new GoogleLoginForm();
        loginForm.ShowDialog(this);
        await RefreshLoginStatusAsync();
    }

    private void BrowseOutputDir()
    {
        using var dialog = new FolderBrowserDialog { SelectedPath = _outputDirBox.Text };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _outputDirBox.Text = dialog.SelectedPath;
        }
    }

    private void EnqueueCurrentUrl()
    {
        var url = _urlBox.Text.Trim();
        if (url.Length == 0)
        {
            return;
        }

        if (!url.Contains("drive.google.com", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "請貼上有效的 Google Drive 連結。", "連結格式錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var item = new QueueItem(url);
        _queue.Add(item);

        var row = new ListViewItem(new[] { url, "等待中", string.Empty });
        item.ListViewItem = row;
        _queueList.Items.Add(row);

        _urlBox.Clear();
    }

    private async Task StartQueueAsync()
    {
        if (_cts != null)
        {
            return;
        }

        Directory.CreateDirectory(_outputDirBox.Text);

        _cts = new CancellationTokenSource();
        _startButton.Enabled = false;
        _stopButton.Enabled = true;

        try
        {
            var ffmpeg = await ToolManager.EnsureFfmpegAsync(Log);

            foreach (var item in _queue)
            {
                if (_cts.IsCancellationRequested)
                {
                    break;
                }

                if (item.Status != QueueStatus.Pending)
                {
                    continue;
                }

                await DownloadOneAsync(item, ffmpeg, _cts.Token);
            }

            Log("佇列處理完畢。");
        }
        catch (Exception ex)
        {
            Log($"處理佇列時發生錯誤：{ex.Message}");
        }
        finally
        {
            _startButton.Enabled = true;
            _stopButton.Enabled = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void StopQueue()
    {
        _cts?.Cancel();
        Log("已要求停止，將於目前項目結束後停止。");
    }

    private async Task DownloadOneAsync(QueueItem item, string? ffmpeg, CancellationToken token)
    {
        UpdateItem(item, QueueStatus.Downloading, "分析畫質中...");

        var result = await DownloadEngine.RunAsync(item.Url, _outputDirBox.Text, ffmpeg, Log, token);

        if (result.Success)
        {
            UpdateItem(item, QueueStatus.Completed, $"完成：{Path.GetFileName(result.OutputPath)}");
        }
        else
        {
            UpdateItem(item, QueueStatus.Failed, result.ErrorMessage ?? "未知錯誤");
            Log($"下載失敗：{item.Url} - {result.ErrorMessage}");
        }
    }

    private void UpdateItem(QueueItem item, QueueStatus status, string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => UpdateItem(item, status, message)));
            return;
        }

        item.Status = status;
        item.Message = message;

        if (item.ListViewItem != null)
        {
            item.ListViewItem.SubItems[1].Text = status switch
            {
                QueueStatus.Pending => "等待中",
                QueueStatus.Downloading => "下載中",
                QueueStatus.Completed => "已完成",
                QueueStatus.Failed => "失敗",
                _ => status.ToString(),
            };
            item.ListViewItem.SubItems[2].Text = message;
        }
    }
}
