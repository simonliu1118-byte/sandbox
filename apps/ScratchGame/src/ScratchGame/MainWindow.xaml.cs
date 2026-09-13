using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ScratchGame.Data;
using ScratchGame.Engine;
using ScratchGame.Models;
using ScratchGame.Services;
using ScratchGame.Views;

namespace ScratchGame;

public partial class MainWindow : Window
{
    private readonly AppDatabase _database = new();
    private readonly CatalogService _catalog;
    private readonly PrizePoolService _prizePool;
    private readonly SeedDataService _seed;
    private readonly BackupService _backup;

    private UserProfile? _currentUser;
    private PendingTicket? _currentPending;
    private TicketDefinition? _currentDefinition;
    private bool _settlementInProgress;

    public MainWindow()
    {
        InitializeComponent();
        _catalog = new CatalogService(_database);
        _prizePool = new PrizePoolService(_database);
        _seed = new SeedDataService(_database);
        _backup = new BackupService(_database);
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
            StatusText.Text = "準備完成";
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
            ClearTicketDisplay("請先建立使用者");
            UserSummaryText.Text = "尚無使用者";
            return;
        }

        await RefreshCurrentUserSummaryAsync();
        _currentPending = await _catalog.GetPendingForUserAsync(_currentUser.Id);
        if (_currentPending is null)
        {
            _currentDefinition = null;
            ClearTicketDisplay("按下「新的一張」開始");
            return;
        }

        var tickets = await _catalog.GetAvailableTicketsAsync();
        _currentDefinition = tickets.FirstOrDefault(t => t.Id == _currentPending.TicketId);
        if (_currentDefinition is null)
        {
            CurrentTicketTitle.Text = "未完成彩券";
            StatusText.Text = "找到 Pending Ticket，但彩券定義目前不可用。";
            return;
        }

