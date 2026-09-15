using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ScratchGame.Services;

namespace PackEditor;

public partial class MainWindow : Window
{
    private PackDraft? _draft;
    private bool _loadingUi;

    public MainWindow()
    {
        InitializeComponent();
        ShowBasicPanel();
        RefreshEditorState();
    }

    private void NewPack_OnClick(object sender, RoutedEventArgs e)
    {
        _draft = PackDraft.CreateNew(GetEditorVersion());
        _loadingUi = true;
        try
        {
            PackNameTextBox.Text = _draft.Name;
            AuthorTextBox.Text = _draft.Author;
            GameTypeComboBox.SelectedIndex = 0;
            PriceTextBox.Text = _draft.Price.ToString();
            PackageIdTextBox.Text = _draft.PackageId.ToString("D");
            MinimumVersionTextBox.Text = FormatVersion(_draft.MinimumAppVersion);

            _draft.TicketArt.Source = "builtin";
            _draft.Foil.Source = "builtin";
            TicketBuiltInRadio.IsChecked = true;
            FoilBuiltInRadio.IsChecked = true;
            TicketPackagePathText.Text = "尚未選擇";
            TicketPackageInfoText.Text = string.Empty;
            FoilPackagePathText.Text = "尚未選擇";
            FoilPackageInfoText.Text = string.Empty;
        }
        finally
        {
            _loadingUi = false;
        }

        PopulateBuiltInResources();
        ApplyResourceSelectionsFromUi();
        ShowBasicPanel();
        RefreshPreview();
        RefreshEditorState();
        PackNameTextBox.Focus();
        PackNameTextBox.SelectAll();
    }

    private void BasicNav_OnClick(object sender, RoutedEventArgs e)
        => ShowBasicPanel();

    private void AssetsNav_OnClick(object sender, RoutedEventArgs e)
        => ShowAssetsPanel();

