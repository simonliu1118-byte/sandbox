using System.Windows;
using ScratchGame.Data;
using ScratchGame.Services;

namespace ScratchGame;

public partial class MainWindow
{
    private async void WalletGrant_OnClick(object sender, RoutedEventArgs e)
    {
        if (_currentUser is null)
            return;

        WalletGrantButton.IsEnabled = false;
        try
        {
            var profiles = new UserProfileService(_database);
            _currentUser = await profiles.GrantWalletFundsAsync(
                _currentUser.Id,
                AppDatabase.DefaultWalletGrantAmount);

            await RefreshCurrentUserSummaryAsync();
            StatusText.Text = $"錢包增加 ${AppDatabase.DefaultWalletGrantAmount:N0}";
        }
        catch (Exception ex)
        {
            GameModal.Warning(this, "增加錢包資金", ex.Message);
        }
        finally
        {
            WalletGrantButton.IsEnabled = true;
        }
    }
}
