using System.Windows;
using ScratchGame.Views;

namespace ScratchGame;

public partial class MainWindow
{
    private async void PlayStatsFooter_OnClick(object sender, RoutedEventArgs e)
    {
        if (_currentUser is null || !PlayStatsFooterButton.IsEnabled)
        {
            GameModal.Info(this, "遊玩紀錄", "目前尚未選擇玩家。");
            return;
        }

        PlayStatsFooterButton.IsEnabled = false;
        try
        {
            await RefreshCurrentUserSummaryAsync();
            if (_currentUser is null)
                return;

            var dialog = new PlayStatsDialog(_currentUser, PlayStatsFooterButton)
            {
                Owner = this
            };
            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            GameModal.Warning(this, "遊玩紀錄", ex.Message);
        }
        finally
        {
            PlayStatsFooterButton.IsEnabled = true;
        }
    }
}
