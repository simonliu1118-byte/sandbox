using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using ScratchGame.Controls;

namespace ScratchGame;

public partial class MainWindow
{
    private readonly Random _build4DebrisRandom = new();
    private Point? _build4LastDebrisPoint;
    private DateTime _build4LastDebrisUtc = DateTime.MinValue;
    private bool _build4MouseHookInstalled;
    private bool _resultTransitionInProgress;

    private void TicketStage_Build4LayoutUpdated(object? sender, EventArgs e)
    {
        // Keep the debris listener in the same routed mouse pipeline even though the
        // existing PreviewMouseMove handler marks scratch movement as handled.
        if (!_build4MouseHookInstalled)
        {
            TicketOverlayCanvas.AddHandler(
                UIElement.MouseMoveEvent,
                new MouseEventHandler(TicketOverlayCanvas_Build4MouseMove),
                handledEventsToo: true);
            _build4MouseHookInstalled = true;
        }

        // Build 4 alignment corrections belong only to pre-ScratchPack local tickets.
        // ScratchPack zones and serial placement are authoritative Pack geometry and
        // must never receive the legacy +1 offset or fixed Y=675 serial override.
        if (_activeScratchPack is not null)
            return;

        // The actual white scratch interiors start one design pixel farther right/down
        // than Build 3. Move the symbol and ScratchSurface together so rendering,
        // hit-testing and mask geometry still have exactly one coordinate owner.
        foreach (var surface in _scratchRegions)
        {
            if (Equals(surface.Tag, "Build4Aligned"))
                continue;

            var left = Canvas.GetLeft(surface);
            var top = Canvas.GetTop(surface);
            if (double.IsNaN(left) || double.IsNaN(top))
                continue;

            var symbol = TicketOverlayCanvas.Children
                .OfType<TextBlock>()
                .FirstOrDefault(text =>
                    Math.Abs(Canvas.GetLeft(text) - left) < 0.01 &&
                    Math.Abs(Canvas.GetTop(text) - top) < 0.01 &&
                    Math.Abs(text.Width - surface.Width) < 0.01 &&
                    Math.Abs(text.Height - surface.Height) < 0.01);

            if (symbol is not null)
            {
                Canvas.SetLeft(symbol, left + 1);
                Canvas.SetTop(symbol, top + 1);
            }

            Canvas.SetLeft(surface, left + 1);
            Canvas.SetTop(surface, top + 1);
            surface.Tag = "Build4Aligned";
        }

        // Legacy Build 4 ticket art keeps only a compact real footer instead of the old
        // artificial fill area. Keep the serial inside that footer for legacy tickets only.
        var serialBadge = TicketOverlayCanvas.Children
            .OfType<Border>()
            .FirstOrDefault(border => Equals(border.Tag, "ProgramSerialBadge"));
        if (serialBadge is not null)
            Canvas.SetTop(serialBadge, 675);
    }

