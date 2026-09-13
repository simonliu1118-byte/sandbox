using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ScratchGame.Services;

namespace ScratchGame;

public partial class MainWindow
{
    private TicketNumberService? _enhancementTicketNumbers;
    private readonly MediaPlayer _enhancementWinSoundPlayer = new();
    private string? _enhancementPendingId;
    private string? _enhancementSerial;
    private int _enhancementPriceDisplay;
    private bool _enhancementRefreshInProgress;

    private void Window_OnEnhancementsReady(object? sender, EventArgs e)
    {
        _enhancementTicketNumbers ??= new TicketNumberService(_database);
    }

    private void TicketOverlayCanvas_OnMouseEnter(object sender, MouseEventArgs e)
    {
        if (_currentPending is null || ResultOverlay.Visibility == Visibility.Visible)
            return;

        TicketOverlayCanvas.Cursor = Cursors.None;
        CoinCursorVisual.Visibility = Visibility.Visible;
        UpdateCoinCursor(e.GetPosition(TicketOverlayCanvas));
    }

    private void TicketOverlayCanvas_OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
            CoinCursorVisual.Visibility = Visibility.Collapsed;
    }

    private void TicketOverlayCanvas_OnCoinMouseMove(object sender, MouseEventArgs e)
    {
        if (_currentPending is null || ResultOverlay.Visibility == Visibility.Visible)
            return;
        UpdateCoinCursor(e.GetPosition(TicketOverlayCanvas));
    }

    private void UpdateCoinCursor(Point point)
    {
        CoinCursorVisual.Visibility = Visibility.Visible;
        Canvas.SetLeft(CoinCursorVisual, point.X - 45);
        Canvas.SetTop(CoinCursorVisual, point.Y - 45);
    }

    private async void TicketOverlayCanvas_OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (_enhancementRefreshInProgress || _currentPending is null || _currentDefinition is null)
            return;

        if (_enhancementPendingId == _currentPending.Id && !string.IsNullOrWhiteSpace(_enhancementSerial))
        {
            var currentSerialText = FindSerialTextBlock();
            var badgeReady = _enhancementPriceDisplay != 1 || HasProgramPriceBadge();
            if (currentSerialText?.Text == _enhancementSerial && badgeReady)
                return;

            if (currentSerialText is not null)
                currentSerialText.Text = _enhancementSerial;
            RemoveProgramPriceBadge();
            if (_enhancementPriceDisplay == 1)
                AddProgramPriceBadge(_currentDefinition.Price);
            return;
        }

        _enhancementRefreshInProgress = true;
        try
        {
            _enhancementTicketNumbers ??= new TicketNumberService(_database);
            var metadata = await _enhancementTicketNumbers.GetMetadataAsync(_currentDefinition.Id);
            var serial = await _enhancementTicketNumbers.GetOrCreateDisplayNumberAsync(_currentPending);

            var serialText = FindSerialTextBlock();
            if (serialText is not null)
                serialText.Text = serial;

            RemoveProgramPriceBadge();
            if (metadata.PriceDisplay == 1)
                AddProgramPriceBadge(_currentDefinition.Price);

            _enhancementPendingId = _currentPending.Id;
            _enhancementSerial = serial;
            _enhancementPriceDisplay = metadata.PriceDisplay;
        }
        catch
        {
            // 票號／面額呈現失敗不可阻止主要刮獎流程。
        }
        finally
        {
            _enhancementRefreshInProgress = false;
        }
    }

    private TextBlock? FindSerialTextBlock()
        => TicketOverlayCanvas.Children
            .OfType<TextBlock>()
            .FirstOrDefault(text =>
                Math.Abs(Canvas.GetTop(text) - 630) < 2 &&
                text.FontFamily.Source.Contains("Consolas", StringComparison.OrdinalIgnoreCase));

    private bool HasProgramPriceBadge()
        => TicketOverlayCanvas.Children.OfType<FrameworkElement>()
            .Any(element => Equals(element.Tag, "ProgramPriceBadge"));

    private void RemoveProgramPriceBadge()
    {
        var existing = TicketOverlayCanvas.Children.OfType<FrameworkElement>()
            .Where(element => Equals(element.Tag, "ProgramPriceBadge"))
            .ToList();
        foreach (var element in existing)
            TicketOverlayCanvas.Children.Remove(element);
    }

    private void AddProgramPriceBadge(long price)
    {
        var outer = new Border
        {
            Tag = "ProgramPriceBadge",
            Width = 176,
            Height = 80,
            CornerRadius = new CornerRadius(16),
            Background = new SolidColorBrush(Color.FromRgb(181, 119, 25)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(255, 225, 124)),
            BorderThickness = new Thickness(3),
            IsHitTestVisible = false
        };
        var inner = new Border
        {
            Margin = new Thickness(5),
            CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(Color.FromRgb(255, 246, 207)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(150, 77, 18)),
            BorderThickness = new Thickness(2),
            Child = new TextBlock
            {
                Text = $"NT${price:N0}",
                FontFamily = new FontFamily("Microsoft JhengHei UI"),
                FontSize = 35,
                FontWeight = FontWeights.Black,
                Foreground = new SolidColorBrush(Color.FromRgb(196, 25, 28)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            }
        };
        outer.Child = inner;
        Canvas.SetLeft(outer, 882);
        Canvas.SetTop(outer, 42);
        TicketOverlayCanvas.Children.Add(outer);
    }

    private async void ResultOverlay_OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (ResultOverlay.Visibility != Visibility.Visible)
            return;

        CoinCursorVisual.Visibility = Visibility.Collapsed;
        TicketOverlayCanvas.Cursor = Cursors.Arrow;

        if (_enhancementPendingId is not null)
        {
            try
            {
                _enhancementTicketNumbers ??= new TicketNumberService(_database);
                await _enhancementTicketNumbers.MarkConsumedAsync(_enhancementPendingId);
            }
            catch
            {
                // 票號狀態是輔助資料，不得讓兌獎結果視窗失敗。
            }
        }

        if (!TryReadPrizeAmount(ResultAmountText.Text, out var prize) || prize <= 0)
            return;

        try
        {
            var fileName = prize >= 50_000 ? "big-win.wav" : "small-win.wav";
            var path = Path.Combine(AppContext.BaseDirectory, "Audio", fileName);
            if (!File.Exists(path))
                return;

            _enhancementWinSoundPlayer.Stop();
            _enhancementWinSoundPlayer.Open(new Uri(path, UriKind.Absolute));
            _enhancementWinSoundPlayer.Volume = 1.0;
            _enhancementWinSoundPlayer.Play();
        }
        catch
        {
            // 音效故障不得影響遊戲資料。
        }
    }

    private static bool TryReadPrizeAmount(string text, out long amount)
    {
        var normalized = text.Replace("$", string.Empty).Trim();
        return long.TryParse(normalized, NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out amount)
            || long.TryParse(normalized, NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out amount);
    }
}
