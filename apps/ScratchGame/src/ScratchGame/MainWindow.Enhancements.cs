using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using ScratchGame.Services;
using ScratchGame.Views;

namespace ScratchGame;

public partial class MainWindow
{
    private enum SettlementOrigin { ManualScratch, RevealAll, SystemAuto }

    private TicketNumberService? _enhancementTicketNumbers;
    private readonly MediaPlayer _enhancementWinSoundPlayer = new();
    private string? _enhancementPendingId;
    private string? _enhancementSerial;
    private int _enhancementPriceDisplay;
    private bool _enhancementRefreshInProgress;
    private bool _enhancementEventsAttached;
    private bool _enhancementAllowClose;
    private bool _enhancementClosingInProgress;
    private bool _coinIsScratching;
    private SettlementOrigin _settlementOrigin = SettlementOrigin.ManualScratch;

    private async void Window_OnLoadedEnhanced(object sender, RoutedEventArgs e)
    {
        try
        {
            StatusText.Text = "正在初始化…";
            await _database.InitializeAsync();
            await _seed.EnsureSeedDataAsync();
            await _backup.BackupIfDueAsync();

            var users = await _catalog.GetUsersAsync();
            _currentUser = users.FirstOrDefault();
            await LoadCurrentUserAsync();

            if (_currentPending is not null)
            {
                _settlementOrigin = SettlementOrigin.SystemAuto;
                await SystemRevealAndRedeemAsync("偵測到上次未完成的彩券，已直接開獎並完成兌獎");
            }
            else
            {
                StatusText.Text = "準備完成，挑一張彩券開始吧";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "啟動失敗", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "初始化失敗";
        }
    }

    private void Window_OnEnhancementsReady(object? sender, EventArgs e)
    {
        _enhancementTicketNumbers ??= new TicketNumberService(_database);
        if (_enhancementEventsAttached) return;
        _enhancementEventsAttached = true;

        TicketOverlayCanvas.AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(CoinPreviewMouseDown), true);
        TicketOverlayCanvas.AddHandler(Mouse.PreviewMouseUpEvent, new MouseButtonEventHandler(CoinPreviewMouseUp), true);
        TicketOverlayCanvas.AddHandler(Mouse.MouseMoveEvent, new MouseEventHandler(CoinCapturedMouseMove), true);
    }

