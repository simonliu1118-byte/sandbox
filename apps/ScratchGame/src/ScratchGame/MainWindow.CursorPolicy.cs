using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace ScratchGame;

public partial class MainWindow
{
    static MainWindow()
    {
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            UIElement.MouseMoveEvent,
            new MouseEventHandler(CursorPolicy_OnMouseEvent),
            handledEventsToo: true);
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            UIElement.MouseEnterEvent,
            new MouseEventHandler(CursorPolicy_OnMouseEvent),
            handledEventsToo: true);
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            UIElement.MouseLeaveEvent,
            new MouseEventHandler(CursorPolicy_OnMouseEvent),
            handledEventsToo: true);
        // Use the bubbling button events as the final routed-event pass. The existing
        // ticket handlers operate on PreviewMouseLeftButtonDown/Up and may temporarily
        // request the coin; the deferred refresh below then applies the single policy.
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            UIElement.MouseLeftButtonDownEvent,
            new MouseButtonEventHandler(CursorPolicy_OnMouseButtonEvent),
            handledEventsToo: true);
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            UIElement.MouseLeftButtonUpEvent,
            new MouseButtonEventHandler(CursorPolicy_OnMouseButtonEvent),
            handledEventsToo: true);
    }

    private static void CursorPolicy_OnMouseEvent(object sender, MouseEventArgs e)
    {
        if (sender is MainWindow window)
            window.QueueCursorPolicyRefresh();
    }

    private static void CursorPolicy_OnMouseButtonEvent(object sender, MouseButtonEventArgs e)
    {
        if (sender is MainWindow window)
            window.QueueCursorPolicyRefresh();
    }

    private void QueueCursorPolicyRefresh()
    {
        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            new Action(ApplyCursorPolicy));
    }

    private void ApplyCursorPolicy()
    {
        // Normal Windows pointer is the default everywhere. The custom coin exists only
        // while the left button is actively scratching inside a Scratch Zone.
        var canShowCoin =
            _currentPending is not null &&
            ResultOverlay.Visibility != Visibility.Visible &&
            ResultOverlay.Opacity > 0 &&
            TicketOverlayCanvas.IsMouseOver &&
            _isBoardScratching &&
            Mouse.LeftButton == MouseButtonState.Pressed;

        if (canShowCoin)
        {
            var point = Mouse.GetPosition(TicketOverlayCanvas);
            canShowCoin = IsPointInsideScratchRegion(point);
            if (canShowCoin)
            {
                SetCoinScratchState(true);
                UpdateCoinCursor(point);
                return;
            }
        }

        SetCoinScratchState(false);
        CoinCursorVisual.Visibility = Visibility.Collapsed;
        TicketOverlayCanvas.Cursor = Cursors.Arrow;
    }
}
