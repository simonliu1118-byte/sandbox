using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ScratchGame.Data;
using ScratchGame.Services;

namespace ScratchGame.Views;

public sealed class HiddenPacksDialog : Window
{
    private readonly TicketAdminService _admin;
    private readonly StackPanel _itemsPanel;
    private readonly TextBlock _emptyText;

    public HiddenPacksDialog(AppDatabase database)
    {
        _admin = new TicketAdminService(database);

        Title = "已隱藏 Pack";
        Width = 720;
        Height = 520;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        ShowInTaskbar = false;
        Background = Brushes.Transparent;

        var root = new Border
        {
            CornerRadius = new CornerRadius(22),
            BorderBrush = new SolidColorBrush(Color.FromRgb(184, 123, 64)),
            BorderThickness = new Thickness(1.5),
            Background = new SolidColorBrush(Color.FromRgb(53, 22, 16)),
            Padding = new Thickness(28, 22, 28, 20)
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new StackPanel();
        heading.Children.Add(new TextBlock
        {
            Text = "已隱藏 Pack",
            FontFamily = new FontFamily("Microsoft JhengHei UI"),
            FontSize = 28,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(255, 230, 166))
        });
        heading.Children.Add(new TextBlock
        {
            Text = "隱藏的 Pack 不會出現在挑券與設定主列表；可在這裡解除隱藏。",
            Margin = new Thickness(1, 5, 0, 16),
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(216, 179, 146))
        });
        Grid.SetRow(heading, 0);
        grid.Children.Add(heading);

        var contentBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(37, 16, 12)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(101, 64, 52)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(10)
        };
        _itemsPanel = new StackPanel();
        _emptyText = new TextBlock
        {
            Text = "目前沒有已隱藏的 Pack",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 120, 0, 0),
            FontSize = 16,
            Foreground = new SolidColorBrush(Color.FromRgb(205, 174, 142)),
            Visibility = Visibility.Collapsed
        };
        var contentGrid = new Grid();
        contentGrid.Children.Add(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _itemsPanel
        });
        contentGrid.Children.Add(_emptyText);
        contentBorder.Child = contentGrid;
        Grid.SetRow(contentBorder, 1);
        grid.Children.Add(contentBorder);

        var close = new Button
        {
            Content = "關閉",
            Width = 110,
            Margin = new Thickness(0, 16, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        if (TryFindResource("DialogSecondaryButton") is Style closeStyle)
            close.Style = closeStyle;
        close.Click += (_, _) => Close();
        Grid.SetRow(close, 2);
        grid.Children.Add(close);

        root.Child = grid;
        Content = root;
        Loaded += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        _itemsPanel.Children.Clear();
        IReadOnlyList<TicketAdminItem> items;
        try
        {
            items = await _admin.GetHiddenPacksAsync();
        }
        catch (Exception ex)
        {
            GameModal.Warning(this, "已隱藏 Pack", ex.Message);
            return;
        }

        _emptyText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var item in items)
        {
            var row = new Grid { MinHeight = 62, Margin = new Thickness(0, 0, 0, 8) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var infoBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(57, 32, 25)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(96, 64, 53)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(11),
                Padding = new Thickness(14, 9, 14, 9)
            };
            var info = new StackPanel();
            info.Children.Add(new TextBlock
            {
                Text = item.DisplayName,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(247, 228, 198))
            });
            info.Children.Add(new TextBlock
            {
                Text = $"${item.Price:N0}　{item.SourceText}",
                Margin = new Thickness(0, 4, 0, 0),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(199, 158, 121))
            });
            infoBorder.Child = info;
            row.Children.Add(infoBorder);

            var restore = new Button
            {
                Content = "解除隱藏",
                Width = 110,
                Margin = new Thickness(12, 8, 0, 8),
                Tag = item
            };
            if (TryFindResource("DialogButton") is Style buttonStyle)
                restore.Style = buttonStyle;
            restore.Click += Restore_OnClick;
            Grid.SetColumn(restore, 1);
            row.Children.Add(restore);

            _itemsPanel.Children.Add(row);
        }
    }

    private async void Restore_OnClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not TicketAdminItem item)
            return;

        try
        {
            await _admin.SetHiddenAsync(item.Id, hidden: false);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            GameModal.Warning(this, "解除隱藏失敗", ex.Message);
        }
    }
}