    private async void Window_OnClosingEnhanced(object? sender, CancelEventArgs e)
    {
        if (_enhancementAllowClose || _currentPending is null) return;
        e.Cancel = true;
        if (_enhancementClosingInProgress) return;

        var confirm = MessageBox.Show(this,
            "目前彩券尚未完成。\n\n關閉程式會直接揭曉這張彩券並完成兌獎，確定要關閉嗎？",
            "尚有未完成彩券", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        _enhancementClosingInProgress = true;
        try
        {
            _settlementOrigin = SettlementOrigin.SystemAuto;
            if (!await SystemRevealAndRedeemAsync("關閉前已直接開獎並完成兌獎")) return;
            _enhancementAllowClose = true;
            Close();
        }
        finally { _enhancementClosingInProgress = false; }
    }

    private async void UserButtonEnhanced_OnClick(object sender, RoutedEventArgs e)
    {
        if (_currentPending is not null)
        {
            var confirm = MessageBox.Show(this,
                "目前彩券尚未完成。\n\n切換使用者會直接揭曉這張彩券並完成兌獎，是否繼續？",
                "切換使用者", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            _settlementOrigin = SettlementOrigin.SystemAuto;
            if (!await SystemRevealAndRedeemAsync("切換使用者前已直接開獎並完成兌獎")) return;
        }

        try
        {
            var users = await _catalog.GetUsersAsync();
            var dialog = new UserDialog(users, _currentUser, _catalog, _database) { Owner = this };
            if (dialog.ShowDialog() != true || dialog.SelectedUser is null) return;

            _currentUser = dialog.SelectedUser;
            await LoadCurrentUserAsync();
            if (_currentPending is not null)
            {
                _settlementOrigin = SettlementOrigin.SystemAuto;
                await SystemRevealAndRedeemAsync("偵測到此使用者上次未完成的彩券，已直接開獎並完成兌獎");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "使用者", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RevealAllEnhanced_OnClick(object sender, RoutedEventArgs e)
    {
        _settlementOrigin = SettlementOrigin.RevealAll;
        RevealAll_OnClick(sender, e);
    }

    private async Task<bool> SystemRevealAndRedeemAsync(string successStatus)
    {
        if (_currentPending is null || _settlementInProgress) return _currentPending is null;

        _settlementInProgress = true;
        try
        {
            foreach (var region in _scratchRegions)
            {
                region.Completed -= ScratchRegion_OnCompleted;
                region.RevealAll();
            }

            var prize = await _prizePool.RedeemAsync(_currentPending.Id);
            _currentPending = null;
            ShowSettlementResult(prize);
            StatusText.Text = successStatus;
            await RefreshCurrentUserSummaryAsync();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "自動兌獎失敗", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "兌獎尚未完成，請先處理目前彩券。";
            return false;
        }
        finally { _settlementInProgress = false; }
    }

    private void CoinPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || _currentPending is null || ResultOverlay.Visibility == Visibility.Visible) return;
        _settlementOrigin = SettlementOrigin.ManualScratch;
        SetCoinScratchState(true);
        UpdateCoinCursor(e.GetPosition(TicketOverlayCanvas));
    }

    private void CoinPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        SetCoinScratchState(false);
        if (_currentPending is not null && ResultOverlay.Visibility != Visibility.Visible)
            UpdateCoinCursor(e.GetPosition(TicketOverlayCanvas));
    }

    private void CoinCapturedMouseMove(object sender, MouseEventArgs e)
    {
        if (_currentPending is null || ResultOverlay.Visibility == Visibility.Visible) return;
        if (e.LeftButton == MouseButtonState.Pressed && !_coinIsScratching) SetCoinScratchState(true);
        UpdateCoinCursor(e.GetPosition(TicketOverlayCanvas));
    }

    private void TicketOverlayCanvas_OnMouseEnter(object sender, MouseEventArgs e)
    {
        if (_currentPending is null || ResultOverlay.Visibility == Visibility.Visible) return;
        TicketOverlayCanvas.Cursor = Cursors.None;
        CoinCursorVisual.Visibility = Visibility.Visible;
        SetCoinScratchState(e.LeftButton == MouseButtonState.Pressed);
        UpdateCoinCursor(e.GetPosition(TicketOverlayCanvas));
    }

    private void TicketOverlayCanvas_OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) return;
        SetCoinScratchState(false);
        CoinCursorVisual.Visibility = Visibility.Collapsed;
    }

    private void TicketOverlayCanvas_OnCoinMouseMove(object sender, MouseEventArgs e)
    {
        if (_currentPending is null || ResultOverlay.Visibility == Visibility.Visible) return;
        UpdateCoinCursor(e.GetPosition(TicketOverlayCanvas));
    }

