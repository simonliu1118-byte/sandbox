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

            var scratch = new ScratchSurface
            {
                Width = zone.Width,
                Height = zone.Height,
                BrushRadius = Math.Clamp(Math.Min(zone.Width, zone.Height) * 0.18, 16, 32),
                CompletionThreshold = 0.78,
                MaskImagePath = resolved.FoilImagePath,
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
