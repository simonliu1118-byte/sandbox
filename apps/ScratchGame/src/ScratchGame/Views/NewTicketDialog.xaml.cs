using System.Windows;
using System.Windows.Controls;
using ScratchGame.Models;
using ScratchGame.Services;

namespace ScratchGame.Views;

public partial class NewTicketDialog : Window
{
    private static readonly long[] SupportedPriceFilters = [100, 200, 300, 500, 1000, 2000, 5000];
    private readonly IReadOnlyList<TicketDefinition> _tickets;

    public TicketDefinition? SelectedTicket { get; private set; }

    public NewTicketDialog(IReadOnlyList<TicketDefinition> tickets)
    {
        InitializeComponent();
        _tickets = tickets;

        var options = new List<PriceFilterOption>
        {
            new(null, "全部", ResolvePriceIcon(null), _tickets.Count > 0)
        };
        options.AddRange(SupportedPriceFilters.Select(price =>
            new PriceFilterOption(price, $"${price:N0}", ResolvePriceIcon(price), _tickets.Any(ticket => ticket.Price == price))));

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
            .Select(ticket => new TicketChoice(ticket, ResolveThumbnail(ticket)))
            .ToList();

        TicketListBox.ItemsSource = filtered;
        EmptyTicketText.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        TicketListBox.Visibility = filtered.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        if (filtered.Count > 0)
            TicketListBox.SelectedIndex = 0;
    }

    private static string ResolvePriceIcon(long? price)
    {
        var file = price switch
        {
            null => "note_all.png",
            100 => "note_100.png",
            200 => "note_200.png",
            300 => "note_300.png",
            500 => "note_500.png",
            1000 => "note_1000.png",
            2000 => "note_2000.png",
            5000 => "note_5000.png",
            _ => "note_all.png"
        };
        return Path.Combine(AppContext.BaseDirectory, "UI", "Denominations", file);
    }

    private static string? ResolveThumbnail(TicketDefinition ticket)
    {
        var folder = ticket.RuleId is "ThreeLine" or "1" ? "ThreeStar" : ticket.Id;
        var file = ticket.Price == 100 ? "thumbnail-100.png" : "thumbnail.png";
        var path = UiAssetLoader.TicketPath(folder, file);
        return File.Exists(path) ? path : null;
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

    private sealed record PriceFilterOption(long? Price, string Label, string IconPath, bool HasTickets);
    private sealed record TicketChoice(TicketDefinition Ticket, string? ThumbnailPath);
}
