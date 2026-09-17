using Microsoft.Win32;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ScratchGame.Data;
using ScratchGame.Services;

namespace ScratchGame.Views;

public partial class SettingsDialog : Window
{
    private readonly AppDatabase _database;
    private readonly TicketAdminService _admin;
    private readonly BackupService _backup;
    private ScrollViewer? _ticketScrollViewer;
    private ScrollBar? _ticketScrollBar;
    private Window? _ticketInfoWindow;

    public SettingsDialog(AppDatabase database)
    {
        InitializeComponent();
        _database = database;
        _admin = new TicketAdminService(database);
        _backup = new BackupService(database);
    }

    private async void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        await ReloadAsync();
        AlignTicketListChrome();
    }

    private async Task ReloadAsync()
    {
        TicketInfoPopup.IsOpen = false;
        TicketInfoPopup.DataContext = null;

        var tickets = await _admin.GetTicketsAsync();
        TicketItems.ItemsSource = tickets.Select(ticket => new TicketRowViewModel(ticket)).ToList();

        Dispatcher.BeginInvoke(new Action(AlignTicketListChrome), DispatcherPriority.Loaded);
    }

    private async void InfoRow_OnClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if ((sender as FrameworkElement)?.Tag is not TicketRowViewModel row)
            return;

        try
        {
            row.Detail = await _admin.GetTicketDetailAsync(row.Ticket.Id);
            ShowTicketInfoModal(row);
        }
        catch (Exception ex)
        {
            GameModal.Warning(this, "彩券詳細資料", ex.Message);
        }
    }

    private void ShowTicketInfoModal(TicketRowViewModel row)
    {
        _ticketInfoWindow?.Close();

        var modal = new Window
        {
            Owner = this,
            Title = "彩券資訊",
            Width = Math.Max(ActualWidth, Width),
            Height = Math.Max(ActualHeight, Height),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false
        };

        var overlay = new Grid
        {
            Background = BrushFrom("#A8120806")
        };

        var cardHost = new Grid
        {
            Width = 700,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransformOrigin = new Point(0.5, 0.5),
            Opacity = 0
        };
        var flyTransform = new TranslateTransform(0, 14);
        cardHost.RenderTransform = flyTransform;

        var shadow = new Border
        {
            Margin = new Thickness(8, 10, 0, 0),
            CornerRadius = new CornerRadius(18),
            Background = BrushFrom("#18000000"),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Color.FromRgb(0x10, 0x06, 0x04),
                BlurRadius = 28,
                ShadowDepth = 8,
                Opacity = 0.72
            }
        };

        var card = new Border
        {
            Padding = new Thickness(24, 22, 24, 20),
            CornerRadius = new CornerRadius(18),
            Background = BrushFrom("#3A1B15"),
            BorderBrush = BrushFrom("#C28A49"),
            BorderThickness = new Thickness(1.5)
        };

        var body = new StackPanel();
        body.Children.Add(CreateInfoHeader(modal, row));
        body.Children.Add(CreateInfoSummary(row));
        body.Children.Add(CreatePrizePoolPanel(row));

        var closeButton = new Button
        {
            Content = "關閉",
            Width = 126,
            Margin = new Thickness(0, 18, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            Style = FindResource("DialogSecondaryButton") as Style
        };
        closeButton.Click += (_, _) => modal.Close();
        body.Children.Add(closeButton);

        card.Child = body;
        cardHost.Children.Add(shadow);
        cardHost.Children.Add(card);
        overlay.Children.Add(cardHost);
        modal.Content = overlay;

        modal.Loaded += (_, _) =>
        {
            var duration = new Duration(TimeSpan.FromMilliseconds(170));
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            cardHost.BeginAnimation(
                OpacityProperty,
                new DoubleAnimation(0, 1, duration) { EasingFunction = ease });

            flyTransform.BeginAnimation(
                TranslateTransform.YProperty,
                new DoubleAnimation(14, 0, duration) { EasingFunction = ease });
        };

        modal.Closed += (_, _) =>
        {
            if (ReferenceEquals(_ticketInfoWindow, modal))
                _ticketInfoWindow = null;
        };

        _ticketInfoWindow = modal;
        modal.ShowDialog();
    }

    private Grid CreateInfoHeader(Window modal, TicketRowViewModel row)
    {
        var header = new Grid();

        var titlePanel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            MaxWidth = 440
        };
        titlePanel.Children.Add(new TextBlock
        {
            Text = row.Ticket.DisplayName,
            FontFamily = new FontFamily("Microsoft JhengHei UI"),
            FontSize = 24,
            FontWeight = FontWeights.Bold,
            Foreground = BrushFrom("#FFE6A6"),
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        var subtitle = new TextBlock
        {
            Text = $"{row.SourceText}  •  {row.Ticket.RuleDisplayName}  •  {row.Ticket.BatchText}",
            Margin = new Thickness(0, 5, 0, 0),
            FontSize = 12,
            Foreground = BrushFrom("#C69B7D"),
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        titlePanel.Children.Add(subtitle);
        header.Children.Add(titlePanel);

        if (row.CanUninstall)
        {
            var uninstallButton = new Button
            {
                Content = "解除安裝",
                Width = 92,
                Height = 30,
                Padding = new Thickness(8, 2, 8, 2),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Style = FindResource("DialogButton") as Style
            };
            uninstallButton.Click += async (_, _) => await UninstallFromInfoModalAsync(modal, row);
            header.Children.Add(uninstallButton);
        }

        return header;
    }

    private Grid CreateInfoSummary(TicketRowViewModel row)
    {
        var summary = new Grid
        {
            Margin = new Thickness(0, 16, 0, 0)
        };

        for (var i = 0; i < 3; i++)
            summary.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        summary.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        summary.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        AddInfoStat(summary, 0, 0, "面額", row.PriceText);
        AddInfoStat(summary, 0, 1, "中獎率", row.WinRateText);
        AddInfoStat(summary, 0, 2, "剩餘張數", row.RemainingText, accent: true);
        AddInfoStat(summary, 1, 0, "總發行", row.IssueSizeText);
        AddInfoStat(summary, 1, 1, "每本", row.TicketsPerBookText);
        AddInfoStat(summary, 1, 2, "總本數", row.BookCountText);

        return summary;
    }

    private void AddInfoStat(Grid grid, int row, int column, string label, string value, bool accent = false)
    {
        var border = new Border
        {
            Margin = new Thickness(5, row == 0 ? 5 : 2, 5, 5),
            Padding = new Thickness(10, 8, 10, 8),
            CornerRadius = new CornerRadius(9),
            Background = BrushFrom("#2D1511"),
            BorderBrush = BrushFrom("#654137"),
            BorderThickness = new Thickness(1)
        };

        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontFamily = new FontFamily("Microsoft JhengHei UI"),
            FontSize = 11,
            Foreground = BrushFrom("#B99176"),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        });
        panel.Children.Add(new TextBlock
        {
            Text = value,
            Margin = new Thickness(0, 3, 0, 0),
            FontFamily = new FontFamily("Microsoft JhengHei UI"),
            FontSize = row == 0 ? 17 : 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = accent ? BrushFrom("#FFE29A") : BrushFrom("#FFF0CF"),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        });

        border.Child = panel;
        Grid.SetRow(border, row);
        Grid.SetColumn(border, column);
        grid.Children.Add(border);
    }

    private FrameworkElement CreatePrizePoolPanel(TicketRowViewModel row)
    {
        var panel = new StackPanel
        {
            Margin = new Thickness(5, 15, 5, 0)
        };

        panel.Children.Add(new TextBlock
        {
            Text = "獎池分配",
            Margin = new Thickness(0, 0, 0, 7),
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushFrom("#FFE1A0"),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        });

        var tableBorder = new Border
        {
            BorderBrush = BrushFrom("#6A4336"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            ClipToBounds = true
        };

        var table = new StackPanel();
        table.Children.Add(CreatePrizeRow("獎金", "發行張數", "剩餘", true));

        foreach (var prize in row.PrizeRows)
            table.Children.Add(CreatePrizeRow(prize.AmountText, prize.InitialText, prize.RemainingText, false));

        tableBorder.Child = table;
        panel.Children.Add(tableBorder);
        return panel;
    }

    private Border CreatePrizeRow(string amount, string initial, string remaining, bool header)
    {
        var grid = new Grid
        {
            MinHeight = header ? 32 : 27,
            Background = BrushFrom(header ? "#51291F" : "#301712")
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        AddPrizeCell(grid, 0, amount, header);
        AddPrizeCell(grid, 1, initial, header);
        AddPrizeCell(grid, 2, remaining, header);

        return new Border
        {
            BorderBrush = BrushFrom("#4E3028"),
            BorderThickness = header ? new Thickness(0) : new Thickness(0, 0, 0, 1),
            Child = grid
        };
    }

    private void AddPrizeCell(Grid grid, int column, string text, bool header)
    {
        var cell = new TextBlock
        {
            Text = text,
            Padding = header ? new Thickness(10, 7, 10, 7) : new Thickness(10, 5, 10, 5),
            Foreground = BrushFrom(header ? "#E8C493" : "#E9D4BC"),
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (!header && column == 0)
            cell.Foreground = BrushFrom("#FFF0D4");

        Grid.SetColumn(cell, column);
        grid.Children.Add(cell);
    }

    private async Task UninstallFromInfoModalAsync(Window modal, TicketRowViewModel row)
    {
        if (!row.Ticket.CanUninstall)
            return;

        if (!GameModal.Confirm(
                modal,
                "解除安裝 Pack",
                $"確定解除安裝「{row.Ticket.DisplayName}」？\n\n這會移除 Imported Pack、批次／票池資料與可重建快取。已完成的遊玩統計不受影響；若目前仍有此 Pack 的未完成彩券，系統才會拒絕解除安裝。",
                "解除安裝",
                "取消"))
            return;

        try
        {
            await _admin.UninstallPackAsync(row.Ticket.Id);
            modal.Close();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            GameModal.Warning(modal, "無法解除安裝", ex.Message);
        }
    }

    private static SolidColorBrush BrushFrom(string hex)
        => (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;

    private void AlignTicketListChrome()
    {
        TicketItems.UpdateLayout();

        var scrollViewer = FindVisualAncestor<ScrollViewer>(TicketItems);
        if (scrollViewer is not null)
        {
            scrollViewer.ApplyTemplate();

            var presenter = scrollViewer.Template.FindName("PART_ScrollContentPresenter", scrollViewer) as ScrollContentPresenter;
            var scrollBar = scrollViewer.Template.FindName("PART_VerticalScrollBar", scrollViewer) as ScrollBar;

            if (presenter is not null && VisualTreeHelper.GetParent(presenter) is Grid templateGrid)
            {
                var span = Math.Max(1, templateGrid.ColumnDefinitions.Count);
                Grid.SetColumn(presenter, 0);
                Grid.SetColumnSpan(presenter, span);
                presenter.Margin = new Thickness(0);

                if (scrollBar is not null)
                {
                    Grid.SetColumn(scrollBar, 0);
                    Grid.SetColumnSpan(scrollBar, span);
                    scrollBar.HorizontalAlignment = HorizontalAlignment.Right;
                    scrollBar.Width = 14;
                    scrollBar.Margin = new Thickness(0, 4, 3, 4);
                    scrollBar.Background = Brushes.Transparent;
                }
            }

            _ticketScrollViewer = scrollViewer;
            _ticketScrollBar = scrollBar;
            scrollViewer.ScrollChanged -= TicketScrollViewer_OnScrollChanged;
            scrollViewer.ScrollChanged += TicketScrollViewer_OnScrollChanged;
            UpdateTicketScrollBarVisibility();
        }

        var operationHeader = FindVisualDescendants<TextBlock>(this)
            .FirstOrDefault(block => block.Text == "操作");
        if (operationHeader is not null && VisualTreeHelper.GetParent(operationHeader) is Grid headerGrid)
            headerGrid.Margin = new Thickness(0, 0, 18, 0);

        foreach (var item in TicketItems.Items)
        {
            if (TicketItems.ItemContainerGenerator.ContainerFromItem(item) is not ContentPresenter container)
                continue;

            container.ApplyTemplate();
            if (FindVisualDescendant<Border>(container) is { Child: Grid rowGrid })
                rowGrid.Margin = new Thickness(0, 0, 18, 0);
        }
    }

    private void TicketScrollViewer_OnScrollChanged(object sender, ScrollChangedEventArgs e)
        => UpdateTicketScrollBarVisibility();

    private void UpdateTicketScrollBarVisibility()
    {
        if (_ticketScrollViewer is null || _ticketScrollBar is null)
            return;

        _ticketScrollBar.Visibility = _ticketScrollViewer.ScrollableHeight > 0.5
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private static T? FindVisualAncestor<T>(DependencyObject child) where T : DependencyObject
    {
        var current = child;
        while (current is not null)
        {
            if (current is T match)
                return match;
            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static T? FindVisualDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
                return match;

            var nested = FindVisualDescendant<T>(child);
            if (nested is not null)
                return nested;
        }

        return null;
    }

    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
                yield return match;

            foreach (var nested in FindVisualDescendants<T>(child))
                yield return nested;
        }
    }

    private void CloseInfoPopup_OnClick(object sender, RoutedEventArgs e)
    {
        TicketInfoPopup.IsOpen = false;
        TicketInfoPopup.DataContext = null;
    }

    private async void HideRow_OnClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if ((sender as FrameworkElement)?.Tag is not TicketRowViewModel row)
            return;

        try
        {
            await _admin.SetHiddenAsync(row.Ticket.Id, hidden: true);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            GameModal.Warning(this, "隱藏 Pack", ex.Message);
        }
    }

    private async void StartBatchRow_OnClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if ((sender as FrameworkElement)?.Tag is not TicketRowViewModel row)
            return;

        var ticket = row.Ticket;
        var firstMessage = ticket.ActiveBatchNumber is null
            ? $"準備發行「{ticket.DisplayName}」第 1 批。\n\n發行後會建立完整固定票池。"
            : $"準備結束「{ticket.DisplayName}」第 {ticket.ActiveBatchNumber} 批並發行下一批。\n\n舊批次結束後不能再抽票。";

        if (!GameModal.Confirm(this, "發行新一批", firstMessage, "下一步", "取消"))
            return;

        var nextNumber = (ticket.ActiveBatchNumber ?? 0) + 1;
        if (!GameModal.Confirm(
                this,
                "再次確認",
                $"確定發行「{ticket.DisplayName}」第 {nextNumber} 批？\n\n這個動作會變更目前可抽取的批次。",
                "確定發行",
                "返回"))
            return;

        try
        {
            var number = await _admin.StartNextBatchAsync(ticket.Id);
            await ReloadAsync();
            GameModal.Info(this, "發行完成", $"「{ticket.DisplayName}」第 {number} 批已開始。");
        }
        catch (Exception ex)
        {
            GameModal.Warning(this, "無法發行新一批", ex.Message);
        }
    }

    private async void UninstallInfo_OnClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (TicketInfoPopup.DataContext is not TicketRowViewModel row)
            return;

        if (!row.Ticket.CanUninstall)
        {
            GameModal.Info(this, "解除安裝", "Built-in Pack 不可解除安裝；不使用時請改用「隱藏」。");
            return;
        }

        TicketInfoPopup.IsOpen = false;

        if (!GameModal.Confirm(
                this,
                "解除安裝 Pack",
                $"確定解除安裝「{row.Ticket.DisplayName}」？\n\n這會移除 Imported Pack、批次／票池資料與可重建快取。已完成的遊玩統計不受影響；若目前仍有此 Pack 的未完成彩券，系統才會拒絕解除安裝。",
                "解除安裝",
                "取消"))
            return;

        try
        {
            await _admin.UninstallPackAsync(row.Ticket.Id);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            GameModal.Warning(this, "無法解除安裝", ex.Message);
        }
    }

    private async void Import_OnClick(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog
        {
            Title = "匯入彩券包",
            Filter = "ScratchPack (*.scratchpack)|*.scratchpack",
            CheckFileExists = true,
            Multiselect = false
        };
        if (picker.ShowDialog(this) != true)
            return;

        try
        {
            await _admin.ImportScratchPackAsync(picker.FileName);
            await ReloadAsync();
            GameModal.Info(this, "ScratchPack", "彩券包匯入完成，第 1 批已自動發行，可以直接開始遊玩。");
        }
        catch (Exception ex)
        {
            GameModal.Warning(this, "匯入失敗", ex.Message);
        }
    }

    private async void HiddenPacks_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new HiddenPacksDialog(_database) { Owner = this };
        dialog.ShowDialog();
        await ReloadAsync();
    }

    private async void Backup_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = await _backup.CreateBackupAsync();
            GameModal.Info(this, "資料備份", $"備份完成：\n{path}");
        }
        catch (Exception ex)
        {
            GameModal.Warning(this, "備份失敗", ex.Message);
        }
    }

    private void Close_OnClick(object sender, RoutedEventArgs e)
        => Close();

    private sealed class TicketRowViewModel : INotifyPropertyChanged
    {
        private TicketAdminDetail? _detail;

        public TicketRowViewModel(TicketAdminItem ticket) => Ticket = ticket;

        public TicketAdminItem Ticket { get; }
        public string PriceText => $"${Ticket.Price:N0}";
        public string WinRateText => Ticket.PublishedWinRate.ToString("P2");
        public string SourceText => Ticket.IsBuiltIn ? "內建 Pack" : "Imported Pack";
        public string IssueSizeText => Detail is null ? "—" : $"{Detail.IssueSize:N0} 張";
        public string TicketsPerBookText => Detail is null ? "—" : $"{Detail.TicketsPerBook:N0} 張";
        public string BookCountText => Detail is null ? "—" : $"{Detail.BookCount:N0} 本";
        public string RemainingText => Detail is null ? "—" : $"{Detail.RemainingCount:N0} 張";
        public bool CanUninstall => Ticket.CanUninstall;
        public string UninstallHint => Ticket.IsBuiltIn
            ? "內建 Pack 不可解除安裝；不使用時可從主清單隱藏。"
            : "解除安裝會移除 Pack 與目前批次／票池資料。";
        public string BatchActionText => Ticket.ActiveBatchNumber is null ? "發行新一批" : "發行下一批";
        public IReadOnlyList<PrizeRowViewModel> PrizeRows
            => Detail?.PrizeRows.Select(row => new PrizeRowViewModel(row)).ToList() ?? [];

        public TicketAdminDetail? Detail
        {
            get => _detail;
            set
            {
                _detail = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IssueSizeText));
                OnPropertyChanged(nameof(TicketsPerBookText));
                OnPropertyChanged(nameof(BookCountText));
                OnPropertyChanged(nameof(RemainingText));
                OnPropertyChanged(nameof(PrizeRows));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private sealed class PrizeRowViewModel(TicketPrizePoolRow row)
    {
        public string AmountText => $"${row.Amount:N0}";
        public string InitialText => row.InitialCount.ToString("N0");
        public string RemainingText => row.RemainingCount.ToString("N0");
    }
}
