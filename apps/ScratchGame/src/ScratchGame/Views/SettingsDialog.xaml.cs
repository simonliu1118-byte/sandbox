using Microsoft.Win32;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using ScratchGame.Data;
using ScratchGame.Services;

namespace ScratchGame.Views;

public partial class SettingsDialog : Window
{
    private readonly AppDatabase _database;
    private readonly TicketAdminService _admin;
    private readonly BackupService _backup;
    private TicketRowViewModel? _expandedRow;

    public SettingsDialog(AppDatabase database)
    {
        InitializeComponent();
        _database = database;
        _admin = new TicketAdminService(database);
        _backup = new BackupService(database);
    }

    private async void Window_OnLoaded(object sender, RoutedEventArgs e)
        => await ReloadAsync();

    private async Task ReloadAsync(string? expandTicketId = null)
    {
        var tickets = await _admin.GetTicketsAsync();
        var rows = tickets.Select(ticket => new TicketRowViewModel(ticket)).ToList();
        TicketItems.ItemsSource = rows;
        _expandedRow = null;

        if (expandTicketId is not null)
        {
            var row = rows.FirstOrDefault(item => item.Ticket.Id == expandTicketId);
            if (row is not null)
                await ExpandRowAsync(row);
        }
    }

    private async void TicketRow_OnClick(object sender, RoutedEventArgs e)
    {
        // Row actions are independent controls. Clicking hide / batch / uninstall
        // must never also toggle the accordion row.
        if (IsInsideButton(e.OriginalSource as DependencyObject))
            return;

        if ((sender as FrameworkElement)?.Tag is not TicketRowViewModel row)
            return;

        if (ReferenceEquals(_expandedRow, row) && row.IsExpanded)
        {
            row.IsExpanded = false;
            _expandedRow = null;
            return;
        }

        if (_expandedRow is not null)
            _expandedRow.IsExpanded = false;
        await ExpandRowAsync(row);
    }

    private static bool IsInsideButton(DependencyObject? source)
    {
        for (var current = source; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is ButtonBase)
                return true;
        }
        return false;
    }

    private async Task ExpandRowAsync(TicketRowViewModel row)
    {
        try
        {
            row.Detail = await _admin.GetTicketDetailAsync(row.Ticket.Id);
            row.IsExpanded = true;
            _expandedRow = row;
        }
        catch (Exception ex)
        {
            GameModal.Warning(this, "彩券詳細資料", ex.Message);
        }
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

    private async void UninstallRow_OnClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if ((sender as FrameworkElement)?.Tag is not TicketRowViewModel row)
            return;

        if (!row.Ticket.CanUninstall)
        {
            GameModal.Info(this, "解除安裝", "Built-in Pack 不可解除安裝；不使用時請改用「隱藏」。");
            return;
        }

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
            var ticketId = await _admin.ImportScratchPackAsync(picker.FileName);
            await ReloadAsync(ticketId);
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
        private bool _isExpanded;
        private TicketAdminDetail? _detail;

        public TicketRowViewModel(TicketAdminItem ticket) => Ticket = ticket;

        public TicketAdminItem Ticket { get; }
        public string PriceText => $"${Ticket.Price:N0}";
        public string WinRateText => Ticket.PublishedWinRate.ToString("P2");
        public string ExpandGlyph => IsExpanded ? "▴" : "▾";
        public string IssueSizeText => Detail is null ? "—" : $"{Detail.IssueSize:N0} 張";
        public string TicketsPerBookText => Detail is null ? "—" : $"{Detail.TicketsPerBook:N0} 張";
        public string BookCountText => Detail is null ? "—" : $"{Detail.BookCount:N0} 本";
        public string RemainingText => Detail is null ? "—" : $"{Detail.RemainingCount:N0} 張";
        public bool CanUninstall => Ticket.CanUninstall;
        public string BatchActionText => Ticket.ActiveBatchNumber is null ? "發行新一批" : "發行下一批";
        public IReadOnlyList<PrizeRowViewModel> PrizeRows => Detail?.PrizeRows.Select(row => new PrizeRowViewModel(row)).ToList() ?? [];

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value)
                    return;
                _isExpanded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ExpandGlyph));
            }
        }

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