        RenderPendingTicket(_currentPending, _currentDefinition);
        StatusText.Text = "已恢復上次尚未完成的彩券";
    }

    private async void NewTicket_OnClick(object sender, RoutedEventArgs e)
    {
        if (_currentUser is null)
            return;

        if (_currentPending is not null)
        {
            MessageBox.Show(
                this,
                "目前仍有一張尚未完成的彩券。請先刮完、全部刮開或放棄本張。",
                "尚有未完成彩券",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            var tickets = await _catalog.GetAvailableTicketsAsync();
            if (tickets.Count == 0)
            {
                MessageBox.Show(this, "目前沒有可用的彩券批次。", "新的一張", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new NewTicketDialog(tickets) { Owner = this };
            if (dialog.ShowDialog() != true || dialog.SelectedTicket is null)
                return;

            _currentDefinition = dialog.SelectedTicket;
            _currentPending = await _prizePool.CreatePendingAsync(
                _currentUser.Id,
                _currentDefinition.Id,
                amount => GamePayloadFactory.Create(
                    _currentDefinition.RuleId,
                    amount,
                    _currentDefinition.Price));

            RenderPendingTicket(_currentPending, _currentDefinition);
            await RefreshCurrentUserSummaryAsync();
            StatusText.Text = "彩券已抽出，開始刮獎";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "無法建立彩券", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RevealAll_OnClick(object sender, RoutedEventArgs e)
    {
        if (_currentPending is null)
        {
            MessageBox.Show(this, "目前沒有尚未完成的彩券。", "全部刮開", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        ScratchLayer.RevealAll();
    }

    private async void ScratchLayer_OnCompleted(object? sender, EventArgs e)
    {
        if (_currentPending is null || _settlementInProgress)
            return;

        _settlementInProgress = true;
        try
        {
            var prize = await _prizePool.RedeemAsync(_currentPending.Id);
            _currentPending = null;
            ResultText.Text = prize > 0
                ? $"恭喜中獎　${prize:N0}"
                : "本張未中獎";
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

    private async void Abandon_OnClick(object sender, RoutedEventArgs e)
    {
        if (_currentPending is null)
        {
            MessageBox.Show(this, "目前沒有可以放棄的彩券。", "放棄本張", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(
            this,
            "放棄後，本張直接以未中獎處理，已投入的面額不退回，也不會揭曉原本隱藏結果。\n\n確定放棄？",
            "放棄本張",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

        try
        {
            await _prizePool.AbandonAsLossAsync(_currentPending.Id);
            _currentPending = null;
            ScratchLayer.Visibility = Visibility.Collapsed;
            ShowSimpleResult("未中獎");
            ResultText.Text = "本張未中獎";
            StatusText.Text = "本張已依未中獎完成";
            await RefreshCurrentUserSummaryAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "無法放棄", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void UserButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var users = await _catalog.GetUsersAsync();
            var dialog = new UserDialog(users, _currentUser, _catalog) { Owner = this };
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
        CurrentTicketTitle.Text = $"{definition.DisplayName}　${definition.Price:N0}";
        TicketCardName.Text = definition.DisplayName;
        TicketPriceText.Text = $"${definition.Price:N0}";
        ResultText.Text = string.Empty;
        ScratchLayer.Visibility = Visibility.Visible;
        ScratchLayer.ResetMask();

        ClearPayloadVisuals();
        using var document = JsonDocument.Parse(pending.PayloadJson);
        var root = document.RootElement;
        var ruleId = root.GetProperty("ruleId").GetString();

        switch (ruleId)
        {
            case "ThreeLine":
                RuleHintText.Text = "刮開九宮格，連成三星即可中獎";
                RenderThreeLine(root);
                break;
            case "LuckyNumberMatch":
                RuleHintText.Text = "刮開號碼區，對中幸運號碼即可中獎";
                RenderFallbackPayload("幸運號碼", root);
                break;
            case "MatchThree":
                RuleHintText.Text = "刮出三個相同結果即可中獎";
                RenderFallbackPayload("三個相同", root);
                break;
            default:
                RuleHintText.Text = "刮開遊戲區";
                RenderFallbackPayload("遊戲結果", root);
                break;
        }
    }

    private void RenderThreeLine(JsonElement root)
    {
        var cells = root.GetProperty("cells").EnumerateArray()
            .Select(element => element.GetString() ?? string.Empty)
            .ToArray();

        var grid = new Grid { Margin = new Thickness(28, 12, 28, 12) };
        for (var i = 0; i < 3; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
        }

        for (var i = 0; i < Math.Min(9, cells.Length); i++)
        {
            var border = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 193, 142)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(7)
            };
            border.Child = new TextBlock
            {
                Text = cells[i],
                FontSize = cells[i] == "★" ? 48 : 30,
                FontWeight = FontWeights.Bold,
                Foreground = cells[i] == "★"
                    ? new SolidColorBrush(Color.FromRgb(174, 34, 41))
                    : new SolidColorBrush(Color.FromRgb(102, 75, 53)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(border, i / 3);
            Grid.SetColumn(border, i % 3);
            grid.Children.Add(border);
        }
        GameGrid.Children.Insert(0, grid);
    }

    private void RenderFallbackPayload(string title, JsonElement root)
    {
        var panel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 28,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        panel.Children.Add(new TextBlock
        {
            Text = root.ToString(),
            MaxWidth = 560,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 14, 0, 0),
            Foreground = Brushes.DimGray
        });
        GameGrid.Children.Insert(0, panel);
    }

    private void ShowSimpleResult(string text)
    {
        ClearPayloadVisuals();
        GameGrid.Children.Insert(0, new TextBlock
        {
            Text = text,
            FontSize = 52,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(110, 92, 78)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });
    }

    private void ClearPayloadVisuals()
    {
        var removable = GameGrid.Children
            .Cast<UIElement>()
            .Where(element => !ReferenceEquals(element, ScratchLayer))
            .ToList();
        foreach (var element in removable)
            GameGrid.Children.Remove(element);
    }

    private void ClearTicketDisplay(string message)
    {
        CurrentTicketTitle.Text = "尚未選擇彩券";
        TicketCardName.Text = "刮刮樂";
        TicketPriceText.Text = "$---";
        RuleHintText.Text = message;
        ResultText.Text = string.Empty;
        ScratchLayer.Visibility = Visibility.Collapsed;
        ClearPayloadVisuals();
        ShowSimpleResult("★");
    }

    private async Task RefreshCurrentUserSummaryAsync()
    {
        if (_currentUser is null)
            return;

        var refreshed = (await _catalog.GetUsersAsync())
            .FirstOrDefault(user => user.Id == _currentUser.Id);
        if (refreshed is not null)
            _currentUser = refreshed;

        UserSummaryText.Text = $"{_currentUser.DisplayName}　投入 ${_currentUser.TotalSpent:N0}　兌獎 ${_currentUser.TotalRedeemed:N0}　損益 {FormatSigned(_currentUser.Net)}";
    }

    private static string FormatSigned(long amount)
        => amount >= 0 ? $"+${amount:N0}" : $"-${Math.Abs(amount):N0}";
}
