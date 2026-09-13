using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScratchGame.Controls;
using ScratchGame.Data;
using ScratchGame.Engine;
using ScratchGame.Models;
using ScratchGame.Services;
using ScratchGame.Views;

namespace ScratchGame;

public partial class MainWindow : Window
{
    private const double DefaultWindowWidth = 1220;
    private const double DefaultWindowHeight = 860;
    private const int WmSysCommand = 0x0112;
    private const int ScSize = 0xF000;

    private static readonly (double X, double Y)[] ThreeLinePositions =
    {
        (274, 216), (464, 216), (654, 216),
        (274, 345), (464, 345), (654, 345),
        (274, 474), (464, 474), (654, 474)
    };

    private const double ThreeLineCellWidth = 180;
    private const double ThreeLineCellHeight = 122;

    private readonly AppDatabase _database = new();
    private readonly CatalogService _catalog;
    private readonly PrizePoolService _prizePool;
    private readonly TicketSwapService _ticketSwap;
    private readonly SeedDataService _seed;
    private readonly BackupService _backup;
    private readonly List<ScratchSurface> _scratchRegions = new();
    private readonly HashSet<ScratchSurface> _completedScratchRegions = new();

    private UserProfile? _currentUser;
    private PendingTicket? _currentPending;
    private TicketDefinition? _currentDefinition;
    private bool _settlementInProgress;
    private bool _isBoardScratching;
    private bool _hasScratched;
    private Point _lastBoardPoint;

    public MainWindow()
    {
        InitializeComponent();
        _catalog = new CatalogService(_database);
        _prizePool = new PrizePoolService(_database);
        _ticketSwap = new TicketSwapService(_database);
        _seed = new SeedDataService(_database);
        _backup = new BackupService(_database);

        UiAssetLoader.TrySetImage(TopBarBackgroundImage, UiAssetLoader.UiPath("topbar_bg.png"));
        UiAssetLoader.TrySetImage(StageBackgroundImage, UiAssetLoader.UiPath("stage_bg.png"));
        UiAssetLoader.TrySetImage(FooterBackgroundImage, UiAssetLoader.UiPath("footer_bg.png"));

        SourceInitialized += MainWindow_OnSourceInitialized;
        StateChanged += MainWindow_OnStateChanged;
    }