    private void SetCoinScratchState(bool scratching)
    {
        _coinIsScratching = scratching;
        CoinIdleVisual.Visibility = scratching ? Visibility.Collapsed : Visibility.Visible;
        CoinScratchVisual.Visibility = scratching ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateCoinCursor(Point point)
    {
        CoinCursorVisual.Visibility = Visibility.Visible;
        TicketOverlayCanvas.Cursor = Cursors.None;
        Canvas.SetLeft(CoinCursorVisual, point.X - 38);
        Canvas.SetTop(CoinCursorVisual, _coinIsScratching ? point.Y - 72 : point.Y - 38);
    }

    private async void TicketOverlayCanvas_OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (_enhancementRefreshInProgress || _currentPending is null || _currentDefinition is null) return;

        if (_enhancementPendingId == _currentPending.Id && !string.IsNullOrWhiteSpace(_enhancementSerial))
        {
            EnsureSerialBadge(_enhancementSerial);
            if (_enhancementPriceDisplay == 1 && !HasProgramPriceBadge()) AddProgramPriceBadge(_currentDefinition.Price);
            return;
        }

        _enhancementRefreshInProgress = true;
        try
        {
            _enhancementTicketNumbers ??= new TicketNumberService(_database);
            var metadata = await _enhancementTicketNumbers.GetMetadataAsync(_currentDefinition.Id);
            var serial = await _enhancementTicketNumbers.GetOrCreateDisplayNumberAsync(_currentPending);
            EnsureSerialBadge(serial);
            RemoveProgramPriceBadge();
            if (metadata.PriceDisplay == 1) AddProgramPriceBadge(_currentDefinition.Price);

            _enhancementPendingId = _currentPending.Id;
            _enhancementSerial = serial;
            _enhancementPriceDisplay = metadata.PriceDisplay;
        }
        catch { }
        finally { _enhancementRefreshInProgress = false; }
    }

