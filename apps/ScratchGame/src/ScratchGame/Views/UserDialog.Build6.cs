using System.Windows;
using System.Windows.Controls;

namespace ScratchGame.Views;

public partial class UserDialog
{
    private bool _build6DialogLayoutApplied;

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        if (_build6DialogLayoutApplied)
            return;

        _build6DialogLayoutApplied = true;
        Width = 760;
        Height = 600;

        if (Content is Border outer && outer.Child is Grid root && root.RowDefinitions.Count >= 4)
        {
            root.Margin = new Thickness(30, 24, 30, 24);
            root.RowDefinitions[0].Height = new GridLength(72);
            root.RowDefinitions[2].Height = new GridLength(76);
            root.RowDefinitions[3].Height = new GridLength(62);
        }

        foreach (var button in FindVisualChildren<Button>(this))
        {
            if (button.Content is not string label)
                continue;

            switch (label)
            {
                case "新增使用者":
                    button.Width = 132;
                    button.Margin = new Thickness(0);
                    if (button.Parent is Grid addGrid && addGrid.ColumnDefinitions.Count >= 3)
                    {
                        addGrid.ColumnDefinitions[1].Width = new GridLength(16);
                        addGrid.ColumnDefinitions[2].Width = new GridLength(142);
                    }
                    break;
                case "重置損益":
                    button.Width = 118;
                    button.Margin = new Thickness(0);
                    break;
                case "取消":
                    button.Width = 108;
                    button.Margin = new Thickness(0, 0, 12, 0);
                    break;
                case "切換使用者":
                    button.Width = 132;
                    button.Margin = new Thickness(0);
                    break;
            }
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T match)
                yield return match;
            foreach (var descendant in FindVisualChildren<T>(child))
                yield return descendant;
        }
    }
}
