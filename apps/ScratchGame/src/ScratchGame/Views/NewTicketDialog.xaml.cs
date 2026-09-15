using System.Windows;
using System.Windows.Controls;
using ScratchGame.Models;
using ScratchGame.Services;

namespace ScratchGame.Views;

public partial class NewTicketDialog : Window
{
    private static readonly long[] SupportedPriceFilters = [100, 200, 300, 500, 1000, 2000, 5000];
    private readonly IReadOnlyList<TicketDefinition> _tickets;
    private readonly TicketThumbnailCacheService _thumbnailCache;

    public TicketDefinition? SelectedTicket { get; private set; }

    public NewTicketDialog(
        IReadOnlyList<TicketDefinition> tickets,
        ScratchPackRuntimeService scratchPackRuntime)
        : this(tickets, new TicketThumbnailCacheService(scratchPackRuntime))
    {
    }

    public NewTicketDialog(
        IReadOnlyList<TicketDefinition> tickets,
        TicketThumbnailCacheService thumbnailCache)
    {
        InitializeComponent();
        _tickets = tickets;
        _thumbnailCache = thumbnailCache;

        var allPalette = GetPricePalette(null);
        var options = new List<PriceFilterOption>
        {
            new(null, "全部", _tickets.Count > 0,
                allPalette.Background, allPalette.Border, allPalette.Foreground, allPalette.Accent)
        };
        options.AddRange(SupportedPriceFilters.Select(price =>
        {
            var palette = GetPricePalette(price);
            return new PriceFilterOption(
                price,
                $"${price:N0}",
                _tickets.Any(ticket => ticket.Price == price),
                palette.Background,
                palette.Border,
                palette.Foreground,
                palette.Accent);
        }));

        PriceFilterListBox.ItemsSource = options;
        PriceFilterListBox.SelectedIndex = 0;
    }

    private void PriceFilterListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PriceFilterListBox.SelectedItem is not PriceFilterOption option)
            return;

        var filtered = _tickets
            .Where(ticket => option.Price is null || ticket.Price == option.Price.Value)
            .OrderBy(ticket => ticket.Price)
            .ThenBy(ticket => ticket.DisplayName)
            .Select(ticket => new TicketChoice(
                ticket,
                ResolveThumbnail(ticket),
                ticket.ActiveBatchNumber > 0 ? $"第 {ticket.ActiveBatchNumber} 批" : "批次未定"))
            .ToList();

        TicketListBox.ItemsSource = filtered;
        EmptyTicketText.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        TicketListBox.Visibility = filtered.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        if (filtered.Count > 0)
            TicketListBox.SelectedIndex = 0;
    }

    private string? ResolveThumbnail(TicketDefinition ticket)
    {
        if (!string.IsNullOrWhiteSpace(ticket.SourcePackageId))
        {
            try
            {
                return _thumbnailCache.GetOrCreate(ticket);
            }
            catch (Exception ex)
            {
                RuntimeAssetLog.Error(ticket.SourcePackageId, "ScratchPack ticket thumbnail cache", ex);
                return null;
            }
        }

        var folder = ticket.RuleId is "ThreeLine" or "1" ? "ThreeStar" : ticket.Id;
        var artwork = ticket.Price == 100 ? "ticket-100.png" : "ticket.png";
        var legacyPath = UiAssetLoader.TicketPath(folder, artwork);
        if (File.Exists(legacyPath))
            return legacyPath;

        RuntimeAssetLog.Missing(legacyPath, "legacy ticket thumbnail source");
        return null;
    }

    private void Start_OnClick(object sender, RoutedEventArgs e)
    {
        if (TicketListBox.SelectedItem is not TicketChoice choice)
        {
            GameModal.Info(this, "挑選彩券", "請先選擇一張彩券。");
            return;
        }

        SelectedTicket = choice.Ticket;
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
        => DialogResult = false;

    private static (string Background, string Border, string Foreground, string Accent) GetPricePalette(long? price)
        => price switch
        {
            100 => ("#5B2422", "#B96755", "#FFE5D6", "#E87B63"),
            200 => ("#244A35", "#5A9B70", "#E1F5E7", "#73BF86"),
            300 => ("#194951", "#4F9EA7", "#DDF7F8", "#5FC0C9"),
            500 => ("#60401C", "#C18A39", "#FFF0C9", "#E1A43F"),
            1000 => ("#243D62", "#5C83BC", "#E2ECFF", "#739CE0"),
            2000 => ("#432D60", "#8866B3", "#F0E4FF", "#A67BD8"),
            5000 => ("#5B2948", "#AE5F89", "#FFE2F1", "#D276A8"),
            _ => ("#4A3020", "#98612F", "#FBE5B8", "#D9A44F")
        };

    private sealed record PriceFilterOption(
        long? Price,
        string Label,
        bool HasTickets,
        string Background,
        string BorderBrush,
        string Foreground,
        string Accent);

    private sealed record TicketChoice(TicketDefinition Ticket, string? ThumbnailPath, string BatchText);
}
