using System.Windows;
using System.Windows.Media;
using ScratchGame.Services;

namespace ScratchGame;

public partial class MainWindow
{
    private readonly MediaPlayer _loseSoundPlayer = new();
    private bool _loseSoundDiagnosticsAttached;

    private void ResultOverlay_OnVisibilityChangedExtended(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        // Keep the existing settlement presentation as the single owner of win effects.
        // Losing audio is triggered directly from ShowSettlementResult(prize) so it never
        // depends on display text or result-overlay visibility transitions.
        ResultOverlay_OnVisibilityChanged(sender, e);
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
