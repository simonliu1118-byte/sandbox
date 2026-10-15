using System.Diagnostics;
using System.Reflection;

namespace GDriveDownloader;

internal sealed class MainForm : Form
{
    // ---- Top bar -----------------------------------------------------
    private readonly Panel _topBar = new() { Dock = DockStyle.Top, Height = 64, BackColor = Theme.Surface };
    private readonly Label _titleLabel = new() { Text = "Google Drive 影片下載器", AutoSize = true, Left = 56, Top = 12 };
    private readonly Label _versionLabel = new() { AutoSize = true, Left = 56, Top = 36 };
    private readonly Panel _iconBadge = new() { Left = 16, Top = 14, Width = 34, Height = 34, BackColor = Theme.Accent };
    private readonly Label _iconGlyph = new() { Text = "⤓", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White, Font = new Font(Theme.FontFamily, 14f, FontStyle.Bold) };

    // Login status and the login action share a single button: while logged out
    // it reads "登入 Google 帳號" and opens the login window; once logged in it
    // just displays the logged-in state (still re-clickable to re-authenticate).
    private readonly Button _loginButton = new() { Width = 160, Height = 30, Top = 17 };
    private readonly Button _openLogButton = new() { Text = "開啟 Log 資料夾", Width = 120, Height = 30, Top = 17 };

    // ---- Input panel ---------------------------------------------------
    private readonly Panel _inputPanel = new() { Dock = DockStyle.Top, Height = 130, BackColor = Theme.Surface, Padding = new Padding(16, 12, 16, 12) };
    private readonly Label _urlSectionLabel = new() { Text = "影片網址", AutoSize = true, Left = 16, Top = 10 };
    private readonly TextBox _urlBox = new() { Left = 16, Top = 32, Width = 700, Height = 30 };
    private readonly Button _enqueueButton = new() { Text = "加入佇列", Left = 724, Top = 31, Width = 110, Height = 32 };
    private readonly Label _hintLabel1 = new() { Text = "公開影片不需登入；無權限的私人影片請先登入 Google 帳號後再加入佇列。", AutoSize = true, Left = 16, Top = 68 };
    private readonly Label _hintLabel2 = new() { AutoSize = true, Left = 16, Top = 86 };
    private readonly Button _browseButton = new() { Text = "變更資料夾", Left = 834, Top = 84, Width = 100, Height = 26 };

    // ---- Task list -------------------------------------------------
    private readonly Panel _taskListPanel = new() { Dock = DockStyle.Fill, BackColor = Theme.Window, Padding = new Padding(16, 12, 16, 12) };
    private readonly Panel _taskListHeader = new() { Dock = DockStyle.Top, Height = 44, BackColor = Theme.Surface };
    private readonly Label _taskListTitle = new() { Text = "任務清單", AutoSize = true, Left = 12, Top = 12 };
    private readonly Label _taskCountsLabel = new() { AutoSize = true, Top = 15 };
    private readonly Button _startButton = new() { Text = "開始下載佇列", Width = 130, Height = 30, Top = 7 };
    private readonly Button _stopButton = new() { Text = "停止", Width = 90, Height = 30, Top = 7, Enabled = false };

