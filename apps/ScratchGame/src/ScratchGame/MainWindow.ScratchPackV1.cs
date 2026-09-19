using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScratchGame.Controls;
using ScratchGame.Engine;
using ScratchGame.Models;
using ScratchGame.Services;

namespace ScratchGame;

public partial class MainWindow
{
    private ResolvedScratchPackTicket? _activeScratchPack;

    private string CreatePayloadForTicket(TicketDefinition definition, long prizeAmount)
    {
        if (string.IsNullOrWhiteSpace(definition.SourcePackageId))
            return GamePayloadFactory.Create(definition.RuleId, prizeAmount, definition.Price);

        var resolved = new ScratchPackRuntimeService(_database).Load(definition);
        return GamePayloadFactory.Create(resolved.Definition, prizeAmount);
    }

    private bool TryRenderScratchPackTicket(PendingTicket pending, TicketDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.SourcePackageId))
        {
            _activeScratchPack = null;
            return false;
        }

        var resolved = new ScratchPackRuntimeService(_database).Load(definition);
        _activeScratchPack = resolved;
        LoadTicketArtworkPath(resolved.TicketImagePath);

        using var payload = JsonDocument.Parse(pending.PayloadJson);
        switch (resolved.Definition.GameType)
        {
            case "1":
                RenderScratchPackGameType1(payload.RootElement, resolved);
                return true;
            case "2":
                RenderScratchPackGameType2(payload.RootElement, resolved);
                return true;
            case "3":
                RenderScratchPackGameType3(payload.RootElement, resolved);
                return true;
            default:
                TicketBackgroundImage.Visibility = Visibility.Collapsed;
                TicketPlaceholderPanel.Visibility = Visibility.Visible;
                TicketPlaceholderText.Text = $"尚未支援 GameType {resolved.Definition.GameType}";
                return true;
        }
    }

    private void RenderScratchPackGameType1(
        JsonElement payload,
        ResolvedScratchPackTicket resolved)
    {
        var cells = payload.GetProperty("cells").EnumerateArray()
            .Select(element => element.GetString() ?? string.Empty)
            .ToArray();
        var zones = resolved.Definition.Zones;
        if (cells.Length != zones.Count)
            throw new InvalidDataException("GameType 1 payload cell 數量與 scratch.zones 不一致。");

        for (var i = 0; i < zones.Count; i++)
        {
            var zone = zones[i];
            var cell = cells[i];
            var symbol = new TextBlock
            {
                Width = zone.Width,
                Height = zone.Height,
                Text = GetDisplaySymbol(cell, i),
                TextAlignment = TextAlignment.Center,
                FontFamily = new FontFamily("Microsoft JhengHei UI"),
                FontSize = cell == "★"
                    ? Math.Max(28, zone.Height * 0.42)
                    : Math.Max(24, zone.Height * 0.34),
                FontWeight = FontWeights.Bold,
                Foreground = cell == "★"
                    ? new SolidColorBrush(Color.FromRgb(175, 28, 34))
                    : new SolidColorBrush(Color.FromRgb(98, 58, 27)),
                Padding = new Thickness(0, Math.Max(8, zone.Height * 0.20), 0, 0),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(symbol, zone.X);
            Canvas.SetTop(symbol, zone.Y);
            TicketOverlayCanvas.Children.Add(symbol);

            AddScratchSurface(zone, resolved.FoilImagePath);
        }
    }

    private void RenderScratchPackGameType2(
        JsonElement payload,
        ResolvedScratchPackTicket resolved)
    {
        var cells = GameType2RenderModel.Build(resolved.Definition, payload);
        foreach (var cell in cells)
        {
            var visual = CreateGameType2CellVisual(cell);
            Canvas.SetLeft(visual, cell.Zone.X);
            Canvas.SetTop(visual, cell.Zone.Y);
            TicketOverlayCanvas.Children.Add(visual);

            AddScratchSurface(cell.Zone, resolved.FoilImagePath);
        }
    }

    private void RenderScratchPackGameType3(
        JsonElement payload,
        ResolvedScratchPackTicket resolved)
    {
        var cells = GameType3RenderModel.Build(resolved.Definition, payload);
        if (cells.Count == 0)
            throw new InvalidDataException("GameType 3 Renderer 沒有可顯示的刮區。");

        var firstZone = cells[0].Zone;
        var amountFontSize = Math.Clamp(
            Math.Min(firstZone.Height * 0.34, firstZone.Width * 0.18),
            16,
            38);
        var amountBrush = new SolidColorBrush(Color.FromRgb(105, 40, 31));

        foreach (var cell in cells)
        {
            var amount = new TextBlock
            {
                Width = cell.Zone.Width,
                Height = cell.Zone.Height,
                Text = $"${cell.Amount:N0}",
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Microsoft JhengHei UI"),
                FontSize = amountFontSize,
                FontWeight = FontWeights.Bold,
                Foreground = amountBrush,
                Padding = new Thickness(4, Math.Max(4, cell.Zone.Height * 0.23), 4, 0),
                TextWrapping = TextWrapping.NoWrap,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(amount, cell.Zone.X);
            Canvas.SetTop(amount, cell.Zone.Y);
            TicketOverlayCanvas.Children.Add(amount);

            AddScratchSurface(cell.Zone, resolved.FoilImagePath);
        }
    }

    private static FrameworkElement CreateGameType2CellVisual(GameType2RenderCell cell)
    {
        var numberFontSize = Math.Max(24, cell.Zone.Height * 0.34);
        var amountFontSize = Math.Max(14, cell.Zone.Height * 0.16);
        var numberBrush = new SolidColorBrush(Color.FromRgb(66, 48, 35));
        var amountBrush = new SolidColorBrush(Color.FromRgb(170, 35, 38));

        if (cell.PrizeAmount is null)
        {
            return new TextBlock
            {
                Width = cell.Zone.Width,
                Height = cell.Zone.Height,
                Text = cell.Number.ToString(),
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Microsoft JhengHei UI"),
                FontSize = numberFontSize,
                FontWeight = FontWeights.Bold,
                Foreground = numberBrush,
                Padding = new Thickness(0, Math.Max(4, cell.Zone.Height * 0.24), 0, 0),
                IsHitTestVisible = false
            };
        }

        var grid = new Grid
        {
            Width = cell.Zone.Width,
            Height = cell.Zone.Height,
            IsHitTestVisible = false
        };
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0.62, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0.38, GridUnitType.Star) });

        var number = new TextBlock
        {
            Text = cell.Number.ToString(),
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            FontFamily = new FontFamily("Microsoft JhengHei UI"),
            FontSize = numberFontSize,
            FontWeight = FontWeights.Bold,
            Foreground = numberBrush,
            Margin = new Thickness(0, 0, 0, Math.Max(1, cell.Zone.Height * 0.015)),
            IsHitTestVisible = false
        };
        Grid.SetRow(number, 0);
        grid.Children.Add(number);

        var amount = new TextBlock
        {
            Text = $"${cell.PrizeAmount.Value:N0}",
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            FontFamily = new FontFamily("Microsoft JhengHei UI"),
            FontSize = amountFontSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = amountBrush,
            Margin = new Thickness(0, Math.Max(1, cell.Zone.Height * 0.015), 0, 0),
            IsHitTestVisible = false
        };
        Grid.SetRow(amount, 1);
        grid.Children.Add(amount);

        return grid;
    }

    private void AddScratchSurface(ScratchPackZone zone, string foilImagePath)
    {
        var scratch = new ScratchSurface
        {
            Width = zone.Width,
            Height = zone.Height,
            BrushRadius = Math.Clamp(Math.Min(zone.Width, zone.Height) * 0.18, 16, 32),
            CompletionThreshold = 0.78,
            MaskImagePath = foilImagePath,
            ZoneShape = zone.Shape,
            CornerRadius = zone.CornerRadius ?? 0
        };
        scratch.Completed += ScratchRegion_OnCompleted;
        Canvas.SetLeft(scratch, zone.X);
        Canvas.SetTop(scratch, zone.Y);
        TicketOverlayCanvas.Children.Add(scratch);
        _scratchRegions.Add(scratch);
        scratch.ResetMask();
    }

    private void LoadTicketArtworkPath(string path)
    {
        if (!File.Exists(path))
        {
            TicketBackgroundImage.Visibility = Visibility.Collapsed;
            TicketPlaceholderPanel.Visibility = Visibility.Visible;
            TicketPlaceholderText.Text = "找不到彩券美術資源";
            return;
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();

        TicketBackgroundImage.Source = bitmap;
        TicketBackgroundImage.Visibility = Visibility.Visible;
        TicketPlaceholderPanel.Visibility = Visibility.Collapsed;
    }

    private ScratchPackRect? CurrentSerialDisplayArea
        => _activeScratchPack?.Definition.SerialDisplayArea;

    private ScratchPackRect? CurrentPriceDisplayArea
        => _activeScratchPack?.Definition.PriceDisplayArea;
}
