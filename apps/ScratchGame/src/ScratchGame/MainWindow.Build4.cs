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
        if (ResultOverlay.Visibility != Visibility.Visible)
            return;

        // The entrance animation keeps an animation clock on Opacity with HoldEnd.
        // Remove that clock first; otherwise changing the base Opacity value does not
        // actually hide the modal on some result paths.
        ResultOverlay.BeginAnimation(OpacityProperty, null);
        ResultOverlay.Opacity = 0;
        ResultOverlay.IsHitTestVisible = false;
        ResultResumeButton.Visibility = Visibility.Visible;
    }

    private void ShowResultOverlay_OnClick(object sender, RoutedEventArgs e)
    {
        ResultOverlay.BeginAnimation(OpacityProperty, null);
        ResultOverlay.Opacity = 1;
        ResultOverlay.IsHitTestVisible = true;
        ResultResumeButton.Visibility = Visibility.Collapsed;
    }

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
