using System.Windows;

namespace PackEditor;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void NewPack_OnClick(object sender, RoutedEventArgs e)
    {
        PackNameTextBox.Text = "未命名彩券";
        GameTypeComboBox.SelectedIndex = 0;
        PriceTextBox.Text = "500";
        PackageIdTextBox.Text = Guid.NewGuid().ToString("D");
        StatusText.Text = "已建立新的 ScratchPack 草稿；Package ID 已產生。";
        PackNameTextBox.Focus();
        PackNameTextBox.SelectAll();
    }
}
