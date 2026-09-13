using Microsoft.Win32;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using ScratchGame.Data;
using ScratchGame.Services;

namespace ScratchGame.Views;

public partial class SettingsDialog : Window
{
    private readonly TicketAdminService _admin;
    private readonly BackupService _backup;
    private TicketRowViewModel? _selectedRow;
    private TicketRowViewModel? _expandedRow;

    public SettingsDialog(AppDatabase database)
    {
        InitializeComponent();
        UiAssetLoader.TrySetImage(DialogBackgroundImage, UiAssetLoader.UiPath("dialog_bg.png"));
        _admin = new TicketAdminService(database);
        _backup = new BackupService(database);
    }

    private async void Window_OnLoaded(object sender, RoutedEventArgs e) => await ReloadAsync();

    private async Task ReloadAsync(string? selectTicketId = null, bool expand = false)
    {
        var tickets = await _admin.GetTicketsAsync();
        var rows = tickets.Select(ticket => new TicketRowViewModel(ticket)).ToList();
        TicketItems.ItemsSource = rows;
        _selectedRow = selectTicketId is null ? null : rows.FirstOrDefault(row => row.Ticket.Id == selectTicketId);
        _expandedRow = null;
        if (expand && _selectedRow is not null)
            await ExpandRowAsync(_selectedRow);
    }

    private async void TicketRow_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not TicketRowViewModel row)
            return;

        _selectedRow = row;
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
            MessageBox.Show(this, ex.Message, "彩券詳細資料", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private TicketAdminItem? GetSelectedTicket()
    {
        if (_selectedRow is not null)
            return _selectedRow.Ticket;
        MessageBox.Show(this, "請先點選一張彩券。", "彩券管理", MessageBoxButton.OK, MessageBoxImage.Information);
        return null;
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
            await ReloadAsync(ticketId, expand: true);
            MessageBox.Show(this, "彩券包匯入完成。請選擇「發行新一批」建立第 1 批後即可遊玩。", "ScratchPack", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "匯入失敗", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void NewBatch_OnClick(object sender, RoutedEventArgs e)
    {
        var ticket = GetSelectedTicket();
        if (ticket is null)
            return;

        var text = ticket.ActiveBatchNumber is null
            ? $"確定發行「{ticket.DisplayName}」第 1 批？"
            : $"確定結束「{ticket.DisplayName}」第 {ticket.ActiveBatchNumber} 批並發行下一批？\n\n舊批次之後不能再刮。";
        if (MessageBox.Show(this, text, "發行新一批", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            var number = await _admin.StartNextBatchAsync(ticket.Id);
            await ReloadAsync(ticket.Id, expand: true);
            MessageBox.Show(this, $"第 {number} 批已開始。", "發行完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "無法發行新一批", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void ToggleEnabled_OnClick(object sender, RoutedEventArgs e)
    {
        var ticket = GetSelectedTicket();
        if (ticket is null)
            return;

        try
        {
            await _admin.SetEnabledAsync(ticket.Id, !ticket.Enabled);
            await ReloadAsync(ticket.Id, expand: true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "彩券管理", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void Delete_OnClick(object sender, RoutedEventArgs e)
    {
        var ticket = GetSelectedTicket();
        if (ticket is null)
            return;
        if (MessageBox.Show(this, $"確定刪除尚未發行的「{ticket.DisplayName}」？", "刪除彩券", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            await _admin.DeleteNeverIssuedAsync(ticket.Id);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "無法刪除", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void Backup_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = await _backup.CreateBackupAsync();
            MessageBox.Show(this, $"備份完成：\n{path}", "資料備份", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "備份失敗", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Close_OnClick(object sender, RoutedEventArgs e) => Close();

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
        public IReadOnlyList<PrizeRowViewModel> PrizeRows => Detail?.PrizeRows.Select(row => new PrizeRowViewModel(row)).ToList() ?? [];

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value) return;
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
