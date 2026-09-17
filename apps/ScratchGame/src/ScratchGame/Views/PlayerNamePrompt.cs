using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ScratchGame.Views;

internal static class PlayerNamePrompt
{
    public static string? Show(Window owner)
    {
        string? result = null;

        var window = new Window
        {
            Owner = owner,
            Title = "新增玩家",
            Width = 500,
            Height = 250,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            ShowInTaskbar = false,
            Background = Brushes.Transparent,
            ResizeMode = ResizeMode.NoResize,
            UseLayoutRounding = true,
            SnapsToDevicePixels = true
        };

        var outer = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(53, 22, 16)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(184, 123, 64)),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(28, 24, 28, 24)
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(46) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(74) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        outer.Child = grid;

        var title = new TextBlock
        {
            Text = "新增玩家",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            FontFamily = new FontFamily("Microsoft JhengHei UI"),
            FontSize = 25,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(255, 230, 166))
        };
        grid.Children.Add(title);

        var inputStack = new StackPanel();
        var label = new TextBlock
        {
            Text = "玩家名稱",
            Margin = new Thickness(0, 0, 0, 7),
            FontFamily = new FontFamily("Microsoft JhengHei UI"),
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromRgb(211, 179, 154))
        };
        var input = new TextBox
        {
            Height = 38,
            Padding = new Thickness(11, 0, 11, 0),
            FontFamily = new FontFamily("Microsoft JhengHei UI"),
            FontSize = 15,
            Foreground = new SolidColorBrush(Color.FromRgb(255, 240, 208)),
            CaretBrush = new SolidColorBrush(Color.FromRgb(255, 208, 90)),
            Background = new SolidColorBrush(Color.FromRgb(37, 17, 14)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(117, 80, 62)),
            BorderThickness = new Thickness(1),
            VerticalContentAlignment = VerticalAlignment.Center,
            MaxLength = 40
        };
        inputStack.Children.Add(label);
        inputStack.Children.Add(input);
        Grid.SetRow(inputStack, 1);
        grid.Children.Add(inputStack);

        var errorText = new TextBlock
        {
            Text = string.Empty,
            VerticalAlignment = VerticalAlignment.Top,
            FontFamily = new FontFamily("Microsoft JhengHei UI"),
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(245, 177, 111))
        };

        var actionGrid = new Grid { VerticalAlignment = VerticalAlignment.Bottom };
        actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(108) });
        actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(108) });

        var cancel = new Button { Content = "取消", Margin = new Thickness(0) };
        cancel.SetResourceReference(FrameworkElement.StyleProperty, "DialogSecondaryButton");
        Grid.SetColumn(cancel, 1);

        var confirm = new Button { Content = "確認", Margin = new Thickness(0), IsDefault = true };
        confirm.SetResourceReference(FrameworkElement.StyleProperty, "DialogButton");
        Grid.SetColumn(confirm, 3);

        void Confirm()
        {
            var name = input.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                errorText.Text = "請輸入玩家名稱。";
                input.Focus();
                return;
            }

            result = name;
            window.DialogResult = true;
        }

        cancel.Click += (_, _) => window.DialogResult = false;
        confirm.Click += (_, _) => Confirm();
        input.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Confirm();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                window.DialogResult = false;
                e.Handled = true;
            }
        };

        var bottom = new Grid();
        bottom.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
        bottom.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) });
        bottom.Children.Add(errorText);
        Grid.SetRow(actionGrid, 1);
        actionGrid.Children.Add(cancel);
        actionGrid.Children.Add(confirm);
        bottom.Children.Add(actionGrid);
        Grid.SetRow(bottom, 2);
        grid.Children.Add(bottom);

        window.Content = outer;
        window.Loaded += (_, _) =>
        {
            input.Focus();
            input.SelectAll();
        };

        return window.ShowDialog() == true ? result : null;
    }
}
