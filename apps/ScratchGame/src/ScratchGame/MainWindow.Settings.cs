using System.Windows;
using ScratchGame.Views;

namespace ScratchGame;

public partial class MainWindow
{
    private async void SettingsButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsDialog(_database) { Owner = this };
        dialog.ShowDialog();

        // 設定可能停用彩券、匯入新彩券或切換批次；回主畫面後重新載入目前使用者狀態。
        await LoadCurrentUserAsync();
    }
}