    private void MainWindow_OnSourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
            source.AddHook(WindowMessageHook);
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmSysCommand && (wParam.ToInt32() & 0xFFF0) == ScSize)
        {
            handled = true;
            return IntPtr.Zero;
        }
        return IntPtr.Zero;
    }

    private void MainWindow_OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState != WindowState.Normal)
            return;

        Width = DefaultWindowWidth;
        Height = DefaultWindowHeight;
    }

    private async void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            StatusText.Text = "正在初始化…";
            await _database.InitializeAsync();
            await _seed.EnsureSeedDataAsync();
            await _backup.BackupIfDueAsync();

            var users = await _catalog.GetUsersAsync();
            _currentUser = users.FirstOrDefault();
            await LoadCurrentUserAsync();
            if (_currentPending is null)
                StatusText.Text = "準備完成，挑一張彩券開始吧";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "啟動失敗", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "初始化失敗";
        }
    }

    private async Task LoadCurrentUserAsync()
    {
        if (_currentUser is null)
        {
            _currentDefinition = null;
            _currentPending = null;
            ClearTicketDisplay("請先建立使用者");
            UserSummaryText.Text = "尚無使用者";
            return;
        }

        await RefreshCurrentUserSummaryAsync();
        _currentPending = await _catalog.GetPendingForUserAsync(_currentUser.Id);
        if (_currentPending is null)
        {
            _currentDefinition = null;
            ClearTicketDisplay("還沒有彩券");
            return;
        }

        var tickets = await _catalog.GetAvailableTicketsAsync();
        _currentDefinition = tickets.FirstOrDefault(t => t.Id == _currentPending.TicketId);
        if (_currentDefinition is null)
        {
            TicketMetaText.Text = "未完成彩券";
            StatusText.Text = "找到 Pending Ticket，但彩券定義目前不可用。";
            return;
        }

        RenderPendingTicket(_currentPending, _currentDefinition);
        StatusText.Text = _hasScratched
            ? "已恢復上次尚未完成的彩券；已開始刮獎，不能換票"
            : "已恢復上次尚未完成的彩券";
    }

    private async void ChooseTicketFromEmpty_OnClick(object sender, RoutedEventArgs e)
        => await ChooseNewTicketAsync();

    private async void ChooseOtherTicket_OnClick(object sender, RoutedEventArgs e)
        => await ChooseNewTicketAsync();

    private async void SameAgain_OnClick(object sender, RoutedEventArgs e)
    {
        if (_currentUser is null || _currentDefinition is null || _currentPending is not null)
            return;

        await CreateTicketAsync(_currentDefinition);
    }

    private async Task ChooseNewTicketAsync()
    {
        if (_currentUser is null)
            return;

        if (_currentPending is not null)
        {
            MessageBox.Show(this, "目前仍有一張尚未完成的彩券。請先刮完或在尚未刮獎時使用「換一張」。", "尚有未完成彩券", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var tickets = await _catalog.GetAvailableTicketsAsync();
            if (tickets.Count == 0)
            {
                MessageBox.Show(this, "目前沒有可用的彩券批次。", "挑選彩券", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new NewTicketDialog(tickets) { Owner = this };
            if (dialog.ShowDialog() != true || dialog.SelectedTicket is null)
                return;

            await CreateTicketAsync(dialog.SelectedTicket);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "無法建立彩券", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task CreateTicketAsync(TicketDefinition definition)
    {
        if (_currentUser is null)
            return;

        try
        {
            _currentDefinition = definition;
            _currentPending = await _prizePool.CreatePendingAsync(
                _currentUser.Id,
                definition.Id,
                amount => GamePayloadFactory.Create(definition.RuleId, amount, definition.Price));

            RenderPendingTicket(_currentPending, definition);
            await RefreshCurrentUserSummaryAsync();
            StatusText.Text = "彩券已抽出，尚未刮獎時可使用「換一張」";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "無法建立彩券", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void RevealAll_OnClick(object sender, RoutedEventArgs e)
    {
        if (_currentPending is null)
            return;

        if (!_hasScratched)
        {
            _hasScratched = true;
            UpdateTicketActionState();
            await MarkScratchStartedSafeAsync();
        }

        foreach (var region in _scratchRegions)
            region.RevealAll();
    }

    private async void SwapTicket_OnClick(object sender, RoutedEventArgs e)
    {
        if (_currentPending is null || _currentDefinition is null)
            return;

        if (_hasScratched)
        {
            MessageBox.Show(this, "這張已經開始刮獎，不能再換票。", "換一張", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var replacement = await _ticketSwap.SwapPendingAsync(
                _currentPending.Id,
                amount => GamePayloadFactory.Create(
                    _currentDefinition.RuleId,
                    amount,
                    _currentDefinition.Price));

            _currentPending = replacement;
            RenderPendingTicket(replacement, _currentDefinition);
            StatusText.Text = "已換成同款彩券的新序號；本次換票不重複計入投入";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "無法換票", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void TicketOverlayCanvas_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_currentPending is null || _scratchRegions.Count == 0)
            return;

        if (!_hasScratched)
        {
            _hasScratched = true;
            UpdateTicketActionState();
            await MarkScratchStartedSafeAsync();
        }

        _isBoardScratching = true;
        _lastBoardPoint = e.GetPosition(TicketOverlayCanvas);
        Mouse.Capture(TicketOverlayCanvas, CaptureMode.Element);
        ScratchBoardSegment(_lastBoardPoint, _lastBoardPoint);
        e.Handled = true;
    }

    private void TicketOverlayCanvas_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isBoardScratching || e.LeftButton != MouseButtonState.Pressed)
            return;

        var point = e.GetPosition(TicketOverlayCanvas);
        ScratchBoardSegment(_lastBoardPoint, point);
        _lastBoardPoint = point;
        e.Handled = true;
    }

    private void TicketOverlayCanvas_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isBoardScratching)
            return;

        var point = e.GetPosition(TicketOverlayCanvas);
        ScratchBoardSegment(_lastBoardPoint, point);
        _isBoardScratching = false;
        Mouse.Capture(null);
        foreach (var region in _scratchRegions)
            region.CheckCompletion();
        e.Handled = true;
    }

    private async Task MarkScratchStartedSafeAsync()
    {
        if (_currentPending is null)
            return;

        try
        {
            await _ticketSwap.MarkScratchStartedAsync(_currentPending.Id);
        }
        catch
        {
            // 本地已立即鎖住換票；資料庫標記失敗不應中斷刮獎手感。
        }
    }

    private void ScratchBoardSegment(Point start, Point end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        const double stepSize = 5.0;
        var steps = Math.Max(1, (int)Math.Ceiling(distance / stepSize));
        var pointsByRegion = new Dictionary<ScratchSurface, List<Point>>();

        for (var i = 0; i <= steps; i++)
        {
            var t = (double)i / steps;
            var point = new Point(start.X + dx * t, start.Y + dy * t);

            foreach (var region in _scratchRegions)
            {
                var left = Canvas.GetLeft(region);
                var top = Canvas.GetTop(region);
                if (double.IsNaN(left) || double.IsNaN(top))
                    continue;

                if (point.X < left || point.X > left + region.Width ||
                    point.Y < top || point.Y > top + region.Height)
                    continue;

                if (!pointsByRegion.TryGetValue(region, out var localPoints))
                {
                    localPoints = new List<Point>();
                    pointsByRegion[region] = localPoints;
                }
                localPoints.Add(new Point(point.X - left, point.Y - top));
            }
        }

        foreach (var pair in pointsByRegion)
            pair.Key.ErasePoints(pair.Value, checkCompletion: true);
    }

    private async void ScratchRegion_OnCompleted(object? sender, EventArgs e)
    {
        if (sender is not ScratchSurface surface || !_completedScratchRegions.Add(surface))
            return;

        StatusText.Text = $"已完成 {_completedScratchRegions.Count}/{_scratchRegions.Count} 個刮獎區";
        if (_scratchRegions.Count > 0 && _completedScratchRegions.Count == _scratchRegions.Count)
            await RedeemCurrentTicketAsync();
    }

    private async Task RedeemCurrentTicketAsync()
    {
        if (_currentPending is null || _settlementInProgress)
            return;

        _settlementInProgress = true;
        try
        {
            var prize = await _prizePool.RedeemAsync(_currentPending.Id);
            _currentPending = null;
            ShowSettlementResult(prize);
            StatusText.Text = "已自動兌獎完成";
            await RefreshCurrentUserSummaryAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "兌獎失敗", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "兌獎尚未完成；請勿關閉程式並重試。";
        }
        finally
        {
            _settlementInProgress = false;
        }
    }

    private async void UserButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var users = await _catalog.GetUsersAsync();
            var dialog = new UserDialog(users, _currentUser, _catalog, _database) { Owner = this };
            if (dialog.ShowDialog() != true || dialog.SelectedUser is null)
                return;

            _currentUser = dialog.SelectedUser;
            await LoadCurrentUserAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "使用者", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RenderPendingTicket(PendingTicket pending, TicketDefinition definition)
    {
        TicketMetaText.Text = $"{definition.DisplayName}　${definition.Price:N0}";
        ResultOverlay.Visibility = Visibility.Collapsed;
        ClearTicketOverlay();
        _hasScratched = !string.IsNullOrWhiteSpace(pending.ScratchStateJson);

        using var document = JsonDocument.Parse(pending.PayloadJson);
        var root = document.RootElement;
        var ruleId = root.GetProperty("ruleId").GetString();

        switch (ruleId)
        {
            case "ThreeLine":
                LoadTicketArtwork("ThreeStar", "ticket.png");
                RenderThreeLine(root, pending);
                break;
            default:
                TicketBackgroundImage.Visibility = Visibility.Collapsed;
                TicketPlaceholderPanel.Visibility = Visibility.Visible;
                TicketPlaceholderText.Text = "此玩法的新版票面仍在製作中";
                break;
        }

        UpdateTicketActionState();
    }

    private void RenderThreeLine(JsonElement root, PendingTicket pending)
    {
        var cells = root.GetProperty("cells").EnumerateArray()
            .Select(element => element.GetString() ?? string.Empty)
            .ToArray();
        var maskPath = GetTicketAssetPath("ThreeStar", "silver-star.png");

        for (var i = 0; i < Math.Min(9, cells.Length); i++)
        {
            var position = ThreeLinePositions[i];
            var symbol = new TextBlock
            {
                Width = ThreeLineCellWidth,
                Height = ThreeLineCellHeight,
                Text = GetDisplaySymbol(cells[i], i),
                TextAlignment = TextAlignment.Center,
                FontFamily = new FontFamily("Microsoft JhengHei UI"),
                FontSize = cells[i] == "★" ? 58 : 49,
                FontWeight = FontWeights.Bold,
                Foreground = cells[i] == "★"
                    ? new SolidColorBrush(Color.FromRgb(175, 28, 34))
                    : new SolidColorBrush(Color.FromRgb(98, 58, 27)),
                Padding = new Thickness(0, 26, 0, 0),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(symbol, position.X);
            Canvas.SetTop(symbol, position.Y);
            TicketOverlayCanvas.Children.Add(symbol);

            var scratch = new ScratchSurface
            {
                Width = ThreeLineCellWidth,
                Height = ThreeLineCellHeight,
                BrushRadius = 20,
                CompletionThreshold = 0.78,
                MaskImagePath = maskPath
            };
            scratch.Completed += ScratchRegion_OnCompleted;
            Canvas.SetLeft(scratch, position.X);
            Canvas.SetTop(scratch, position.Y);
            TicketOverlayCanvas.Children.Add(scratch);
            _scratchRegions.Add(scratch);
            scratch.ResetMask();
        }

        var serial = new TextBlock
        {
            Width = 178,
            Height = 24,
            Text = CreateDisplaySerial(pending),
            TextAlignment = TextAlignment.Center,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(83, 32, 25)),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(serial, 55);
        Canvas.SetTop(serial, 630);
        TicketOverlayCanvas.Children.Add(serial);
    }

    private static string CreateDisplaySerial(PendingTicket pending)
    {
        static int ToSixDigits(string id)
        {
            unchecked
            {
                var hash = 17;
                foreach (var ch in id)
                    hash = hash * 31 + ch;
                return Math.Abs(hash % 1_000_000);
            }
        }

        var ticketNumber = ToSixDigits(pending.Id);
        var batchNumber = Math.Abs(ToSixDigits(pending.BatchId) % 1000);
        return $"NO. {ticketNumber:000000}-{batchNumber:000}";
    }

    private static string GetDisplaySymbol(string raw, int index)
    {
        if (raw == "★")
            return "★";

        var symbols = new[] { "●", "◆", "▲", "■", "♥", "✦", "⬟", "✚" };
        var hash = index * 17;
        foreach (var ch in raw)
            hash = unchecked(hash * 31 + ch);
        return symbols[(hash & 0x7FFFFFFF) % symbols.Length];
    }

    private void LoadTicketArtwork(params string[] parts)
    {
        var path = GetTicketAssetPath(parts);
        if (!File.Exists(path))
        {
            TicketBackgroundImage.Visibility = Visibility.Collapsed;
            TicketPlaceholderPanel.Visibility = Visibility.Visible;
            TicketPlaceholderText.Text = "找不到彩券美術資源";
            return;
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();

        TicketBackgroundImage.Source = bitmap;
        TicketBackgroundImage.Visibility = Visibility.Visible;
        TicketPlaceholderPanel.Visibility = Visibility.Collapsed;
    }

    private static string GetTicketAssetPath(params string[] parts)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Tickets");
        foreach (var part in parts)
            path = Path.Combine(path, part);
        return path;
    }

    private void ShowSettlementResult(long prize)
    {
        FooterActionsPanel.Visibility = Visibility.Collapsed;
        ResultOverlay.Visibility = Visibility.Visible;
        SameAgainButton.IsEnabled = _currentDefinition is not null;

        if (prize > 0)
        {
            ResultHeadline.Text = "✦ 恭喜中獎 ✦";
            ResultAmountText.Text = $"${prize:N0}";
        }
        else
        {
            ResultHeadline.Text = "本張未中獎";
            ResultAmountText.Text = "再試一張吧";
        }
    }

    private void UpdateTicketActionState()
    {
        FooterActionsPanel.Visibility = _currentPending is null ? Visibility.Collapsed : Visibility.Visible;
        RevealAllButton.IsEnabled = _currentPending is not null;
        SwapTicketButton.IsEnabled = _currentPending is not null && !_hasScratched;
        SwapTicketButton.ToolTip = _hasScratched ? "已開始刮獎，不能換票" : "換成同款彩券的新序號";
    }

    private void ClearTicketOverlay()
    {
        _isBoardScratching = false;
        if (Mouse.Captured == TicketOverlayCanvas)
            Mouse.Capture(null);

        foreach (var region in _scratchRegions)
            region.Completed -= ScratchRegion_OnCompleted;
        _scratchRegions.Clear();
        _completedScratchRegions.Clear();
        TicketOverlayCanvas.Children.Clear();
    }

    private void ClearTicketDisplay(string message)
    {
        ClearTicketOverlay();
        _hasScratched = false;
        TicketMetaText.Text = string.Empty;
        ResultOverlay.Visibility = Visibility.Collapsed;
        TicketBackgroundImage.Source = null;
        TicketBackgroundImage.Visibility = Visibility.Collapsed;
        TicketPlaceholderPanel.Visibility = Visibility.Visible;
        TicketPlaceholderText.Text = message;
        FooterActionsPanel.Visibility = Visibility.Collapsed;
    }

    private async Task RefreshCurrentUserSummaryAsync()
    {
        if (_currentUser is null)
            return;

        var refreshed = (await _catalog.GetUsersAsync())
            .FirstOrDefault(user => user.Id == _currentUser.Id);
        if (refreshed is not null)
            _currentUser = refreshed;

        UserSummaryText.Text = $"{_currentUser.DisplayName}　損益 {FormatSigned(_currentUser.Net)}";
    }

    private static string FormatSigned(long amount)
        => amount >= 0 ? $"+${amount:N0}" : $"-${Math.Abs(amount):N0}";
}