    private void EnsureSerialBadge(string serial)
    {
        var oldText = TicketOverlayCanvas.Children.OfType<TextBlock>()
            .FirstOrDefault(t => t.FontFamily.Source.Contains("Consolas", StringComparison.OrdinalIgnoreCase));
        if (oldText is not null) TicketOverlayCanvas.Children.Remove(oldText);

        var existing = TicketOverlayCanvas.Children.OfType<Border>()
            .FirstOrDefault(b => Equals(b.Tag, "ProgramSerialBadge"));
        if (existing?.Child is TextBlock existingText)
        {
            existingText.Text = serial;
            return;
        }

        var badge = new Border
        {
            Tag = "ProgramSerialBadge",
            MinWidth = 185,
            Height = 30,
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromRgb(255, 246, 224)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(145, 62, 42)),
            BorderThickness = new Thickness(1.5),
            Padding = new Thickness(12, 2, 12, 2),
            IsHitTestVisible = false,
            Child = new TextBlock
            {
                Text = serial,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(78, 34, 26)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            }
        };
        Canvas.SetLeft(badge, 54);
        Canvas.SetTop(badge, 615);
        TicketOverlayCanvas.Children.Add(badge);
    }

    private bool HasProgramPriceBadge() => TicketOverlayCanvas.Children.OfType<FrameworkElement>()
        .Any(element => Equals(element.Tag, "ProgramPriceBadge"));

    private void RemoveProgramPriceBadge()
    {
        foreach (var element in TicketOverlayCanvas.Children.OfType<FrameworkElement>()
                     .Where(element => Equals(element.Tag, "ProgramPriceBadge")).ToList())
            TicketOverlayCanvas.Children.Remove(element);
    }

    private void AddProgramPriceBadge(long price)
    {
        RemoveProgramPriceBadge();
        var outer = new Border
        {
            Tag = "ProgramPriceBadge", Width = 176, Height = 80, CornerRadius = new CornerRadius(16),
            Background = new SolidColorBrush(Color.FromRgb(181, 119, 25)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(255, 225, 124)), BorderThickness = new Thickness(3), IsHitTestVisible = false
        };
        outer.Child = new Border
        {
            Margin = new Thickness(5), CornerRadius = new CornerRadius(12), Background = new SolidColorBrush(Color.FromRgb(255, 246, 207)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(150, 77, 18)), BorderThickness = new Thickness(2),
            Child = new TextBlock
            {
                Text = $"NT${price:N0}", FontFamily = new FontFamily("Microsoft JhengHei UI"), FontSize = 35,
                FontWeight = FontWeights.Black, Foreground = new SolidColorBrush(Color.FromRgb(196, 25, 28)),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center
            }
        };
        Canvas.SetLeft(outer, 882);
        Canvas.SetTop(outer, 42);
        TicketOverlayCanvas.Children.Add(outer);
    }

    private async void ResultOverlay_OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (ResultOverlay.Visibility != Visibility.Visible)
        {
            CelebrationLayer.Children.Clear();
            ResultOverlay.BeginAnimation(OpacityProperty, null);
            ResultOverlay.Opacity = 1;
            return;
        }

        await Task.Yield();
        CoinCursorVisual.Visibility = Visibility.Collapsed;
        SetCoinScratchState(false);
        TicketOverlayCanvas.Cursor = Cursors.Arrow;

        if (!TryReadPrizeAmount(ResultAmountText.Text, out var prize) || prize <= 0) return;

        var prizeRank = await GetPrizeRankAsync(prize);
        if (prizeRank == 1) ResultHeadline.Text = "✦ 恭喜中頭獎！ ✦";
        else if (prizeRank == 2) ResultHeadline.Text = "✦ 恭喜中二獎！ ✦";

        StartCelebrationEffect(prizeRank);
        StartResultOverlayEntrance(prizeRank);
        PlaySettlementSound(prize);
    }

    private async Task<int> GetPrizeRankAsync(long amount)
    {
        if (_currentDefinition is null || amount <= 0) return 0;
        try
        {
            await using var connection = await _database.OpenConnectionAsync();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT amount FROM prize_tiers WHERE ticket_id=$ticketId AND amount>0 GROUP BY amount ORDER BY amount DESC;";
            command.Parameters.AddWithValue("$ticketId", _currentDefinition.Id);
            await using var reader = await command.ExecuteReaderAsync();
            var rank = 0;
            while (await reader.ReadAsync())
            {
                rank++;
                if (reader.GetInt64(0) == amount) return rank;
            }
        }
        catch { }
        return 0;
    }

    private void PlaySettlementSound(long prize)
    {
        try
        {
            var level = prize >= 50_000 ? "big" : "small";
            var mode = _settlementOrigin == SettlementOrigin.ManualScratch ? "manual" : "auto";
            var path = Path.Combine(AppContext.BaseDirectory, "Audio", $"{level}-win-{mode}.wav");
            if (!File.Exists(path)) path = Path.Combine(AppContext.BaseDirectory, "Audio", $"{level}-win.wav");
            if (!File.Exists(path)) return;

            _enhancementWinSoundPlayer.Stop();
            _enhancementWinSoundPlayer.Open(new Uri(path, UriKind.Absolute));
            _enhancementWinSoundPlayer.Volume = 1.0;
            _enhancementWinSoundPlayer.Play();
        }
        catch { }
    }

    private void StartResultOverlayEntrance(int prizeRank)
    {
        ResultOverlay.BeginAnimation(OpacityProperty, null);
        ResultOverlay.Opacity = 0;
        var delay = prizeRank == 1 ? 0.48 : prizeRank == 2 ? 0.18 : 0.0;
        ResultOverlay.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(280))
        {
            BeginTime = TimeSpan.FromSeconds(delay), FillBehavior = FillBehavior.HoldEnd
        });
    }

    private void StartCelebrationEffect(int prizeRank)
    {
        CelebrationLayer.Children.Clear();
        if (prizeRank is not (1 or 2)) return;

        var jackpot = prizeRank == 1;
        var brush = new RadialGradientBrush();
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(jackpot ? (byte)170 : (byte)105, 255, 221, 99), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 196, 42), 1));
        var flash = new Rectangle { Width = 1080, Height = 657, Fill = brush, Opacity = 0, IsHitTestVisible = false };
        CelebrationLayer.Children.Add(flash);
        var flashAnim = new DoubleAnimationUsingKeyFrames();
        flashAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        flashAnim.KeyFrames.Add(new LinearDoubleKeyFrame(jackpot ? 0.55 : 0.28, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150))));
        flashAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(jackpot ? 850 : 520))));
        flash.BeginAnimation(OpacityProperty, flashAnim);

        AddBurstRing(540, 328, jackpot ? 1.0 : 0.65, 0);
        if (jackpot) AddBurstRing(540, 328, 0.75, 180);

        var random = new Random(unchecked(Environment.TickCount * 397));
        var count = jackpot ? 34 : 14;
        for (var i = 0; i < count; i++)
        {
            var particle = new TextBlock
            {
                Text = jackpot && i % 4 == 0 ? "●" : "✦",
                FontFamily = new FontFamily("Segoe UI Symbol"),
                FontSize = random.Next(jackpot ? 22 : 18, jackpot ? 42 : 31),
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(i % 3 == 0 ? Color.FromRgb(255, 238, 164) : Color.FromRgb(244, 181, 47)),
                Opacity = 0.96, IsHitTestVisible = false, RenderTransformOrigin = new Point(0.5, 0.5)
            };
            var translate = new TranslateTransform();
            var rotate = new RotateTransform(random.Next(-45, 46));
            var transforms = new TransformGroup();
            transforms.Children.Add(rotate); transforms.Children.Add(translate);
            particle.RenderTransform = transforms;
            Canvas.SetLeft(particle, 528); Canvas.SetTop(particle, 316);
            CelebrationLayer.Children.Add(particle);

            var angle = Math.PI * 2 * i / count + (random.NextDouble() - 0.5) * 0.35;
            var distance = random.Next(jackpot ? 250 : 180, jackpot ? 500 : 330);
            var x = Math.Cos(angle) * distance;
            var y = Math.Sin(angle) * distance + random.Next(20, jackpot ? 160 : 100);
            var duration = random.Next(jackpot ? 1050 : 750, jackpot ? 1950 : 1250);
            var begin = random.Next(0, jackpot ? 220 : 110);
            translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, x, TimeSpan.FromMilliseconds(duration)) { BeginTime = TimeSpan.FromMilliseconds(begin), DecelerationRatio = 0.35 });
            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, y, TimeSpan.FromMilliseconds(duration)) { BeginTime = TimeSpan.FromMilliseconds(begin), AccelerationRatio = 0.18 });
            rotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(rotate.Angle, rotate.Angle + random.Next(-260, 261), TimeSpan.FromMilliseconds(duration)) { BeginTime = TimeSpan.FromMilliseconds(begin) });
            particle.BeginAnimation(OpacityProperty, new DoubleAnimation(0.96, 0, TimeSpan.FromMilliseconds(duration * 0.72)) { BeginTime = TimeSpan.FromMilliseconds(begin + duration * 0.28) });
        }
    }

    private void AddBurstRing(double centerX, double centerY, double opacity, int delayMs)
    {
        var ring = new Ellipse
        {
            Width = 150, Height = 150, Stroke = new SolidColorBrush(Color.FromRgb(255, 220, 102)), StrokeThickness = 7,
            Opacity = opacity, IsHitTestVisible = false, RenderTransformOrigin = new Point(0.5, 0.5)
        };
        var scale = new ScaleTransform(0.25, 0.25);
        ring.RenderTransform = scale;
        Canvas.SetLeft(ring, centerX - 75); Canvas.SetTop(ring, centerY - 75);
        CelebrationLayer.Children.Add(ring);
        var duration = TimeSpan.FromMilliseconds(760);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.25, 4.6, duration) { BeginTime = TimeSpan.FromMilliseconds(delayMs), DecelerationRatio = 0.45 });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.25, 4.6, duration) { BeginTime = TimeSpan.FromMilliseconds(delayMs), DecelerationRatio = 0.45 });
        ring.BeginAnimation(OpacityProperty, new DoubleAnimation(opacity, 0, duration) { BeginTime = TimeSpan.FromMilliseconds(delayMs + 100) });
    }

    private static bool TryReadPrizeAmount(string text, out long amount)
    {
        var normalized = text.Replace("$", string.Empty).Trim();
        return long.TryParse(normalized, NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out amount)
            || long.TryParse(normalized, NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out amount);
    }
}
