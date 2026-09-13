using Microsoft.Win32;
using System.Windows;
using ScratchGame.Data;
using ScratchGame.Services;

namespace ScratchGame.Views;

public partial class SettingsDialog : Window
{
    private readonly TicketAdminService _admin;
    private readonly BackupService _backup;

    public SettingsDialog(AppDatabase database)
    {
        InitializeComponent();
        _admin = new TicketAdminService(database);
        _backup = new BackupService(database);
    }

    private async void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        await ReloadAsync();
    }

    private async Task ReloadAsync(string? selectTicketId = null)
    {
        var tickets = await _admin.GetTicketsAsync();
        TicketGrid.ItemsSource = tickets;
        if (selectTicketId is not null)
            TicketGrid.SelectedItem = tickets.FirstOrDefault(t => t.Id == selectTicketId);
        else if (tickets.Count > 0 && TicketGrid.SelectedIndex < 0)
            TicketGrid.SelectedIndex = 0;
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
            MessageBox.Show(this, "彩券包匯入完成。請選擇「發行新一批」建立第 1 批後即可遊玩。", "ScratchPack", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "匯入失敗", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void NewBatch_OnClick(object sender, RoutedEventArgs e)
    {
        if (TicketGrid.SelectedItem is not TicketAdminItem ticket)
            return;

        var text = ticket.ActiveBatchNumber is null
            ? $"確定發行「{ticket.DisplayName}」第 1 批？"
            : $"確定結束「{ticket.DisplayName}」第 {ticket.ActiveBatchNumber} 批並發行下一批？\n\n舊批次之後不能再刮。";
        if (MessageBox.Show(this, text, "發行新一批", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            var number = await _admin.StartNextBatchAsync(ticket.Id);
            await ReloadAsync(ticket.Id);
            MessageBox.Show(this, $"第 {number} 批已開始。", "發行完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "無法發行新一批", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void ToggleEnabled_OnClick(object sender, RoutedEventArgs e)
    {
        if (TicketGrid.SelectedItem is not TicketAdminItem ticket)
            return;
        try
        {
            await _admin.SetEnabledAsync(ticket.Id, !ticket.Enabled);
            await ReloadAsync(ticket.Id);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "彩券管理", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void Delete_OnClick(object sender, RoutedEventArgs e)
    {
        if (TicketGrid.SelectedItem is not TicketAdminItem ticket)
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

    private void Close_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
