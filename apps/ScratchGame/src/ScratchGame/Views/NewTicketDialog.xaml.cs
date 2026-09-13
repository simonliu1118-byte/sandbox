using System.Windows;
using System.Windows.Controls;
using ScratchGame.Models;
using ScratchGame.Services;

namespace ScratchGame.Views;

public partial class NewTicketDialog : Window
{
    private readonly IReadOnlyList<TicketDefinition> _tickets;

    public TicketDefinition? SelectedTicket { get; private set; }

    public NewTicketDialog(IReadOnlyList<TicketDefinition> tickets)
    {
        InitializeComponent();
        UiAssetLoader.TrySetImage(DialogBackgroundImage, UiAssetLoader.UiPath("dialog_bg.png"));
        _tickets = tickets;

        PriceComboBox.ItemsSource = _tickets
            .Select(t => t.Price)
            .Distinct()
            .OrderBy(price => price)
            .Select(price => $"${price:N0}")
            .ToList();

        if (PriceComboBox.Items.Count > 0)
            PriceComboBox.SelectedIndex = 0;
    }

    private void PriceComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PriceComboBox.SelectedIndex < 0)
            return;

        var price = _tickets
            .Select(t => t.Price)
            .Distinct()
            .OrderBy(value => value)
            .ElementAt(PriceComboBox.SelectedIndex);

        var filtered = _tickets
            .Where(ticket => ticket.Price == price)
            .OrderBy(ticket => ticket.DisplayName)
            .Select(ticket => new TicketChoice(ticket, ResolveThumbnail(ticket)))
            .ToList();
        TicketListBox.ItemsSource = filtered;
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
            MessageBox.Show(this, "請先選擇一張彩券。", "新的一張", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SelectedTicket = choice.Ticket;
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private sealed record TicketChoice(TicketDefinition Ticket, string? ThumbnailPath);
}
