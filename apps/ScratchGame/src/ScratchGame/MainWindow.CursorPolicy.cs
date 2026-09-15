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
        // V0.4 cursor rule:
        // - while a ticket is still pending, every point inside the Stage uses the coin;
        // - active scratching changes only the coin artwork to its side/scratch state;
        // - everywhere else, and immediately after redemption, use the normal pointer.
        var stagePoint = Mouse.GetPosition(StageBackgroundImage);
        var insideStage =
            stagePoint.X >= 0 && stagePoint.Y >= 0 &&
            stagePoint.X <= StageBackgroundImage.ActualWidth &&
            stagePoint.Y <= StageBackgroundImage.ActualHeight;

        if (_currentPending is not null && insideStage)
        {
            Mouse.OverrideCursor = Cursors.None;
            SetCoinScratchState(_isBoardScratching && Mouse.LeftButton == MouseButtonState.Pressed);
            UpdateCoinCursor(Mouse.GetPosition(TicketOverlayCanvas));
            return;
        }

        Mouse.OverrideCursor = null;
        SetCoinScratchState(false);
        CoinCursorVisual.Visibility = Visibility.Collapsed;
        TicketOverlayCanvas.Cursor = Cursors.Arrow;
    }
}
