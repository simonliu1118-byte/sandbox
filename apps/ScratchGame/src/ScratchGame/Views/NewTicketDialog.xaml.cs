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
        UiAssetLoader.TrySetImage(DialogBackgroundImage, UiAssetLoader.UiPath("dialog_bg.png"));
        _tickets = tickets;

        var options = new List<PriceFilterOption>
        {
            new(null, "全部", _tickets.Count > 0)
        };
        options.AddRange(SupportedPriceFilters.Select(price =>
            new PriceFilterOption(price, $"${price:N0}", _tickets.Any(ticket => ticket.Price == price))));

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

    private static string? ResolveThumbnail(TicketDefinition ticket)
    {
        var folder = ticket.RuleId is "ThreeLine" or "1" ? "ThreeStar" : ticket.Id;
        var path = UiAssetLoader.TicketPath(folder, "thumbnail.png");
        return File.Exists(path) ? path : null;
    }

    private void Start_OnClick(object sender, RoutedEventArgs e)
    {
        if (TicketListBox.SelectedItem is not TicketChoice choice)
        {
            MessageBox.Show(this, "請先選擇一張彩券。", "挑選彩券", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SelectedTicket = choice.Ticket;
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private sealed record PriceFilterOption(long? Price, string Label, bool HasTickets);
    private sealed record TicketChoice(TicketDefinition Ticket, string? ThumbnailPath);
}