    private void ShowBasicPanel()
    {
        BasicPanel.Visibility = Visibility.Visible;
        AssetsPanel.Visibility = Visibility.Collapsed;
        BasicNavButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(79, 43, 31));
        AssetsNavButton.Background = System.Windows.Media.Brushes.Transparent;
    }

    private void ShowAssetsPanel()
    {
        BasicPanel.Visibility = Visibility.Collapsed;
        AssetsPanel.Visibility = Visibility.Visible;
        BasicNavButton.Background = System.Windows.Media.Brushes.Transparent;
        AssetsNavButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(79, 43, 31));
    }

    private void DraftField_OnChanged(object sender, TextChangedEventArgs e)
    {
        if (_loadingUi || _draft is null)
            return;

        SyncBasicFieldsToDraft();
        RefreshEditorState();
    }

    private void GameTypeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingUi || _draft is null)
            return;

        SyncBasicFieldsToDraft();
        PopulateBuiltInResources();
        ApplyResourceSelectionsFromUi();
        RefreshPreview();
        RefreshEditorState();
    }

    private void TicketSource_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_loadingUi)
            return;

        UpdateResourcePanels();
        ApplyResourceSelectionsFromUi();
        RefreshPreview();
        RefreshEditorState();
    }

    private void FoilSource_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_loadingUi)
            return;

        UpdateResourcePanels();
        ApplyResourceSelectionsFromUi();
        RefreshFoilPreview();
        RefreshEditorState();
    }

    private void TicketBuiltInComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingUi)
            return;

        ApplyResourceSelectionsFromUi();
        RefreshPreview();
        RefreshEditorState();
    }

    private void FoilBuiltInComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingUi)
            return;

        ApplyResourceSelectionsFromUi();
        RefreshFoilPreview();
        RefreshEditorState();
    }

    private void ChooseTicketPng_OnClick(object sender, RoutedEventArgs e)
    {
        if (_draft is null)
        {
            MessageBox.Show(this, "請先按「新增 Pack」。", "PackEditor", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var path = PickPng("選擇自訂票面 PNG");
        if (path is null)
            return;

        try
        {
            var (width, height) = EditorResourceCatalog.ReadPngDimensions(path);
            if (width != 1080 || height != 882)
            {
                MessageBox.Show(
                    this,
                    $"Canvas 1 票面必須精確為 1080 × 882。\n目前圖片：{width} × {height}\n\nPackEditor 不會自動縮放或裁切。",
                    "票面尺寸不符",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            _draft.TicketArt.ExternalPath = path;
            _draft.TicketArt.Source = "package";
            _draft.TicketArt.Reference = "assets/ticket.png";
            _loadingUi = true;
            try
            {
                TicketPackageRadio.IsChecked = true;
                TicketPackagePathText.Text = Path.GetFileName(path);
                TicketPackageInfoText.Text = $"{width} × {height}　→　assets/ticket.png";
            }
            finally
            {
                _loadingUi = false;
            }

            UpdateResourcePanels();
            RefreshPreview();
            RefreshEditorState();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "無法使用票面 PNG", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ChooseFoilPng_OnClick(object sender, RoutedEventArgs e)
    {
        if (_draft is null)
        {
            MessageBox.Show(this, "請先按「新增 Pack」。", "PackEditor", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var path = PickPng("選擇自訂銀膜 PNG");
        if (path is null)
            return;

        try
        {
            var (width, height) = EditorResourceCatalog.ReadPngDimensions(path);
            _draft.Foil.ExternalPath = path;
            _draft.Foil.Source = "package";
            _draft.Foil.Reference = "assets/foil.png";
            _loadingUi = true;
            try
            {
                FoilPackageRadio.IsChecked = true;
                FoilPackagePathText.Text = Path.GetFileName(path);
                FoilPackageInfoText.Text = $"{width} × {height}　→　assets/foil.png";
            }
            finally
            {
                _loadingUi = false;
            }

            UpdateResourcePanels();
            RefreshFoilPreview();
            RefreshEditorState();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "無法使用銀膜 PNG", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SyncBasicFieldsToDraft()
    {
        if (_draft is null)
            return;

        _draft.Name = PackNameTextBox.Text.Trim();
        _draft.Author = AuthorTextBox.Text.Trim();
        _draft.GameType = SelectedGameType();
        _draft.Price = long.TryParse(PriceTextBox.Text.Trim(), out var price) ? price : 0;
    }

    private void PopulateBuiltInResources()
    {
        if (_draft is null)
            return;

        _loadingUi = true;
        try
        {
            var ticketRefs = EditorResourceCatalog.GetBuiltInTicketRefs(_draft.GameType);
            TicketBuiltInComboBox.ItemsSource = ticketRefs;
            if (ticketRefs.Count > 0)
            {
                var wanted = _draft.TicketArt.Source == "builtin" ? _draft.TicketArt.Reference : string.Empty;
                TicketBuiltInComboBox.SelectedItem = ticketRefs.Contains(wanted, StringComparer.Ordinal)
                    ? wanted
                    : ticketRefs[0];
                TicketBuiltInHintText.Text = $"來源：BuiltInAssets/Tickets/gameType{_draft.GameType}/　共 {ticketRefs.Count} 款";
            }
            else
            {
                TicketBuiltInComboBox.SelectedIndex = -1;
                TicketBuiltInHintText.Text = "目前執行目錄找不到可用 Built-in 票面；正式 portable 需與 ScratchGame 共用 BuiltInAssets。";
            }

            var foilRefs = EditorResourceCatalog.GetBuiltInFoilRefs();
            FoilBuiltInComboBox.ItemsSource = foilRefs;
            if (foilRefs.Count > 0)
            {
                var wanted = _draft.Foil.Source == "builtin" ? _draft.Foil.Reference : string.Empty;
                FoilBuiltInComboBox.SelectedItem = foilRefs.Contains(wanted, StringComparer.Ordinal)
                    ? wanted
                    : foilRefs[0];
                FoilBuiltInHintText.Text = $"來源：BuiltInAssets/Foils/　共 {foilRefs.Count} 款";
            }
            else
            {
                FoilBuiltInComboBox.SelectedIndex = -1;
                FoilBuiltInHintText.Text = "目前執行目錄找不到可用 Built-in 銀膜；正式 portable 需與 ScratchGame 共用 BuiltInAssets。";
            }
        }
        finally
        {
            _loadingUi = false;
        }

        UpdateResourcePanels();
    }

    private void ApplyResourceSelectionsFromUi()
    {
        if (_draft is null)
            return;

        if (TicketPackageRadio.IsChecked == true)
        {
            _draft.TicketArt.Source = "package";
            _draft.TicketArt.Reference = string.IsNullOrWhiteSpace(_draft.TicketArt.ExternalPath)
                ? string.Empty
                : "assets/ticket.png";
        }
        else
        {
            _draft.TicketArt.Source = "builtin";
            _draft.TicketArt.Reference = TicketBuiltInComboBox.SelectedItem as string ?? string.Empty;
        }

        if (FoilPackageRadio.IsChecked == true)
        {
            _draft.Foil.Source = "package";
            _draft.Foil.Reference = string.IsNullOrWhiteSpace(_draft.Foil.ExternalPath)
                ? string.Empty
                : "assets/foil.png";
        }
        else
        {
            _draft.Foil.Source = "builtin";
            _draft.Foil.Reference = FoilBuiltInComboBox.SelectedItem as string ?? string.Empty;
        }
    }

    private void UpdateResourcePanels()
    {
        TicketBuiltInPanel.Visibility = TicketPackageRadio.IsChecked == true ? Visibility.Collapsed : Visibility.Visible;
        TicketPackagePanel.Visibility = TicketPackageRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        FoilBuiltInPanel.Visibility = FoilPackageRadio.IsChecked == true ? Visibility.Collapsed : Visibility.Visible;
        FoilPackagePanel.Visibility = FoilPackageRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshPreview()
    {
        if (_draft is null)
        {
            ClearTicketPreview("建立 Pack 並選擇票面素材後，票面會在此顯示。");
            RefreshFoilPreview();
            return;
        }

        try
        {
            var path = ResolveTicketPreviewPath();
            if (path is null)
            {
                ClearTicketPreview("尚未選擇可用票面素材。");
            }
            else
            {
                TicketPreviewImage.Source = EditorResourceCatalog.LoadBitmap(path);
                TicketPreviewImage.Visibility = Visibility.Visible;
                PreviewPlaceholderPanel.Visibility = Visibility.Collapsed;
                PreviewResourceText.Text = $"{_draft.TicketArt.Source}:{_draft.TicketArt.Reference}";
            }
        }
        catch (Exception ex)
        {
            ClearTicketPreview($"票面無法預覽：{ex.Message}");
        }

        RefreshFoilPreview();
        RefreshResourceSummary();
    }

    private void RefreshFoilPreview()
    {
        if (_draft is null)
        {
            FoilPreviewImage.Visibility = Visibility.Collapsed;
            FoilPreviewPlaceholder.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            var path = ResolveFoilPreviewPath();
            if (path is null)
            {
                FoilPreviewImage.Visibility = Visibility.Collapsed;
                FoilPreviewPlaceholder.Visibility = Visibility.Visible;
                FoilPreviewPlaceholder.Text = "尚未選擇銀膜";
                return;
            }

            FoilPreviewImage.Source = EditorResourceCatalog.LoadBitmap(path);
            FoilPreviewImage.Visibility = Visibility.Visible;
            FoilPreviewPlaceholder.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            FoilPreviewImage.Visibility = Visibility.Collapsed;
            FoilPreviewPlaceholder.Visibility = Visibility.Visible;
            FoilPreviewPlaceholder.Text = $"無法預覽：{ex.Message}";
        }

        RefreshResourceSummary();
    }

    private string? ResolveTicketPreviewPath()
    {
        if (_draft is null || string.IsNullOrWhiteSpace(_draft.TicketArt.Reference))
            return null;

        if (_draft.TicketArt.Source == "package")
            return _draft.TicketArt.ExternalPath;

        if (!ScratchPackV1Loader.IsBuiltInTicketSupported(_draft.GameType, _draft.TicketArt.Reference))
            return null;
        var path = ScratchPackV1Loader.ResolveBuiltInTicketPath(_draft.GameType, _draft.TicketArt.Reference);
        return File.Exists(path) ? path : null;
    }

    private string? ResolveFoilPreviewPath()
    {
        if (_draft is null || string.IsNullOrWhiteSpace(_draft.Foil.Reference))
            return null;

        if (_draft.Foil.Source == "package")
            return _draft.Foil.ExternalPath;

        if (!ScratchPackV1Loader.IsBuiltInFoilSupported(_draft.Foil.Reference))
            return null;
        var path = ScratchPackV1Loader.ResolveBuiltInFoilPath(_draft.Foil.Reference);
        return File.Exists(path) ? path : null;
    }

    private void ClearTicketPreview(string message)
    {
        TicketPreviewImage.Source = null;
        TicketPreviewImage.Visibility = Visibility.Collapsed;
        PreviewPlaceholderPanel.Visibility = Visibility.Visible;
        PreviewPlaceholderText.Text = message;
        PreviewResourceText.Text = string.Empty;
    }

    private void RefreshResourceSummary()
    {
        if (_draft is null)
        {
            CurrentResourceSummaryText.Text = string.Empty;
            return;
        }

        var ticket = string.IsNullOrWhiteSpace(_draft.TicketArt.Reference)
            ? "票面：—"
            : $"票面 {_draft.TicketArt.Source}:{_draft.TicketArt.Reference}";
        var foil = string.IsNullOrWhiteSpace(_draft.Foil.Reference)
            ? "銀膜：—"
            : $"銀膜 {_draft.Foil.Source}:{_draft.Foil.Reference}";
        CurrentResourceSummaryText.Text = $"{ticket}　｜　{foil}";
    }

    private void RefreshEditorState()
    {
        if (_draft is null)
        {
            StatusText.Text = "尚未建立 Pack";
            return;
        }

        SyncBasicFieldsToDraft();
        ApplyResourceSelectionsFromUi();
        RefreshResourceSummary();

        var issues = ValidateCurrentStage();
        StatusText.Text = issues.Count == 0
            ? "✓ 基本資料與票面素材完成；下一步：遊戲區域"
            : $"目前階段待完成：{issues[0]}{(issues.Count > 1 ? $"（另 {issues.Count - 1} 項）" : string.Empty)}";
    }

    private List<string> ValidateCurrentStage()
    {
        var issues = new List<string>();
        if (_draft is null)
            return new List<string> { "尚未建立 Pack" };

        if (_draft.Name.Length is < 1 or > 60)
            issues.Add("彩券名稱需為 1～60 字元");
        if (_draft.Price <= 0)
            issues.Add("面額必須大於 0");
        if (_draft.GameType != "1")
            issues.Add("目前只支援 GameType 1");
        if (_draft.Canvas != 1)
            issues.Add("目前只支援 Canvas 1");

        if (_draft.TicketArt.Source == "builtin")
        {
            if (string.IsNullOrWhiteSpace(_draft.TicketArt.Reference) ||
                !ScratchPackV1Loader.IsBuiltInTicketSupported(_draft.GameType, _draft.TicketArt.Reference))
                issues.Add("請選擇有效的 Built-in 票面");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(_draft.TicketArt.ExternalPath) || !File.Exists(_draft.TicketArt.ExternalPath))
            {
                issues.Add("請選擇自訂票面 PNG");
            }
            else
            {
                try
                {
                    var size = EditorResourceCatalog.ReadPngDimensions(_draft.TicketArt.ExternalPath);
                    if (size.Width != 1080 || size.Height != 882)
                        issues.Add("自訂票面必須為 1080 × 882");
                }
                catch
                {
                    issues.Add("自訂票面 PNG 無法解碼");
                }
            }
        }

        if (_draft.Foil.Source == "builtin")
        {
            if (string.IsNullOrWhiteSpace(_draft.Foil.Reference) ||
                !ScratchPackV1Loader.IsBuiltInFoilSupported(_draft.Foil.Reference))
                issues.Add("請選擇有效的 Built-in 銀膜");
        }
        else if (string.IsNullOrWhiteSpace(_draft.Foil.ExternalPath) || !File.Exists(_draft.Foil.ExternalPath))
        {
            issues.Add("請選擇自訂銀膜 PNG");
        }
        else
        {
            try
            {
                _ = EditorResourceCatalog.ReadPngDimensions(_draft.Foil.ExternalPath);
            }
            catch
            {
                issues.Add("自訂銀膜 PNG 無法解碼");
            }
        }

        return issues;
    }

    private string SelectedGameType()
    {
        if (GameTypeComboBox.SelectedItem is ComboBoxItem item && item.Tag is string gameType)
            return gameType;
        return "1";
    }

    private static string? PickPng(string title)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = "PNG 圖片 (*.png)|*.png",
            CheckFileExists = true,
            Multiselect = false
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static Version GetEditorVersion()
        => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

    private static string FormatVersion(Version version)
        => $"{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}";
}
