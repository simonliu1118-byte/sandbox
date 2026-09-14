using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace ScratchGame;

public partial class MainWindow
{
    private bool _build6LayoutApplied;

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        if (_build6LayoutApplied)
            return;

        _build6LayoutApplied = true;
        ApplyBuild6MainLayout();
    }

    private void ApplyBuild6MainLayout()
    {
        if (Content is not Grid root ||
            TopBarBackgroundImage.Parent is not Grid headerGrid ||
            StageBackgroundImage.Parent is not Grid stageGrid ||
            FooterBackgroundImage.Parent is not Grid footerGrid)
            return;

        // Header / Stage / Footer are three independent art surfaces.  The separators
        // belong to the program, never to a theme PNG.
        root.RowDefinitions.Clear();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(96) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(5) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(5) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(104) });

        Grid.SetRow(headerGrid, 0);
        Grid.SetRow(stageGrid, 2);
        Grid.SetRow(footerGrid, 4);

        AddProgramSeparator(root, 1);
        AddProgramSeparator(root, 3);

        // Hide decorative lines baked too close to old asset edges. The actual boundary
        // is the WPF separator row above/below, so theme art can never collide with it.
        if (headerGrid.FindName("Build6HeaderSafetyMask") is null)
        {
            var mask = new Border
            {
                Name = "Build6HeaderSafetyMask",
                Height = 7,
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = new SolidColorBrush(Color.FromRgb(0x85, 0x03, 0x08)),
                IsHitTestVisible = false
            };
            headerGrid.Children.Add(mask);
        }

        if (footerGrid.FindName("Build6FooterSafetyMask") is null)
        {
            var mask = new Border
            {
                Name = "Build6FooterSafetyMask",
                Height = 4,
                VerticalAlignment = VerticalAlignment.Top,
                Background = new SolidColorBrush(Color.FromRgb(0x76, 0x00, 0x06)),
                IsHitTestVisible = false
            };
            footerGrid.Children.Add(mask);
        }

        RestyleEmptyStageCard();
    }

    private static void AddProgramSeparator(Grid root, int row)
    {
        var host = new Grid { IsHitTestVisible = false };
        host.Children.Add(new Border { Background = new SolidColorBrush(Color.FromRgb(0x5D, 0x26, 0x0B)) });
        host.Children.Add(new Border
        {
            Height = 3,
            VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Color.FromRgb(0xE3, 0xB2, 0x4A))
        });
        host.Children.Add(new Border
        {
            Height = 1,
            VerticalAlignment = VerticalAlignment.Top,
            Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xE1, 0x8A))
        });
        Grid.SetRow(host, row);
        root.Children.Add(host);
    }

    private void RestyleEmptyStageCard()
    {
        if (TicketPlaceholderPanel.Children.Count != 1 ||
            TicketPlaceholderPanel.Children[0] is not StackPanel stack)
            return;

        TicketPlaceholderPanel.Children.Remove(stack);
        stack.Width = 452;

        foreach (var text in stack.Children.OfType<TextBlock>())
            text.Foreground = new SolidColorBrush(Color.FromRgb(0x6B, 0x32, 0x22));
        if (stack.Children.OfType<TextBlock>().FirstOrDefault() is { } star)
            star.Foreground = new SolidColorBrush(Color.FromRgb(0xB8, 0x72, 0x16));
        TicketPlaceholderText.Foreground = new SolidColorBrush(Color.FromRgb(0x5B, 0x24, 0x18));

        var card = new Border
        {
            Width = 520,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            CornerRadius = new CornerRadius(26),
            Padding = new Thickness(34, 26, 34, 26),
            Background = new SolidColorBrush(Color.FromArgb(0xCD, 0xEF, 0xD8, 0xAE)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0xE5, 0xD5, 0x9A, 0x45)),
            BorderThickness = new Thickness(2),
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0x5B, 0x19, 0x0B),
                BlurRadius = 18,
                ShadowDepth = 5,
                Opacity = 0.32
            },
            Child = stack
        };
        TicketPlaceholderPanel.Children.Add(card);
    }
}