    private void HideResultOverlay_OnClick(object sender, RoutedEventArgs e)
    {
        if (_resultTransitionInProgress ||
            ResultOverlay.Visibility != Visibility.Visible ||
            ResultOverlay.Opacity <= 0.01)
            return;

        _resultTransitionInProgress = true;
        ResultOverlay.IsHitTestVisible = false;
        ResultResumeButton.IsHitTestVisible = false;

        ClearResultOverlayTransformAnimations();
        ResultOverlay.BeginAnimation(
            OpacityProperty,
            CreateResultUiAnimation(1, 0, 180));
        ResultOverlayScale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            CreateResultUiAnimation(1, 0.94, 180));
        ResultOverlayScale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            CreateResultUiAnimation(1, 0.94, 180));
        ResultOverlayTranslate.BeginAnimation(
            TranslateTransform.XProperty,
            CreateResultUiAnimation(0, 10, 180));
        ResultOverlayTranslate.BeginAnimation(
            TranslateTransform.YProperty,
            CreateResultUiAnimation(0, 8, 180));

        PrepareResultTransitionChip(0, 0, 1, 0);
        ResultTransitionChip.Visibility = Visibility.Visible;
        var (targetX, targetY) = GetResultTransitionTarget();

        ResultTransitionChip.BeginAnimation(
            OpacityProperty,
            CreateResultUiAnimation(0, 1, 90, 70));
        ResultTransitionScale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            CreateResultUiAnimation(1, 0.88, 300, 70));
        ResultTransitionScale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            CreateResultUiAnimation(1, 0.88, 300, 70));
        ResultTransitionTranslate.BeginAnimation(
            TranslateTransform.XProperty,
            CreateResultUiAnimation(0, targetX, 300, 70));

        var flyY = CreateResultUiAnimation(0, targetY, 300, 70);
        flyY.Completed += (_, _) => CompleteHideResultTransition();
        ResultTransitionTranslate.BeginAnimation(TranslateTransform.YProperty, flyY);
    }

    private void ShowResultOverlay_OnClick(object sender, RoutedEventArgs e)
    {
        if (_resultTransitionInProgress || ResultResumeButton.Visibility != Visibility.Visible)
            return;

        _resultTransitionInProgress = true;
        ResultResumeButton.IsHitTestVisible = false;
        var (targetX, targetY) = GetResultTransitionTarget();

        ResultResumeButton.BeginAnimation(
            OpacityProperty,
            CreateResultUiAnimation(1, 0, 110));
        ResultResumeScale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            CreateResultUiAnimation(1, 0.90, 110));
        ResultResumeScale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            CreateResultUiAnimation(1, 0.90, 110));

        PrepareResultTransitionChip(targetX, targetY, 0.88, 1);
        ResultTransitionChip.Visibility = Visibility.Visible;
        ResultTransitionTranslate.BeginAnimation(
            TranslateTransform.XProperty,
            CreateResultUiAnimation(targetX, 0, 260, 45));
        ResultTransitionScale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            CreateResultUiAnimation(0.88, 1, 260, 45));
        ResultTransitionScale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            CreateResultUiAnimation(0.88, 1, 260, 45));
        ResultTransitionChip.BeginAnimation(
            OpacityProperty,
            CreateResultUiAnimation(1, 0.18, 100, 205));

        var flyY = CreateResultUiAnimation(targetY, 0, 260, 45);
        flyY.Completed += (_, _) => CompleteShowResultTransition();
        ResultTransitionTranslate.BeginAnimation(TranslateTransform.YProperty, flyY);
    }

    private void CompleteHideResultTransition()
    {
        ClearResultTransitionChipAnimations();
        ResultTransitionChip.Visibility = Visibility.Collapsed;
        ResultTransitionChip.Opacity = 0;

        ResultOverlay.BeginAnimation(OpacityProperty, null);
        ResultOverlay.Opacity = 0;
        ClearResultOverlayTransformAnimations();
        ResultOverlayScale.ScaleX = 1;
        ResultOverlayScale.ScaleY = 1;
        ResultOverlayTranslate.X = 0;
        ResultOverlayTranslate.Y = 0;

        ResultResumeButton.Visibility = Visibility.Visible;
        ResultResumeButton.Opacity = 0;
        ResultResumeScale.ScaleX = 0.90;
        ResultResumeScale.ScaleY = 0.90;

        var fade = CreateResultUiAnimation(0, 1, 130);
        fade.Completed += (_, _) =>
        {
            ResultResumeButton.BeginAnimation(OpacityProperty, null);
            ResultResumeButton.Opacity = 1;
            ResultResumeScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            ResultResumeScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            ResultResumeScale.ScaleX = 1;
            ResultResumeScale.ScaleY = 1;
            ResultResumeButton.IsHitTestVisible = true;
            _resultTransitionInProgress = false;
            ApplyCursorPolicy();
        };
        ResultResumeButton.BeginAnimation(OpacityProperty, fade);
        ResultResumeScale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            CreateResultUiAnimation(0.90, 1, 130));
        ResultResumeScale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            CreateResultUiAnimation(0.90, 1, 130));
    }

    private void CompleteShowResultTransition()
    {
        ResultResumeButton.BeginAnimation(OpacityProperty, null);
        ResultResumeButton.Visibility = Visibility.Collapsed;
        ResultResumeButton.Opacity = 1;
        ResultResumeScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        ResultResumeScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        ResultResumeScale.ScaleX = 1;
        ResultResumeScale.ScaleY = 1;

        ClearResultTransitionChipAnimations();
        ResultTransitionChip.Visibility = Visibility.Collapsed;
        ResultTransitionChip.Opacity = 0;

        ClearResultOverlayTransformAnimations();
        ResultOverlay.BeginAnimation(OpacityProperty, null);
        ResultOverlay.Opacity = 0;
        ResultOverlay.IsHitTestVisible = true;
        ResultOverlayScale.ScaleX = 0.94;
        ResultOverlayScale.ScaleY = 0.94;
        ResultOverlayTranslate.X = 10;
        ResultOverlayTranslate.Y = 8;

        var fade = CreateResultUiAnimation(0, 1, 190);
        fade.Completed += (_, _) =>
        {
            ResultOverlay.BeginAnimation(OpacityProperty, null);
            ResultOverlay.Opacity = 1;
            ClearResultOverlayTransformAnimations();
            ResultOverlayScale.ScaleX = 1;
            ResultOverlayScale.ScaleY = 1;
            ResultOverlayTranslate.X = 0;
            ResultOverlayTranslate.Y = 0;
            _resultTransitionInProgress = false;
            ApplyCursorPolicy();
        };
        ResultOverlay.BeginAnimation(OpacityProperty, fade);
        ResultOverlayScale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            CreateResultUiAnimation(0.94, 1, 190));
        ResultOverlayScale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            CreateResultUiAnimation(0.94, 1, 190));
        ResultOverlayTranslate.BeginAnimation(
            TranslateTransform.XProperty,
            CreateResultUiAnimation(10, 0, 190));
        ResultOverlayTranslate.BeginAnimation(
            TranslateTransform.YProperty,
            CreateResultUiAnimation(8, 0, 190));
    }

    private void ResetResultTransitionUi()
    {
        _resultTransitionInProgress = false;
        ClearResultTransitionChipAnimations();
        ResultTransitionChip.Visibility = Visibility.Collapsed;
        ResultTransitionChip.Opacity = 0;
        ResultTransitionScale.ScaleX = 1;
        ResultTransitionScale.ScaleY = 1;
        ResultTransitionTranslate.X = 0;
        ResultTransitionTranslate.Y = 0;

        ResultResumeButton.BeginAnimation(OpacityProperty, null);
        ResultResumeScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        ResultResumeScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        ResultResumeButton.Visibility = Visibility.Collapsed;
        ResultResumeButton.Opacity = 1;
        ResultResumeButton.IsHitTestVisible = true;
        ResultResumeScale.ScaleX = 1;
        ResultResumeScale.ScaleY = 1;

        ClearResultOverlayTransformAnimations();
        ResultOverlayScale.ScaleX = 1;
        ResultOverlayScale.ScaleY = 1;
        ResultOverlayTranslate.X = 0;
        ResultOverlayTranslate.Y = 0;
        ResultOverlay.IsHitTestVisible = true;
    }

    private void PrepareResultTransitionChip(double x, double y, double scale, double opacity)
    {
        ClearResultTransitionChipAnimations();
        ResultTransitionTranslate.X = x;
        ResultTransitionTranslate.Y = y;
        ResultTransitionScale.ScaleX = scale;
        ResultTransitionScale.ScaleY = scale;
        ResultTransitionChip.Opacity = opacity;
    }

    private void ClearResultTransitionChipAnimations()
    {
        ResultTransitionChip.BeginAnimation(OpacityProperty, null);
        ResultTransitionTranslate.BeginAnimation(TranslateTransform.XProperty, null);
        ResultTransitionTranslate.BeginAnimation(TranslateTransform.YProperty, null);
        ResultTransitionScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        ResultTransitionScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
    }

    private void ClearResultOverlayTransformAnimations()
    {
        ResultOverlayScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        ResultOverlayScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        ResultOverlayTranslate.BeginAnimation(TranslateTransform.XProperty, null);
        ResultOverlayTranslate.BeginAnimation(TranslateTransform.YProperty, null);
    }

    private (double X, double Y) GetResultTransitionTarget()
    {
        const double chipWidth = 154;
        const double chipHeight = 38;
        const double edge = 18;
        var x = Math.Max(0, (StageUiOverlay.ActualWidth - chipWidth) / 2.0 - edge);
        var y = Math.Max(0, (StageUiOverlay.ActualHeight - chipHeight) / 2.0 - edge);
        return (x, y);
    }

    private static DoubleAnimation CreateResultUiAnimation(
        double from,
        double to,
        int durationMs,
        int beginMs = 0)
        => new(from, to, TimeSpan.FromMilliseconds(durationMs))
        {
            BeginTime = TimeSpan.FromMilliseconds(beginMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.HoldEnd
        };

    private void TicketOverlayCanvas_Build4MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isBoardScratching || e.LeftButton != MouseButtonState.Pressed || _currentPending is null)
            return;

        var point = e.GetPosition(TicketOverlayCanvas);
        if (!IsPointInsideScratchRegion(point))
            return;

        var now = DateTime.UtcNow;
        if (_build4LastDebrisPoint is Point last)
        {
            var dx = point.X - last.X;
            var dy = point.Y - last.Y;
            if (dx * dx + dy * dy < 36 && (now - _build4LastDebrisUtc).TotalMilliseconds < 28)
                return;
        }

        _build4LastDebrisPoint = point;
        _build4LastDebrisUtc = now;
        EmitScratchDebris(point);
    }

    private bool IsPointInsideScratchRegion(Point point)
    {
        foreach (var region in _scratchRegions)
        {
            var left = Canvas.GetLeft(region);
            var top = Canvas.GetTop(region);
            if (double.IsNaN(left) || double.IsNaN(top))
                continue;

            if (point.X >= left && point.X <= left + region.Width &&
                point.Y >= top && point.Y <= top + region.Height)
                return true;
        }
        return false;
    }

    private void EmitScratchDebris(Point point)
    {
        while (ScratchDebrisLayer.Children.Count > 72)
            ScratchDebrisLayer.Children.RemoveAt(0);

        var count = _build4DebrisRandom.Next(2, 5);
        for (var i = 0; i < count; i++)
        {
            var size = _build4DebrisRandom.NextDouble() * 4.5 + 2.5;
            var shard = new Rectangle
            {
                Width = size * 1.55,
                Height = size,
                RadiusX = 1.2,
                RadiusY = 1.2,
                Fill = new SolidColorBrush(Color.FromArgb(
                    (byte)_build4DebrisRandom.Next(145, 220),
                    (byte)_build4DebrisRandom.Next(150, 196),
                    (byte)_build4DebrisRandom.Next(150, 196),
                    (byte)_build4DebrisRandom.Next(150, 196))),
                RenderTransformOrigin = new Point(0.5, 0.5),
                IsHitTestVisible = false
            };

            var transform = new TransformGroup();
            var rotate = new RotateTransform(_build4DebrisRandom.Next(-45, 46));
            var translate = new TranslateTransform();
            transform.Children.Add(rotate);
            transform.Children.Add(translate);
            shard.RenderTransform = transform;

            var startX = point.X + _build4DebrisRandom.NextDouble() * 18 - 9;
            var startY = point.Y + _build4DebrisRandom.NextDouble() * 12 - 6;
            Canvas.SetLeft(shard, startX);
            Canvas.SetTop(shard, startY);
            ScratchDebrisLayer.Children.Add(shard);

            var duration = TimeSpan.FromMilliseconds(_build4DebrisRandom.Next(260, 430));
            var dx = _build4DebrisRandom.NextDouble() * 34 - 17;
            var dy = _build4DebrisRandom.NextDouble() * 24 + 7;
            translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, dx, duration));
            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, dy, duration));
            rotate.BeginAnimation(
                RotateTransform.AngleProperty,
                new DoubleAnimation(rotate.Angle, rotate.Angle + _build4DebrisRandom.Next(-120, 121), duration));

            var fade = new DoubleAnimation(0.9, 0, duration)
            {
                BeginTime = TimeSpan.FromMilliseconds(70),
                FillBehavior = FillBehavior.Stop
            };
            fade.Completed += (_, _) => ScratchDebrisLayer.Children.Remove(shard);
            shard.BeginAnimation(OpacityProperty, fade);
        }
    }
}