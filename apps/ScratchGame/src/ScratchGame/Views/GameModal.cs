using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ScratchGame.Views;

public enum GameModalKind
{
    Info,
    Warning,
    Error,
    Confirm
}

public static class GameModal
{
    public static void Info(Window owner, string title, string message)
        => Show(owner, title, message, GameModalKind.Info, "知道了");

    public static void Warning(Window owner, string title, string message)
        => Show(owner, title, message, GameModalKind.Warning, "知道了");

    public static void Error(Window owner, string title, string message)
        => Show(owner, title, message, GameModalKind.Error, "知道了");

    public static bool Confirm(
        Window owner,
        string title,
        string message,
        string confirmText = "確定",
        string cancelText = "取消")
        => Show(owner, title, message, GameModalKind.Confirm, confirmText, cancelText);

    private static bool Show(
        Window owner,
        string title,
        string message,
        GameModalKind kind,
        string primaryText,
        string? secondaryText = null)
    {
        try
        {
            var result = false;
            var window = new Window
            {
                Owner = owner,
                Width = 520,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                ShowInTaskbar = false,
                Background = Brushes.Transparent,
                ResizeMode = ResizeMode.NoResize,
                UseLayoutRounding = true,
                SnapsToDevicePixels = true
            };

            var accent = kind switch
            {
                GameModalKind.Error => Color.FromRgb(204, 72, 56),
                GameModalKind.Warning => Color.FromRgb(227, 157, 49),
                _ => Color.FromRgb(227, 171, 72)
            };

            var outer = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(48, 19, 15)),
                BorderBrush = new SolidColorBrush(accent),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(20),
                Padding = new Thickness(28, 24, 28, 24)
            };

            var stack = new StackPanel();
            outer.Child = stack;

            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontFamily = new FontFamily("Microsoft JhengHei UI"),
                FontSize = 25,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 228, 169)),
                TextWrapping = TextWrapping.Wrap
            });

            stack.Children.Add(new TextBlock
            {
                Text = message,
                Margin = new Thickness(0, 14, 0, 24),
                FontFamily = new FontFamily("Microsoft JhengHei UI"),
                FontSize = 16,
                LineHeight = 25,
                Foreground = new SolidColorBrush(Color.FromRgb(236, 211, 186)),
                TextWrapping = TextWrapping.Wrap
            });

            var buttons = new Grid();
            buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            if (secondaryText is not null)
            {
                buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
                buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            Button MakeButton(string text, bool primary)
            {
                return new Button
                {
                    Content = text,
                    MinHeight = 46,
                    FontSize = 17,
                    FontWeight = FontWeights.SemiBold,
                    FontFamily = new FontFamily("Microsoft JhengHei UI"),
                    Foreground = Brushes.White,
                    Background = new SolidColorBrush(primary ? Color.FromRgb(176, 29, 36) : Color.FromRgb(77, 54, 47)),
                    BorderBrush = new SolidColorBrush(primary ? Color.FromRgb(232, 166, 69) : Color.FromRgb(123, 91, 78)),
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand,
                    Padding = new Thickness(18, 9, 18, 9),
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Template = BuildButtonTemplate()
                };
            }

            if (secondaryText is not null)
            {
                var secondary = MakeButton(secondaryText, false);
                secondary.Click += (_, _) => { result = false; window.DialogResult = false; };
                Grid.SetColumn(secondary, 0);
                buttons.Children.Add(secondary);

                var primary = MakeButton(primaryText, true);
                primary.Click += (_, _) => { result = true; window.DialogResult = true; };
                Grid.SetColumn(primary, 2);
                buttons.Children.Add(primary);
                window.KeyDown += (_, e) =>
                {
                    if (e.Key == Key.Escape) { result = false; window.DialogResult = false; }
                };
            }
            else
            {
                var primary = MakeButton(primaryText, true);
                primary.Click += (_, _) => { result = true; window.DialogResult = true; };
                buttons.Children.Add(primary);
                window.KeyDown += (_, e) =>
                {
                    if (e.Key is Key.Escape or Key.Enter) { result = true; window.DialogResult = true; }
                };
            }

            stack.Children.Add(buttons);
            window.Content = outer;
            window.ShowDialog();
            return result;
        }
        catch
        {
            var buttons = kind == GameModalKind.Confirm ? MessageBoxButton.YesNo : MessageBoxButton.OK;
            var image = kind switch
            {
                GameModalKind.Error => MessageBoxImage.Error,
                GameModalKind.Warning => MessageBoxImage.Warning,
                GameModalKind.Confirm => MessageBoxImage.Question,
                _ => MessageBoxImage.Information
            };
            return MessageBox.Show(owner, message, title, buttons, image) == MessageBoxResult.Yes;
        }
    }

    private static ControlTemplate BuildButtonTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(14));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);
        template.VisualTree = border;
        return template;
    }
}
