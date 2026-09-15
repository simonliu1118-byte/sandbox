using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ScratchGame.Data;
using ScratchGame.Services;
using ScratchGame.Views;

namespace ScratchGame;

public partial class MainWindow
{
    private readonly MediaPlayer _walletGrantSoundPlayer = new();

    private async void WalletGrant_OnClick(object sender, RoutedEventArgs e)
    {
        if (_currentUser is null || !WalletGrantButton.IsEnabled)
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
            StartWalletGrantEffect();
        }
        catch (Exception ex)
        {
            WalletGrantButton.IsEnabled = true;
            GameModal.Warning(this, "增加錢包資金", ex.Message);
        }
    }

    private void StartWalletGrantEffect()
    {
        var imagePath = UiAssetLoader.UiPath("grant-overlay-01.png");
        UiAssetLoader.TrySetImage(WalletGrantOverlayImage, imagePath);
        PlayWalletGrantSound();

        if (WalletGrantOverlayImage.Source is null)
        {
            WalletGrantButton.IsEnabled = true;
            return;
        }

        WalletGrantOverlay.BeginAnimation(OpacityProperty, null);
        WalletGrantOverlay.Visibility = Visibility.Visible;
        WalletGrantOverlay.Opacity = 1;

        var scale = new ScaleTransform(0.90, 0.90);
        var rotate = new RotateTransform(-5);
        var translate = new TranslateTransform(-16, 0);
        var transforms = new TransformGroup();
        transforms.Children.Add(scale);
        transforms.Children.Add(rotate);
        transforms.Children.Add(translate);
        WalletGrantOverlayImage.RenderTransform = transforms;

        var rotation = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromSeconds(3)
        };
        rotation.KeyFrames.Add(new LinearDoubleKeyFrame(-5, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        rotation.KeyFrames.Add(new LinearDoubleKeyFrame(6, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.34))));
        rotation.KeyFrames.Add(new LinearDoubleKeyFrame(-5, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.68))));
        rotation.KeyFrames.Add(new LinearDoubleKeyFrame(4.5, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.02))));
        rotation.KeyFrames.Add(new LinearDoubleKeyFrame(-3.5, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.36))));
        rotation.KeyFrames.Add(new LinearDoubleKeyFrame(2.5, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.70))));
        rotation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2.05))));
        rotation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(3))));

        var scaleX = CreateWalletGrantScaleAnimation();
        var scaleY = CreateWalletGrantScaleAnimation();

        var movement = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromSeconds(3)
        };
        movement.KeyFrames.Add(new LinearDoubleKeyFrame(-16, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        movement.KeyFrames.Add(new LinearDoubleKeyFrame(18, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.34))));
        movement.KeyFrames.Add(new LinearDoubleKeyFrame(-17, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.68))));
        movement.KeyFrames.Add(new LinearDoubleKeyFrame(14, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.02))));
        movement.KeyFrames.Add(new LinearDoubleKeyFrame(-11, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.36))));
        movement.KeyFrames.Add(new LinearDoubleKeyFrame(8, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.70))));
        movement.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2.05))));
        movement.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(3))));

        var fade = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromSeconds(3),
            FillBehavior = FillBehavior.Stop
        };
        fade.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        fade.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2))));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(3))));
        fade.Completed += (_, _) =>
        {
            WalletGrantOverlay.BeginAnimation(OpacityProperty, null);
            WalletGrantOverlay.Opacity = 0;
            WalletGrantOverlay.Visibility = Visibility.Collapsed;
            WalletGrantOverlayImage.RenderTransform = Transform.Identity;
            WalletGrantButton.IsEnabled = true;
        };

        rotate.BeginAnimation(RotateTransform.AngleProperty, rotation);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
        translate.BeginAnimation(TranslateTransform.XProperty, movement);
        WalletGrantOverlay.BeginAnimation(OpacityProperty, fade);
    }

    private static DoubleAnimationUsingKeyFrames CreateWalletGrantScaleAnimation()
    {
        var animation = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromSeconds(3)
        };
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(0.90, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(1.06, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.34))));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(0.96, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.68))));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(1.08, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.02))));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(0.99, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.36))));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(1.05, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.70))));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(1.00, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2.05))));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(1.00, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(3))));
        return animation;
    }

    private void PlayWalletGrantSound()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Audio", "wallet-grant.wav");
            if (!File.Exists(path))
                return;

            _walletGrantSoundPlayer.Stop();
            _walletGrantSoundPlayer.Open(new Uri(path, UriKind.Absolute));
            _walletGrantSoundPlayer.Volume = 1.0;
            _walletGrantSoundPlayer.Play();
        }
        catch
        {
            // Cosmetic audio failure must not affect the wallet transaction.
        }
    }
}
