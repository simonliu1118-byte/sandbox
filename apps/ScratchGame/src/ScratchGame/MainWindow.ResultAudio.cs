using System.Windows;
using System.Windows.Media;

namespace ScratchGame;

public partial class MainWindow
{
    private readonly MediaPlayer _loseSoundPlayer = new();

    private async void ResultOverlay_OnVisibilityChangedExtended(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        // Keep the existing settlement presentation as the single owner of win effects.
        ResultOverlay_OnVisibilityChanged(sender, e);

        if (ResultOverlay.Visibility != Visibility.Visible)
            return;

        await Task.Yield();
        if (!TryReadPrizeAmount(ResultAmountText.Text, out var prize) || prize > 0)
            return;

        PlayLoseSound();
    }

    private void PlayLoseSound()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Audio", "lose.wav");
            if (!File.Exists(path))
                return;

            _loseSoundPlayer.Stop();
            _loseSoundPlayer.Open(new Uri(path, UriKind.Absolute));
            _loseSoundPlayer.Volume = 1.0;
            _loseSoundPlayer.Play();
        }
        catch
        {
            // Result audio is cosmetic and must never block settlement.
        }
    }
}
