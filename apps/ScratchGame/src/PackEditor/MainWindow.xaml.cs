using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using ScratchGame.Models;
using ScratchGame.Services;

namespace PackEditor;

public partial class MainWindow : Window
{
    private static readonly Brush ZoneBrush = new SolidColorBrush(Color.FromRgb(218, 55, 170));
    private static readonly Brush PriceBrush = new SolidColorBrush(Color.FromRgb(42, 198, 226));
    private static readonly Brush SerialBrush = new SolidColorBrush(Color.FromRgb(77, 196, 116));

    private PackDraft? _draft;
    private bool _loadingUi;

    public MainWindow()
    {
        InitializeComponent();
        ShowPanel(BasicPanel, BasicNavButton);
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

            PopulateLayoutControlsFromDraft();
            IssueSizeTextBox.Text = _draft.IssueSize.ToString();
            TicketsPerBookTextBox.Text = _draft.TicketsPerBook.ToString();
            ValidationResultText.Text = "尚未執行完整驗證。";
        }
        finally
        {
            _loadingUi = false;
        }

        PopulateBuiltInResources();
        ApplyResourceSelectionsFromUi();
        BindPrizeGrid();
        ShowPanel(BasicPanel, BasicNavButton);
        ExportPackButton.IsEnabled = true;
        RefreshPreview();
        RefreshLayoutOverlay();
        RefreshPrizeSummary();
        RefreshEditorState();
        PackNameTextBox.Focus();
        PackNameTextBox.SelectAll();
    }

    private void BasicNav_OnClick(object sender, RoutedEventArgs e) => ShowPanel(BasicPanel, BasicNavButton);
    private void AssetsNav_OnClick(object sender, RoutedEventArgs e) => ShowPanel(AssetsPanel, AssetsNavButton);
    private void GameAreaNav_OnClick(object sender, RoutedEventArgs e) => ShowPanel(GameAreaPanel, GameAreaNavButton);
    private void PrizeNav_OnClick(object sender, RoutedEventArgs e) => ShowPanel(PrizePanel, PrizeNavButton);
    private void ValidationNav_OnClick(object sender, RoutedEventArgs e) => ShowPanel(ValidationPanel, ValidationNavButton);

    private void ShowPanel(StackPanel panel, Button activeButton)
    {
        foreach (var candidate in new[] { BasicPanel, AssetsPanel, GameAreaPanel, PrizePanel, ValidationPanel })
            candidate.Visibility = candidate == panel ? Visibility.Visible : Visibility.Collapsed;

        foreach (var button in new[] { BasicNavButton, AssetsNavButton, GameAreaNavButton, PrizeNavButton, ValidationNavButton })
            button.Background = button == activeButton
                ? new SolidColorBrush(Color.FromRgb(79, 43, 31))
                : Brushes.Transparent;
    }

    private void DraftField_OnChanged(object sender, TextChangedEventArgs e)
    {
        if (_loadingUi || _draft is null)
            return;

        SyncBasicFieldsToDraft();
        RefreshPrizeSummary();
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
            ShowNeedNewPack();
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
            ShowNeedNewPack();
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

    private void GridSizeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingUi || _draft is null)
            return;

        if (GridSizeComboBox.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out var gridSize))
        {
            _draft.GridSize = gridSize;
            _draft.EnsurePrizeTiers();
            BindPrizeGrid();
            RefreshPrizeSummary();
            RefreshLayoutOverlay();
            RefreshEditorState();
        }
    }

    private void ZoneShapeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingUi || _draft is null)
            return;

        _draft.ZoneShape = SelectedZoneShape();
        CornerRadiusTextBox.IsEnabled = _draft.ZoneShape == "roundedRectangle";
        RefreshLayoutOverlay();
        RefreshEditorState();
    }

    private void LayoutField_OnChanged(object sender, TextChangedEventArgs e)
    {
        if (_loadingUi || _draft is null)
            return;

        SyncLayoutFieldsToDraft();
        RefreshLayoutOverlay();
        RefreshEditorState();
    }

    private void LayoutOption_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_loadingUi || _draft is null)
            return;

        _draft.AllowNearMiss = AllowNearMissCheckBox.IsChecked == true;
        _draft.PriceDisplay = PriceDisplayCheckBox.IsChecked == true;
        SetPriceAreaControlsEnabled(_draft.PriceDisplay);
        RefreshLayoutOverlay();
        RefreshEditorState();
    }

    private void PrizeHeader_OnChanged(object sender, TextChangedEventArgs e)
    {
        if (_loadingUi || _draft is null)
            return;

        SyncPrizeHeaderToDraft();
        RefreshPrizeSummary();
        RefreshEditorState();
    }

    private void PrizeDataGrid_OnCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            RefreshPrizeSummary();
            RefreshEditorState();
        }));
    }

    private void ValidateDraft_OnClick(object sender, RoutedEventArgs e)
    {
        if (_draft is null)
        {
            ShowNeedNewPack();
            return;
        }

        SyncAllControlsToDraft();
        var issues = ValidateEditorInputs();
        if (issues.Count > 0)
        {
            ValidationResultText.Text = "✕ 編輯檢查尚未通過：\n" + string.Join("\n", issues.Select(issue => "• " + issue));
            StatusText.Text = $"完整驗證未執行：{issues[0]}";
            return;
        }

        var tempPack = Path.Combine(Path.GetTempPath(), $"packeditor-validate-{Guid.NewGuid():N}.scratchpack");
        try
        {
            var loaded = PackExportService.ExportAndRoundTripValidate(_draft, tempPack);
            ValidationResultText.Text = $"✓ Round-trip PASS\nPackage ID：{loaded.Manifest.PackageId:D}\nContent SHA-256：{loaded.ContentHash}";
            StatusText.Text = "✓ ScratchPack V1 完整驗證通過";
        }
        catch (Exception ex)
        {
            ValidationResultText.Text = $"✕ ScratchPackV1Loader 驗證失敗：\n{ex.Message}";
            StatusText.Text = "完整驗證失敗";
        }
        finally
        {
            try { if (File.Exists(tempPack)) File.Delete(tempPack); } catch { }
        }
    }

    private void ExportPack_OnClick(object sender, RoutedEventArgs e)
    {
        if (_draft is null)
        {
            ShowNeedNewPack();
            return;
        }

        SyncAllControlsToDraft();
        var issues = ValidateEditorInputs();
        if (issues.Count > 0)
        {
            ValidationResultText.Text = "✕ 無法輸出：\n" + string.Join("\n", issues.Select(issue => "• " + issue));
            ShowPanel(ValidationPanel, ValidationNavButton);
            StatusText.Text = $"無法輸出：{issues[0]}";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "輸出 ScratchPack",
            Filter = "ScratchPack (*.scratchpack)|*.scratchpack",
            AddExtension = true,
            DefaultExt = ".scratchpack",
            FileName = MakeSafeFileName(_draft.Name) + ".scratchpack"
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var loaded = PackExportService.ExportAndRoundTripValidate(_draft, dialog.FileName);
            ValidationResultText.Text = $"✓ 已輸出並通過 Round-trip 驗證\n{dialog.FileName}\nPackage ID：{loaded.Manifest.PackageId:D}\nContent SHA-256：{loaded.ContentHash}";
            ShowPanel(ValidationPanel, ValidationNavButton);
            StatusText.Text = $"✓ 已輸出 {Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception ex)
        {
            ValidationResultText.Text = $"✕ 輸出失敗：\n{ex.Message}";
            ShowPanel(ValidationPanel, ValidationNavButton);
            StatusText.Text = "輸出失敗";
            MessageBox.Show(this, ex.Message, "ScratchPack 輸出失敗", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void PopulateLayoutControlsFromDraft()
    {
        if (_draft is null)
            return;

        SelectComboByTag(GridSizeComboBox, _draft.GridSize.ToString());
        SelectComboByTag(ZoneShapeComboBox, _draft.ZoneShape);
        AllowNearMissCheckBox.IsChecked = _draft.AllowNearMiss;
        PriceDisplayCheckBox.IsChecked = _draft.PriceDisplay;
        ZoneXTextBox.Text = _draft.ZoneX.ToString();
        ZoneYTextBox.Text = _draft.ZoneY.ToString();
        ZoneWidthTextBox.Text = _draft.ZoneWidth.ToString();
        ZoneHeightTextBox.Text = _draft.ZoneHeight.ToString();
        HorizontalGapTextBox.Text = _draft.HorizontalGap.ToString();
        VerticalGapTextBox.Text = _draft.VerticalGap.ToString();
        CornerRadiusTextBox.Text = _draft.CornerRadius.ToString();
        PriceXTextBox.Text = _draft.PriceArea.X.ToString();
        PriceYTextBox.Text = _draft.PriceArea.Y.ToString();
        PriceWidthTextBox.Text = _draft.PriceArea.Width.ToString();
        PriceHeightTextBox.Text = _draft.PriceArea.Height.ToString();
        SerialXTextBox.Text = _draft.SerialArea.X.ToString();
        SerialYTextBox.Text = _draft.SerialArea.Y.ToString();
        SerialWidthTextBox.Text = _draft.SerialArea.Width.ToString();
        SerialHeightTextBox.Text = _draft.SerialArea.Height.ToString();
        CornerRadiusTextBox.IsEnabled = _draft.ZoneShape == "roundedRectangle";
        SetPriceAreaControlsEnabled(_draft.PriceDisplay);
    }

    private void SyncAllControlsToDraft()
    {
        SyncBasicFieldsToDraft();
        ApplyResourceSelectionsFromUi();
        SyncLayoutFieldsToDraft();
        SyncPrizeHeaderToDraft();
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

    private void SyncLayoutFieldsToDraft()
    {
        if (_draft is null)
            return;

        _draft.ZoneX = ReadInt(ZoneXTextBox);
        _draft.ZoneY = ReadInt(ZoneYTextBox);
        _draft.ZoneWidth = ReadInt(ZoneWidthTextBox);
        _draft.ZoneHeight = ReadInt(ZoneHeightTextBox);
        _draft.HorizontalGap = ReadInt(HorizontalGapTextBox);
        _draft.VerticalGap = ReadInt(VerticalGapTextBox);
        _draft.CornerRadius = ReadInt(CornerRadiusTextBox);
        _draft.ZoneShape = SelectedZoneShape();
        _draft.AllowNearMiss = AllowNearMissCheckBox.IsChecked == true;
        _draft.PriceDisplay = PriceDisplayCheckBox.IsChecked == true;
        _draft.PriceArea.X = ReadInt(PriceXTextBox);
        _draft.PriceArea.Y = ReadInt(PriceYTextBox);
        _draft.PriceArea.Width = ReadInt(PriceWidthTextBox);
        _draft.PriceArea.Height = ReadInt(PriceHeightTextBox);
        _draft.SerialArea.X = ReadInt(SerialXTextBox);
        _draft.SerialArea.Y = ReadInt(SerialYTextBox);
        _draft.SerialArea.Width = ReadInt(SerialWidthTextBox);
        _draft.SerialArea.Height = ReadInt(SerialHeightTextBox);
    }

    private void SyncPrizeHeaderToDraft()
    {
        if (_draft is null)
            return;

        _draft.IssueSize = long.TryParse(IssueSizeTextBox.Text.Trim(), out var issueSize) ? issueSize : 0;
        _draft.TicketsPerBook = long.TryParse(TicketsPerBookTextBox.Text.Trim(), out var perBook) ? perBook : 0;
    }

    private void BindPrizeGrid()
    {
        if (_draft is null)
            return;
        PrizeDataGrid.ItemsSource = null;
        PrizeDataGrid.ItemsSource = _draft.Prizes;
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
                TicketBuiltInComboBox.SelectedItem = ticketRefs.Contains(wanted, StringComparer.Ordinal) ? wanted : ticketRefs[0];
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
                FoilBuiltInComboBox.SelectedItem = foilRefs.Contains(wanted, StringComparer.Ordinal) ? wanted : foilRefs[0];
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
            _draft.TicketArt.Reference = string.IsNullOrWhiteSpace(_draft.TicketArt.ExternalPath) ? string.Empty : "assets/ticket.png";
        }
        else
        {
            _draft.TicketArt.Source = "builtin";
            _draft.TicketArt.Reference = TicketBuiltInComboBox.SelectedItem as string ?? string.Empty;
        }

        if (FoilPackageRadio.IsChecked == true)
        {
            _draft.Foil.Source = "package";
            _draft.Foil.Reference = string.IsNullOrWhiteSpace(_draft.Foil.ExternalPath) ? string.Empty : "assets/foil.png";
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
            RefreshLayoutOverlay();
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
        RefreshLayoutOverlay();
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

    private void RefreshLayoutOverlay()
    {
        EditorOverlayCanvas.Children.Clear();
        if (_draft is null)
            return;

        foreach (var zone in _draft.BuildZones())
            AddOverlayRect(new ScratchPackRect(zone.X, zone.Y, zone.Width, zone.Height), ZoneBrush, zone.Id, zone.CornerRadius ?? 0);

        if (_draft.PriceDisplay)
            AddOverlayRect(_draft.PriceArea.ToRect(), PriceBrush, $"面額 ${_draft.Price:N0}", 8);
        AddOverlayRect(_draft.SerialArea.ToRect(), SerialBrush, "票號", 8);
    }

    private void AddOverlayRect(ScratchPackRect rect, Brush brush, string label, int cornerRadius)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
            return;

        var border = new Border
        {
            Width = rect.Width,
            Height = rect.Height,
            BorderBrush = brush,
            BorderThickness = new Thickness(3),
            CornerRadius = new CornerRadius(Math.Max(0, cornerRadius)),
            Background = new SolidColorBrush(Color.FromArgb(30, ((SolidColorBrush)brush).Color.R, ((SolidColorBrush)brush).Color.G, ((SolidColorBrush)brush).Color.B)),
            Child = new TextBlock
            {
                Text = label,
                Foreground = brush,
                Background = new SolidColorBrush(Color.FromArgb(190, 35, 18, 14)),
                FontFamily = new FontFamily("Microsoft JhengHei UI"),
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(5, 2, 5, 2),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            }
        };
        Canvas.SetLeft(border, rect.X);
        Canvas.SetTop(border, rect.Y);
        EditorOverlayCanvas.Children.Add(border);
    }

    private void RefreshPrizeSummary()
    {
        if (_draft is null)
        {
            foreach (var text in new[] { WinningCountText, LosingCountText, WinRateSummaryText, TotalSalesText, TotalPrizeText, ExpectedPrizeText, RewardRateText })
                text.Text = "—";
            return;
        }

        var winningCount = _draft.Prizes.Where(p => p.Count > 0).Sum(p => p.Count);
        var losingCount = Math.Max(0, _draft.IssueSize - winningCount);
        decimal totalPrize = 0;
        foreach (var tier in _draft.Prizes)
            totalPrize += (decimal)tier.Amount * tier.Count;
        var totalSales = (decimal)_draft.Price * _draft.IssueSize;

        WinningCountText.Text = $"{winningCount:N0} 張";
        LosingCountText.Text = $"{losingCount:N0} 張";
        WinRateSummaryText.Text = _draft.IssueSize > 0 ? ((decimal)winningCount / _draft.IssueSize).ToString("P2") : "—";
        TotalSalesText.Text = totalSales >= 0 ? $"${totalSales:N0}" : "—";
        TotalPrizeText.Text = totalPrize >= 0 ? $"${totalPrize:N0}" : "—";
        ExpectedPrizeText.Text = _draft.IssueSize > 0 ? $"${totalPrize / _draft.IssueSize:N2}" : "—";
        RewardRateText.Text = totalSales > 0 ? (totalPrize / totalSales).ToString("P2") : "—";
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

        var ticket = string.IsNullOrWhiteSpace(_draft.TicketArt.Reference) ? "票面：—" : $"票面 {_draft.TicketArt.Source}:{_draft.TicketArt.Reference}";
        var foil = string.IsNullOrWhiteSpace(_draft.Foil.Reference) ? "銀膜：—" : $"銀膜 {_draft.Foil.Source}:{_draft.Foil.Reference}";
        CurrentResourceSummaryText.Text = $"{ticket}　｜　{foil}";
    }

    private void RefreshEditorState()
    {
        if (_draft is null)
        {
            StatusText.Text = "尚未建立 Pack";
            ExportPackButton.IsEnabled = false;
            return;
        }

        SyncAllControlsToDraft();
        RefreshResourceSummary();
        var issues = ValidateEditorInputs();
        StatusText.Text = issues.Count == 0
            ? "✓ 編輯檢查通過；可執行完整 Round-trip 驗證或輸出"
            : $"目前待完成：{issues[0]}{(issues.Count > 1 ? $"（另 {issues.Count - 1} 項）" : string.Empty)}";
        ExportPackButton.IsEnabled = true;
    }

    private List<string> ValidateEditorInputs()
    {
        var issues = new List<string>();
        if (_draft is null)
            return new List<string> { "尚未建立 Pack" };

        if (_draft.Name.Length is < 1 or > 60) issues.Add("彩券名稱需為 1～60 字元");
        if (_draft.Price <= 0) issues.Add("面額必須大於 0");
        if (_draft.GameType != "1") issues.Add("目前只支援 GameType 1");
        if (_draft.Canvas != 1) issues.Add("目前只支援 Canvas 1");

        if (_draft.TicketArt.Source == "builtin")
        {
            if (string.IsNullOrWhiteSpace(_draft.TicketArt.Reference) || !ScratchPackV1Loader.IsBuiltInTicketSupported(_draft.GameType, _draft.TicketArt.Reference))
                issues.Add("請選擇有效的 Built-in 票面");
        }
        else if (!ValidTicketPackageAsset())
        {
            issues.Add("自訂票面必須是有效的 1080 × 882 PNG");
        }

        if (_draft.Foil.Source == "builtin")
        {
            if (string.IsNullOrWhiteSpace(_draft.Foil.Reference) || !ScratchPackV1Loader.IsBuiltInFoilSupported(_draft.Foil.Reference))
                issues.Add("請選擇有效的 Built-in 銀膜");
        }
        else if (!ValidPackagePng(_draft.Foil.ExternalPath))
        {
            issues.Add("請選擇有效的自訂銀膜 PNG");
        }

        if (_draft.GridSize is not 3 and not 4 and not 5) issues.Add("Grid Size 只允許 3、4、5");
        if (_draft.ZoneWidth <= 0 || _draft.ZoneHeight <= 0) issues.Add("Scratch Zone 尺寸必須大於 0");
        if (_draft.HorizontalGap < 0 || _draft.VerticalGap < 0) issues.Add("Scratch Zone 間距不可為負數");
        if (_draft.ZoneX < 0 || _draft.ZoneY < 0) issues.Add("Scratch Zone 起點不可為負數");
        var gridWidth = (long)_draft.ZoneWidth * _draft.GridSize + (long)_draft.HorizontalGap * (_draft.GridSize - 1);
        var gridHeight = (long)_draft.ZoneHeight * _draft.GridSize + (long)_draft.VerticalGap * (_draft.GridSize - 1);
        if (_draft.ZoneX + gridWidth > 1080 || _draft.ZoneY + gridHeight > 882) issues.Add("Scratch Zone 網格超出 Canvas 1");
        if (_draft.ZoneShape is not "rectangle" and not "roundedRectangle" and not "circle" and not "ellipse") issues.Add("Scratch Zone shape 無效");
        if (_draft.ZoneShape == "roundedRectangle" && (_draft.CornerRadius < 0 || _draft.CornerRadius * 2 > Math.Min(_draft.ZoneWidth, _draft.ZoneHeight))) issues.Add("roundedRectangle 圓角無效");
        if (_draft.ZoneShape == "circle" && _draft.ZoneWidth != _draft.ZoneHeight) issues.Add("circle 的 Width / Height 必須相同");
        if (_draft.PriceDisplay && !RectInsideCanvas(_draft.PriceArea)) issues.Add("面額區超出 Canvas 或尺寸無效");
        if (!RectInsideCanvas(_draft.SerialArea)) issues.Add("票號區超出 Canvas 或尺寸無效");

        if (_draft.IssueSize <= 0 || _draft.TicketsPerBook <= 0 || _draft.IssueSize % _draft.TicketsPerBook != 0) issues.Add("發行張數 / 每本張數必須大於 0 且可整除");
        if (_draft.Prizes.Count != 2 * _draft.GridSize + 1) issues.Add("Prize Tier 數量不符合 GameType 1");
        if (_draft.Prizes.Any(p => p.Amount <= 0)) issues.Add("所有獎金必須大於 0");
        if (_draft.Prizes.Any(p => p.Count < 0)) issues.Add("Prize Tier 張數不可為負數");
        for (var i = 1; i < _draft.Prizes.Count; i++)
            if (_draft.Prizes[i].Amount <= _draft.Prizes[i - 1].Amount)
            {
                issues.Add("獎金必須依線數嚴格遞增");
                break;
            }
        if (_draft.Prizes.Sum(p => p.Count) > _draft.IssueSize) issues.Add("中獎張數總和不可超過發行張數");

        return issues;
    }

    private bool ValidTicketPackageAsset()
    {
        if (_draft is null || !ValidPackagePng(_draft.TicketArt.ExternalPath))
            return false;
        try
        {
            var size = EditorResourceCatalog.ReadPngDimensions(_draft.TicketArt.ExternalPath!);
            return size.Width == 1080 && size.Height == 882;
        }
        catch { return false; }
    }

    private static bool ValidPackagePng(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || !string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase))
            return false;
        try { _ = EditorResourceCatalog.ReadPngDimensions(path); return true; }
        catch { return false; }
    }

    private static bool RectInsideCanvas(EditorRect rect)
        => rect.X >= 0 && rect.Y >= 0 && rect.Width > 0 && rect.Height > 0 && rect.X + (long)rect.Width <= 1080 && rect.Y + (long)rect.Height <= 882;

    private void SetPriceAreaControlsEnabled(bool enabled)
    {
        foreach (var control in new[] { PriceXTextBox, PriceYTextBox, PriceWidthTextBox, PriceHeightTextBox })
            control.IsEnabled = enabled;
    }

    private static int ReadInt(TextBox textBox)
        => int.TryParse(textBox.Text.Trim(), out var value) ? value : 0;

    private string SelectedGameType()
        => GameTypeComboBox.SelectedItem is ComboBoxItem item && item.Tag is string gameType ? gameType : "1";

    private string SelectedZoneShape()
        => ZoneShapeComboBox.SelectedItem is ComboBoxItem item && item.Tag is string shape ? shape : "roundedRectangle";

    private static void SelectComboByTag(ComboBox comboBox, string tag)
    {
        foreach (var entry in comboBox.Items)
        {
            if (entry is ComboBoxItem item && string.Equals(item.Tag?.ToString(), tag, StringComparison.Ordinal))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }
        comboBox.SelectedIndex = comboBox.Items.Count > 0 ? 0 : -1;
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

    private static string MakeSafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var safe = new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(safe) ? "ScratchPack" : safe;
    }

    private void ShowNeedNewPack()
        => MessageBox.Show(this, "請先按「新增 Pack」。", "PackEditor", MessageBoxButton.OK, MessageBoxImage.Information);
}
