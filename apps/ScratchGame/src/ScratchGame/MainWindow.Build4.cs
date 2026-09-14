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

    private void TicketStage_Build4LayoutUpdated(object? sender, EventArgs e)
    {
        // Build 4: the ticket artwork's white scratch interiors start one design pixel
        // farther right/down than the Build 3 mask placement. Keep hit testing and the
        // visible ScratchSurface together by moving the surface itself exactly once.
        foreach (var surface in _scratchRegions)
        {
            if (Equals(surface.Tag, "Build4Aligned"))
                continue;

            var left = Canvas.GetLeft(surface);
            var top = Canvas.GetTop(surface);
            if (!double.IsNaN(left))
                Canvas.SetLeft(surface, left + 1);
            if (!double.IsNaN(top))
                Canvas.SetTop(surface, top + 1);
            surface.Tag = "Build4Aligned";
        }
    }

    private void HideResultOverlay_OnClick(object sender, RoutedEventArgs e)
    {
        if (ResultOverlay.Visibility != Visibility.Visible)
            return;

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
            rotate.BeginAnimation(RotateTransform.AngleProperty,
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
