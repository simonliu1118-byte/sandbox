using System.Windows;
using ScratchGame.Models;

namespace ScratchGame.Views;

public partial class PlayStatsDialog : Window
{
    public PlayStatsDialog(UserProfile profile)
    {
        InitializeComponent();

        UserNameText.Text = profile.DisplayName;
        WalletText.Text = $"${profile.WalletBalance:N0}";
        CompletedText.Text = $"{profile.CompletedTicketCount:N0} 張";
        WinCountText.Text = $"{profile.WinCount:N0} 張";
        WinRateText.Text = profile.WinRate.ToString("P2");
        MaxPrizeText.Text = $"${profile.MaxPrize:N0}";
        TotalSpentText.Text = $"${profile.TotalSpent:N0}";
        TotalRedeemedText.Text = $"${profile.TotalRedeemed:N0}";
        NetText.Text = profile.Net >= 0
            ? $"+${profile.Net:N0}"
            : $"-${Math.Abs(profile.Net):N0}";
        GrantText.Text = $"{profile.GrantCount:N0} 次　｜　累計 ${profile.GrantTotalAmount:N0}";
    }

    private void Close_OnClick(object sender, RoutedEventArgs e)
        => Close();
}
