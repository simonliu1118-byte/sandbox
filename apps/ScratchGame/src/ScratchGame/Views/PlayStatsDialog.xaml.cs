using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ScratchGame.Models;

namespace ScratchGame.Views;

public partial class PlayStatsDialog : Window
{
    private readonly FrameworkElement? _anchor;
    private Point _returnOffset;
    private bool _closingAnimationStarted;

    public PlayStatsDialog(UserProfile profile)
        : this(profile, null)
    {
    }

    public PlayStatsDialog(UserProfile profile, FrameworkElement? anchor)
    {
        InitializeComponent();
        _anchor = anchor;

        UserNameText.Text = profile.DisplayName;
        WalletText.Text = $"目前錢包　${profile.WalletBalance:N0}";
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

    private async void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        ConfigureToOwnerContent();
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

        _returnOffset = CalculateAnchorOffset();
        Opacity = 1;
        BeginOpenAnimation();
    }

    private void ConfigureToOwnerContent()
    {
        if (Owner?.Content is not FrameworkElement ownerContent || ownerContent.ActualWidth <= 0 || ownerContent.ActualHeight <= 0)
            return;

        var screenPoint = ownerContent.PointToScreen(new Point(0, 0));
        var presentationSource = PresentationSource.FromVisual(Owner);
        var fromDevice = presentationSource?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var origin = fromDevice.Transform(screenPoint);

        Left = origin.X;
        Top = origin.Y;
        Width = ownerContent.ActualWidth;
        Height = ownerContent.ActualHeight;
    }

    private Point CalculateAnchorOffset()
    {
        if (_anchor is null || Owner?.Content is not FrameworkElement ownerContent)
            return new Point(Math.Max(260, ActualWidth * 0.34), Math.Max(250, ActualHeight * 0.36));

        try
        {
            var anchorCenter = _anchor.TranslatePoint(
                new Point(_anchor.ActualWidth / 2.0, _anchor.ActualHeight / 2.0),
                ownerContent);
            return new Point(
                anchorCenter.X - ownerContent.ActualWidth / 2.0,
                anchorCenter.Y - ownerContent.ActualHeight / 2.0);
        }
        catch
        {
            return new Point(Math.Max(260, ActualWidth * 0.34), Math.Max(250, ActualHeight * 0.36));
        }
    }

    private void BeginOpenAnimation()
    {
        _closingAnimationStarted = false;
        ClearAnimations();

        DimLayer.Opacity = 0;
        ScrollCard.Opacity = 0.18;
        ScrollScale.ScaleX = 0.28;
        ScrollScale.ScaleY = 0.18;
        ScrollTranslate.X = _returnOffset.X;
        ScrollTranslate.Y = _returnOffset.Y;

        DimLayer.BeginAnimation(
            OpacityProperty,
            CreateAnimation(0, 1, 230));
        ScrollCard.BeginAnimation(
            OpacityProperty,
            CreateAnimation(0.18, 1, 250));
        ScrollScale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            CreateAnimation(0.28, 1, 360));
        ScrollScale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            CreateAnimation(0.18, 1, 360));
        ScrollTranslate.BeginAnimation(
            TranslateTransform.XProperty,
            CreateAnimation(_returnOffset.X, 0, 360));
        ScrollTranslate.BeginAnimation(
            TranslateTransform.YProperty,
            CreateAnimation(_returnOffset.Y, 0, 360));
    }

    private void Close_OnClick(object sender, RoutedEventArgs e)
        => BeginCloseAnimation();

    private void Window_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        BeginCloseAnimation();
        e.Handled = true;
    }

    private void BeginCloseAnimation()
    {
        if (_closingAnimationStarted)
            return;

        _closingAnimationStarted = true;
        IsHitTestVisible = false;
        ClearAnimations();

        DimLayer.BeginAnimation(
            OpacityProperty,
            CreateAnimation(1, 0, 240));
        ScrollCard.BeginAnimation(
            OpacityProperty,
            CreateAnimation(1, 0.18, 260));
        ScrollScale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            CreateAnimation(1, 0.28, 320));
        ScrollScale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            CreateAnimation(1, 0.18, 320));
        ScrollTranslate.BeginAnimation(
            TranslateTransform.XProperty,
            CreateAnimation(0, _returnOffset.X, 320));

        var flyBack = CreateAnimation(0, _returnOffset.Y, 320);
        flyBack.Completed += (_, _) => Close();
        ScrollTranslate.BeginAnimation(TranslateTransform.YProperty, flyBack);
    }

    private void ClearAnimations()
    {
        DimLayer.BeginAnimation(OpacityProperty, null);
        ScrollCard.BeginAnimation(OpacityProperty, null);
        ScrollScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        ScrollScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        ScrollTranslate.BeginAnimation(TranslateTransform.XProperty, null);
        ScrollTranslate.BeginAnimation(TranslateTransform.YProperty, null);
    }

    private static DoubleAnimation CreateAnimation(double from, double to, int durationMs)
        => new(from, to, TimeSpan.FromMilliseconds(durationMs))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.HoldEnd
        };
}
