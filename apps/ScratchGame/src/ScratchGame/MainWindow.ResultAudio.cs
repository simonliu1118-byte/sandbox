using System.Windows;
using System.Windows.Media;
using ScratchGame.Services;

namespace ScratchGame;

public partial class MainWindow
{
    private readonly MediaPlayer _loseSoundPlayer = new();
    private bool _loseSoundDiagnosticsAttached;

    private async void ResultOverlay_OnVisibilityChangedExtended(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        // Keep the existing settlement presentation as the single owner of win effects.
        ResultOverlay_OnVisibilityChanged(sender, e);

        if (ResultOverlay.Visibility != Visibility.Visible)
        {
            ResetResultTransitionUi();
            return;
        }

        // ShowSettlementResult sets the loss headline after making the result overlay visible.
        // Yield once so the final settlement text is available, then use the explicit loss state.
        // Do not parse ResultAmountText here: a losing result deliberately displays "再試一張吧"
        // instead of "$0", so parsing UI text can never be a reliable loss trigger.
        await Task.Yield();
        if (!string.Equals(ResultHeadline.Text, "本張未中獎", StringComparison.Ordinal))
            return;

        PlayLoseSound();
    }

    private void PlayLoseSound()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Audio", "lose.wav");
        try
        {
            if (!File.Exists(path))
            {
                RuntimeAssetLog.Missing(path, "lose result audio");
                return;
            }

            EnsureLoseSoundDiagnostics(path);
            _loseSoundPlayer.Stop();
            _loseSoundPlayer.Open(new Uri(path, UriKind.Absolute));
            _loseSoundPlayer.Volume = 1.0;
            _loseSoundPlayer.Play();
        }
        catch (Exception ex)
        {
            RuntimeAssetLog.Error(path, "lose result audio", ex);
            // Result audio is cosmetic and must never block settlement.
        }
    }

    private void EnsureLoseSoundDiagnostics(string path)
    {
        if (_loseSoundDiagnosticsAttached)
            return;

        _loseSoundDiagnosticsAttached = true;
        _loseSoundPlayer.MediaFailed += (_, args) =>
        {
            var error = args.ErrorException ?? new InvalidOperationException("MediaPlayer failed to play lose result audio.");
            RuntimeAssetLog.Error(path, "lose result audio", error);
        };
    }
}