    private readonly FlowLayoutPanel _taskListFlow = new()
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoScroll = true,
        BackColor = Theme.Window,
    };

    private readonly Label _emptyStateLabel = new()
    {
        Text = "尚未加入影片網址",
        TextAlign = ContentAlignment.MiddleCenter,
        Dock = DockStyle.Fill,
        Visible = true,
    };

    // ---- Log panel -------------------------------------------------
    private readonly Panel _logPanel = new() { Dock = DockStyle.Bottom, Height = 190, BackColor = Theme.Surface, Padding = new Padding(16, 8, 16, 8) };
    private readonly Label _logSectionLabel = new() { Text = "執行紀錄", AutoSize = true, Left = 16, Top = 6 };
    private readonly TextBox _logBox = new()
    {
        Left = 16,
        Top = 28,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
    };

    // ---- Status bar -------------------------------------------------
    private readonly Panel _statusBar = new() { Dock = DockStyle.Bottom, Height = 30, BackColor = Theme.Window };
    private readonly Label _statusCountsLabel = new() { AutoSize = true, Left = 16, Top = 7 };
    private readonly Label _outputPathLabel = new() { AutoSize = true, Top = 7 };
    private readonly TextBox _outputDirBox = new() { Visible = false };

    private readonly List<QueueItem> _queue = new();
    private CancellationTokenSource? _cts;
    private readonly StreamWriter? _logFileWriter;
    private readonly string _logFilePath;

    public MainForm()
    {
        Directory.CreateDirectory(AppPaths.LogsDir);
        _logFilePath = Path.Combine(AppPaths.LogsDir, $"GDriveDownloader-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        try
        {
            _logFileWriter = new StreamWriter(_logFilePath, append: true) { AutoFlush = true };
        }
        catch
        {
            _logFileWriter = null;
        }

        Text = "GDriveDownloader - Google Drive 影片下載器";
        Width = 1000;
        Height = 720;
        MinimumSize = new Size(760, 560);
        StartPosition = FormStartPosition.CenterScreen;

        Theme.ApplyForm(this);
        BuildLayout();
        WireEvents();

        _outputDirBox.Text = AppPaths.DefaultDownloadDir;
        UpdateOutputPathLabel();
        UpdateTaskCounts();

        Load += async (_, _) =>
        {
            Log($"Log 檔案：{_logFilePath}（測試時可直接把這個檔案傳給開發者）");
            await RefreshLoginStatusAsync();
        };
    }

    private void BuildLayout()
    {
        // ---- Top bar ----
        _iconBadge.Controls.Add(_iconGlyph);
        Theme.StyleHeading(_titleLabel);
        Theme.StyleCaption(_versionLabel);
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        _versionLabel.Text = version is null ? "版本未知" : $"版本 {version.Major}.{version.Minor}.{version.Build}";

        Theme.StylePrimaryButton(_loginButton);
        Theme.StyleSecondaryButton(_openLogButton);

        _topBar.Controls.Add(_iconBadge);
        _topBar.Controls.Add(_titleLabel);
        _topBar.Controls.Add(_versionLabel);
        _topBar.Controls.Add(_loginButton);
        _topBar.Controls.Add(_openLogButton);
        _topBar.Controls.Add(Theme.MakeDivider());
        _topBar.Resize += (_, _) => LayoutTopBarRight();

        // ---- Input panel ----
        Theme.StyleLabel(_urlSectionLabel);
        _urlSectionLabel.Font = Theme.SectionFont;
        Theme.StyleTextBox(_urlBox);
        _urlBox.Font = new Font(Theme.FontFamily, 10.5f, FontStyle.Regular);
        _urlBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Theme.StylePrimaryButton(_enqueueButton);
        _enqueueButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        Theme.StyleCaption(_hintLabel1);
        Theme.StyleCaption(_hintLabel2);
        Theme.StyleSecondaryButton(_browseButton);
        _browseButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        _inputPanel.Controls.Add(_urlSectionLabel);
        _inputPanel.Controls.Add(_urlBox);
        _inputPanel.Controls.Add(_enqueueButton);
        _inputPanel.Controls.Add(_hintLabel1);
        _inputPanel.Controls.Add(_hintLabel2);
        _inputPanel.Controls.Add(_browseButton);
        _inputPanel.Controls.Add(Theme.MakeDivider());
        _inputPanel.Resize += (_, _) => LayoutInputPanel();

        // ---- Task list ----
        Theme.StyleHeading(_taskListTitle);
        _taskListTitle.Font = Theme.SectionFont;
        Theme.StyleCaption(_taskCountsLabel);
        Theme.StylePrimaryButton(_startButton);
        Theme.StyleDangerButton(_stopButton);

        _taskListHeader.Controls.Add(_taskListTitle);
        _taskListHeader.Controls.Add(_taskCountsLabel);
        _taskListHeader.Controls.Add(_stopButton);
        _taskListHeader.Controls.Add(_startButton);
        _taskListHeader.Controls.Add(Theme.MakeDivider());
        _taskListHeader.Resize += (_, _) => LayoutTaskListHeaderRight();

        Theme.StyleCaption(_emptyStateLabel);
        _emptyStateLabel.ForeColor = Theme.TextSecondary;
        _emptyStateLabel.Font = Theme.BodyFont;

        var listHost = new Panel { Dock = DockStyle.Fill };
        listHost.Controls.Add(_taskListFlow);
        listHost.Controls.Add(_emptyStateLabel);
        _emptyStateLabel.BringToFront();
        _taskListFlow.Resize += (_, _) => ResizeAllCards();

        _taskListPanel.Controls.Add(listHost);
        _taskListPanel.Controls.Add(_taskListHeader);

        // ---- Log panel ----
        Theme.StyleLabel(_logSectionLabel);
        _logSectionLabel.Font = Theme.SectionFont;
        Theme.StyleTextBox(_logBox);
        _logPanel.Controls.Add(_logBox);
        _logPanel.Controls.Add(_logSectionLabel);
        _logPanel.Controls.Add(Theme.MakeDivider());
        _logPanel.Resize += (_, _) => LayoutLogPanel();

        // ---- Status bar ----
        Theme.StyleCaption(_statusCountsLabel);
        Theme.StyleCaption(_outputPathLabel);
        _statusBar.Controls.Add(_statusCountsLabel);
        _statusBar.Controls.Add(_outputPathLabel);
        var topDivider = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Theme.Border };
        _statusBar.Controls.Add(topDivider);
        _statusBar.Resize += (_, _) => LayoutStatusBarRight();

        // Docking order matters: last added Dock=Top/Bottom ends up closest to Fill.
        Controls.Add(_taskListPanel);
        Controls.Add(_logPanel);
        Controls.Add(_statusBar);
        Controls.Add(_inputPanel);
        Controls.Add(_topBar);
        Controls.Add(_outputDirBox);

        LayoutTopBarRight();
        LayoutInputPanel();
        LayoutTaskListHeaderRight();
        LayoutLogPanel();
        LayoutStatusBarRight();
    }

    private void LayoutTopBarRight()
    {
        var right = _topBar.ClientSize.Width - 16;
        _openLogButton.Left = right - _openLogButton.Width;
        _loginButton.Left = _openLogButton.Left - 8 - _loginButton.Width;
    }

    private void LayoutInputPanel()
    {
        var right = _inputPanel.ClientSize.Width - 16;
        _browseButton.Left = right - _browseButton.Width;
        _hintLabel2.Left = 16;
        _hintLabel2.Top = _hintLabel1.Bottom + 4;
    }

    private void LayoutTaskListHeaderRight()
    {
        var right = _taskListHeader.ClientSize.Width - 12;
        _startButton.Left = right - _startButton.Width;
        _stopButton.Left = _startButton.Left - 8 - _stopButton.Width;
        _taskCountsLabel.Left = _stopButton.Left - 16 - _taskCountsLabel.Width;
    }

    private void LayoutLogPanel()
    {
        _logBox.Width = _logPanel.ClientSize.Width - 32;
        _logBox.Height = _logPanel.ClientSize.Height - 36;
    }

    private void LayoutStatusBarRight()
    {
        _outputPathLabel.Left = _statusBar.ClientSize.Width - 16 - _outputPathLabel.Width;
    }

    private void ResizeAllCards()
    {
        // FlowLayoutPanel doesn't stretch children to its own width, so each card's
        // width is kept in sync by hand (minus a little room for the scrollbar).
        var width = Math.Max(200, _taskListFlow.ClientSize.Width - 20);
        foreach (var item in _queue)
        {
            item.Card?.SetWidth(width);
        }
    }

    private void WireEvents()
    {
        _enqueueButton.Click += (_, _) => EnqueueCurrentUrl();
        _urlBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                EnqueueCurrentUrl();
            }
        };
        _loginButton.Click += async (_, _) => await OpenLoginAsync();
        _browseButton.Click += (_, _) => BrowseOutputDir();
        _startButton.Click += async (_, _) => await StartQueueAsync();
        _stopButton.Click += (_, _) => StopQueue();
        _openLogButton.Click += (_, _) => OpenLogFolder();

        FormClosed += (_, _) => _logFileWriter?.Dispose();
    }

    private void UpdateOutputPathLabel()
    {
        _hintLabel2.Text = $"下載資料夾：{_outputDirBox.Text}";
        _outputPathLabel.Text = $"儲存位置：{_outputDirBox.Text}";
        LayoutStatusBarRight();
        LayoutInputPanel();
    }

    private void Log(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => Log(message)));
            return;
        }

        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        _logBox.AppendText(line + Environment.NewLine);

        try
        {
            _logFileWriter?.WriteLine(line);
        }
        catch
        {
            // Best effort; the on-screen log box remains the primary record.
        }
    }

    private void OpenLogFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AppPaths.LogsDir}\"") { UseShellExecute = true });
        }
        catch
        {
            MessageBox.Show(this, $"Log 資料夾：{AppPaths.LogsDir}", "開啟失敗", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private async Task RefreshLoginStatusAsync()
    {
        _loginButton.Text = "檢查登入狀態中...";
        _loginButton.Enabled = false;

        var loggedIn = await LoginStatusChecker.IsLoggedInAsync();

        _loginButton.Enabled = true;
        if (loggedIn)
        {
            _loginButton.Text = "✓ 已登入 Google";
            Theme.StyleSecondaryButton(_loginButton);
        }
        else
        {
            _loginButton.Text = "登入 Google 帳號";
            Theme.StylePrimaryButton(_loginButton);
        }

        LayoutTopBarRight();
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
            UpdateOutputPathLabel();
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

        var card = new TaskCard(item);
        card.SetWidth(Math.Max(200, _taskListFlow.ClientSize.Width - 20));
        card.StopRequested += (_, _) => OnCardStopRequested(item);
        item.Card = card;

        _taskListFlow.Controls.Add(card);
        _emptyStateLabel.Visible = false;

        _urlBox.Clear();
        UpdateTaskCounts();
    }

    private void OnCardStopRequested(QueueItem item)
    {
        switch (item.Status)
        {
            case QueueStatus.Downloading:
                // The in-flight download is cancelled; DownloadOneAsync removes the
                // card once it observes the cancellation instead of leaving behind
                // a "cancelled" row the user has to dismiss separately.
                item.RemoveAfterCompletion = true;
                item.Cts?.Cancel();
                item.Card?.SetStatus(QueueStatus.Downloading, "正在停止...");
                break;
            default:
                RemoveItem(item);
                break;
        }
    }

    private void RemoveItem(QueueItem item)
    {
        _queue.Remove(item);
        if (item.Card != null)
        {
            _taskListFlow.Controls.Remove(item.Card);
            item.Card.Dispose();
            item.Card = null;
        }

        _emptyStateLabel.Visible = _queue.Count == 0;
        UpdateTaskCounts();
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

            // Index-based walk (not foreach) because a card's stop/remove button can
            // delete a pending item from _queue while this loop is awaiting a
            // different item's download; foreach would throw on that mutation.
            var i = 0;
            while (i < _queue.Count)
            {
                if (_cts.IsCancellationRequested)
                {
                    break;
                }

                var item = _queue[i];
                if (item.Status != QueueStatus.Pending)
                {
                    i++;
                    continue;
                }

                await DownloadOneAsync(item, ffmpeg, _cts.Token);

                // If the item is still at index i, advance past it; if it was
                // removed mid-download, index i now already points at the next
                // item, so stay put instead of skipping it.
                if (i < _queue.Count && ReferenceEquals(_queue[i], item))
                {
                    i++;
                }
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

    private async Task DownloadOneAsync(QueueItem item, string? ffmpeg, CancellationToken outerToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(outerToken);
        item.Cts = linkedCts;

        UpdateItemStatus(item, QueueStatus.Downloading, "分析畫質中...");
        item.Card?.SetVideoProgress(null, "分析畫質中...");
        item.Card?.SetAudioProgress(null, "分析畫質中...");

        void OnAudioAvailable(bool hasAudio)
        {
            if (!hasAudio)
            {
                item.Card?.SetAudioUnavailable();
            }
        }

        var result = await DownloadEngine.RunAsync(
            item.Url,
            _outputDirBox.Text,
            ffmpeg,
            Log,
            (fraction, label) => item.Card?.SetVideoProgress(fraction, label),
            (fraction, label) => item.Card?.SetAudioProgress(fraction, label),
            OnAudioAvailable,
            linkedCts.Token);

        item.Cts = null;

        if (item.RemoveAfterCompletion)
        {
            RemoveItem(item);
            return;
        }

        if (result.Success)
        {
            UpdateItemStatus(item, QueueStatus.Completed, $"完成：{Path.GetFileName(result.OutputPath)}");
        }
        else if (linkedCts.IsCancellationRequested)
        {
            UpdateItemStatus(item, QueueStatus.Cancelled, "已取消");
        }
        else
        {
            UpdateItemStatus(item, QueueStatus.Failed, result.ErrorMessage ?? "未知錯誤");
            Log($"下載失敗：{item.Url} - {result.ErrorMessage}");
        }
    }

    private void UpdateItemStatus(QueueItem item, QueueStatus status, string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => UpdateItemStatus(item, status, message)));
            return;
        }

        item.Status = status;
        item.Message = message;
        item.Card?.SetStatus(status, message);

        UpdateTaskCounts();
    }

    private void UpdateTaskCounts()
    {
        var pending = _queue.Count(i => i.Status == QueueStatus.Pending);
        var downloading = _queue.Count(i => i.Status == QueueStatus.Downloading);
        var completed = _queue.Count(i => i.Status == QueueStatus.Completed);
        var failed = _queue.Count(i => i.Status == QueueStatus.Failed);

        _taskCountsLabel.Text = $"等待 {pending} 筆 · 已完成 {completed} 筆";
        _statusCountsLabel.Text = $"下載中 {downloading} 筆 · 等待 {pending} 筆 · 已完成 {completed} 筆 · 失敗 {failed} 筆";

        LayoutTaskListHeaderRight();
    }
}
