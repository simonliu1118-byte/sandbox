using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using ScratchGame.Services;

namespace PackEditor;

public partial class MainWindow
{
    private enum OverlayTarget { None, Grid, Price, Serial }

    private int _currentWorkflowStep = 1;
    private bool _lastFullValidationPassed;
    private OverlayTarget _dragTarget = OverlayTarget.None;
    private Point _dragStartPoint;
    private int _dragStartX;
    private int _dragStartY;
    private int _dragStartWidth;
    private int _dragStartHeight;

    private static readonly IReadOnlyDictionary<string, string> TicketDisplayNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["01-blue"] = "內建票面 A - 藍",
        ["01-red"] = "內建票面 A - 紅",
        ["02"] = "內建票面 B - 紅"
    };

    private static readonly IReadOnlyDictionary<string, string> FoilDisplayNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["brushed-silver-plain"] = "素面銀膜",
        ["brushed-silver-three-star"] = "三星銀膜"
    };

    private void WindowV051_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_draft is not null)
            return;

        NewPack_OnClick(this, new RoutedEventArgs());
        if (_draft is null)
            return;

        _loadingUi = true;
        try
        {
            _draft.Name = string.Empty;
            _draft.Author = string.Empty;
            _draft.Price = 0;
            _draft.TicketArt.Source = "builtin";
            _draft.TicketArt.Reference = string.Empty;
            _draft.TicketArt.ExternalPath = null;
            _draft.Foil.Source = "builtin";
            _draft.Foil.Reference = string.Empty;
            _draft.Foil.ExternalPath = null;

            PackNameTextBox.Text = string.Empty;
            AuthorTextBox.Text = string.Empty;
            PriceTextBox.Text = string.Empty;
            GameTypeComboBox.SelectedIndex = 0;
            CanvasComboBox.SelectedIndex = 0;
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

        PopulateFriendlyResourceChoices(selectDefaults: false);
        UpdateV051ResourcePanels();
        BindPrizeGrid();
        RefreshPrizeSummary();
        GoToWorkflowStep(1, runValidation: false);
        RefreshV051Preview();
        MarkValidationDirty();
        PackNameTextBox.Focus();
    }

    private void PopulateFriendlyResourceChoices(bool selectDefaults)
    {
        if (_draft is null)
            return;

        var previousLoading = _loadingUi;
        _loadingUi = true;
        try
        {
            var wantedTicket = _draft.TicketArt.Reference;
            var ticketRefs = EditorResourceCatalog.GetBuiltInTicketRefs(_draft.GameType);
            TicketBuiltInComboBox.ItemsSource = null;
            TicketBuiltInComboBox.Items.Clear();
            foreach (var reference in ticketRefs)
            {
                TicketBuiltInComboBox.Items.Add(new ComboBoxItem
                {
                    Content = TicketDisplayNames.TryGetValue(reference, out var name) ? name : reference,
                    Tag = reference
                });
            }
            SelectResourceByTag(TicketBuiltInComboBox, wantedTicket, selectDefaults);

            var wantedFoil = _draft.Foil.Reference;
            var foilRefs = EditorResourceCatalog.GetBuiltInFoilRefs();
            FoilBuiltInComboBox.ItemsSource = null;
            FoilBuiltInComboBox.Items.Clear();
            foreach (var reference in foilRefs)
            {
                FoilBuiltInComboBox.Items.Add(new ComboBoxItem
                {
                    Content = FoilDisplayNames.TryGetValue(reference, out var name) ? name : reference,
                    Tag = reference
                });
            }
            SelectResourceByTag(FoilBuiltInComboBox, wantedFoil, selectDefaults);
        }
        finally
        {
            _loadingUi = previousLoading;
        }
    }

    private static void SelectResourceByTag(ComboBox comboBox, string? wanted, bool selectDefault)
    {
        comboBox.SelectedIndex = -1;
        if (!string.IsNullOrWhiteSpace(wanted))
        {
            foreach (var entry in comboBox.Items)
            {
                if (entry is ComboBoxItem item && string.Equals(item.Tag?.ToString(), wanted, StringComparison.Ordinal))
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }
        }
        if (selectDefault && comboBox.Items.Count > 0)
            comboBox.SelectedIndex = 0;
    }

    private static string SelectedResourceRef(ComboBox comboBox)
        => comboBox.SelectedItem is ComboBoxItem item ? item.Tag?.ToString() ?? string.Empty : string.Empty;

    private void V051BasicField_OnChanged(object sender, TextChangedEventArgs e)
    {
        if (_loadingUi || _draft is null)
            return;
        SyncV051BasicAndResources();
        RefreshPrizeSummary();
        RefreshInteractiveOverlay();
        MarkValidationDirty();
    }

    private void V051GameType_OnChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingUi || _draft is null)
            return;
        SyncV051BasicAndResources();
        PopulateFriendlyResourceChoices(selectDefaults: false);
        SyncV051BasicAndResources();
        RefreshV051Preview();
        MarkValidationDirty();
    }

    private void V051Canvas_OnChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingUi || _draft is null)
            return;
        _draft.Canvas = CanvasComboBox.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out var canvas) ? canvas : 1;
        MarkValidationDirty();
        RefreshV051Preview();
    }

    private void V051TicketSource_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_loadingUi || _draft is null)
            return;
        UpdateV051ResourcePanels();
        SyncV051BasicAndResources();
        RefreshV051Preview();
        MarkValidationDirty();
    }

    private void V051FoilSource_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_loadingUi || _draft is null)
            return;
        UpdateV051ResourcePanels();
        SyncV051BasicAndResources();
        RefreshFoilPreview();
        RefreshInteractiveOverlay();
        MarkValidationDirty();
    }

    private void V051TicketResource_OnChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingUi || _draft is null)
            return;
        SyncV051BasicAndResources();
        RefreshV051Preview();
        MarkValidationDirty();
    }

    private void V051FoilResource_OnChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingUi || _draft is null)
            return;
        SyncV051BasicAndResources();
        RefreshFoilPreview();
        RefreshInteractiveOverlay();
        MarkValidationDirty();
    }

    private void UpdateV051ResourcePanels()
    {
        TicketBuiltInPanel.Visibility = TicketPackageRadio.IsChecked == true ? Visibility.Collapsed : Visibility.Visible;
        TicketPackagePanel.Visibility = TicketPackageRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        FoilBuiltInPanel.Visibility = FoilPackageRadio.IsChecked == true ? Visibility.Collapsed : Visibility.Visible;
        FoilPackagePanel.Visibility = FoilPackageRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SyncV051BasicAndResources()
    {
        if (_draft is null)
            return;

        _draft.Name = PackNameTextBox.Text.Trim();
        _draft.Author = AuthorTextBox.Text.Trim();
        _draft.GameType = GameTypeComboBox.SelectedItem is ComboBoxItem gameItem ? gameItem.Tag?.ToString() ?? "1" : "1";
        _draft.Price = long.TryParse(PriceTextBox.Text.Trim(), out var price) ? price : 0;
        _draft.Canvas = CanvasComboBox.SelectedItem is ComboBoxItem canvasItem && int.TryParse(canvasItem.Tag?.ToString(), out var canvas) ? canvas : 1;

        if (TicketPackageRadio.IsChecked == true)
        {
            _draft.TicketArt.Source = "package";
            _draft.TicketArt.Reference = string.IsNullOrWhiteSpace(_draft.TicketArt.ExternalPath) ? string.Empty : "assets/ticket.png";
        }
        else
        {
            _draft.TicketArt.Source = "builtin";
            _draft.TicketArt.Reference = SelectedResourceRef(TicketBuiltInComboBox);
        }

        if (FoilPackageRadio.IsChecked == true)
        {
            _draft.Foil.Source = "package";
            _draft.Foil.Reference = string.IsNullOrWhiteSpace(_draft.Foil.ExternalPath) ? string.Empty : "assets/foil.png";
        }
        else
        {
            _draft.Foil.Source = "builtin";
            _draft.Foil.Reference = SelectedResourceRef(FoilBuiltInComboBox);
        }
    }

    private void V051ChooseTicketPng_OnClick(object sender, RoutedEventArgs e)
    {
        if (_draft is null)
            return;

        var path = PickPng("選擇票面圖片");
        if (path is null)
            return;

        try
        {
            var (width, height) = EditorResourceCatalog.ReadPngDimensions(path);
            if (_draft.Canvas == 1 && (width != 1080 || height != 882))
            {
                MessageBox.Show(this, $"目前彩券尺寸需要 1080 × 882 px。\n選擇的圖片為 {width} × {height} px。", "票面尺寸不符", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                TicketPackageInfoText.Text = $"{width} × {height} px";
            }
            finally { _loadingUi = false; }
            UpdateV051ResourcePanels();
            RefreshV051Preview();
            MarkValidationDirty();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "無法使用票面圖片", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void V051ChooseFoilPng_OnClick(object sender, RoutedEventArgs e)
    {
        if (_draft is null)
            return;

        var path = PickPng("選擇銀膜圖片");
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
                FoilPackageInfoText.Text = $"{width} × {height} px";
            }
            finally { _loadingUi = false; }
            UpdateV051ResourcePanels();
            RefreshFoilPreview();
            RefreshInteractiveOverlay();
            MarkValidationDirty();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "無法使用銀膜圖片", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void V051GridSize_OnChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingUi || _draft is null)
            return;
        if (GridSizeComboBox.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out var size))
        {
            _draft.GridSize = size;
            _draft.EnsurePrizeTiers();
            BindPrizeGrid();
            RefreshPrizeSummary();
            RefreshInteractiveOverlay();
            MarkValidationDirty();
        }
    }

    private void V051LayoutCombo_OnChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingUi || _draft is null)
            return;
        SyncLayoutFieldsToDraft();
        CornerRadiusTextBox.IsEnabled = _draft.ZoneShape == "roundedRectangle";
        RefreshInteractiveOverlay();
        MarkValidationDirty();
    }

    private void V051LayoutField_OnChanged(object sender, TextChangedEventArgs e)
    {
        if (_loadingUi || _draft is null)
            return;
        SyncLayoutFieldsToDraft();
        RefreshInteractiveOverlay();
        MarkValidationDirty();
    }

    private void V051LayoutOption_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_loadingUi || _draft is null)
            return;
        SyncLayoutFieldsToDraft();
        SetPriceAreaControlsEnabled(_draft.PriceDisplay);
        RefreshInteractiveOverlay();
        MarkValidationDirty();
    }

    private void V051PrizeHeader_OnChanged(object sender, TextChangedEventArgs e)
    {
        if (_loadingUi || _draft is null)
            return;
        SyncPrizeHeaderToDraft();
        RefreshPrizeSummary();
        MarkValidationDirty();
    }

    private void V051PrizeDataGrid_OnCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            RefreshPrizeSummary();
            MarkValidationDirty();
        }));
    }

    private void V051Next_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || !int.TryParse(button.Tag?.ToString(), out var step))
            return;

        SyncV051Step(step);
        var issues = ValidateV051Step(step);
        if (issues.Count > 0)
        {
            ShowStepIssues(step, issues);
            return;
        }
        GoToWorkflowStep(Math.Min(4, step + 1), runValidation: true);
    }

    private void V051Previous_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || !int.TryParse(button.Tag?.ToString(), out var step))
            return;
        GoToWorkflowStep(Math.Max(1, step - 1), runValidation: false);
    }

    private void GoToWorkflowStep(int step, bool runValidation)
    {
        _currentWorkflowStep = Math.Clamp(step, 1, 4);
        switch (_currentWorkflowStep)
        {
            case 1: ShowPanel(BasicPanel, BasicNavButton); break;
            case 2: ShowPanel(GameAreaPanel, GameAreaNavButton); break;
            case 3: ShowPanel(PrizePanel, PrizeNavButton); break;
            default:
                ShowPanel(ValidationPanel, ValidationNavButton);
                if (runValidation)
                    RunV051FullValidation();
                break;
        }
    }

    private void SyncV051Step(int step)
    {
        switch (step)
        {
            case 1:
                SyncV051BasicAndResources();
                break;
            case 2:
                SyncLayoutFieldsToDraft();
                break;
            case 3:
                PrizeDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                PrizeDataGrid.CommitEdit(DataGridEditingUnit.Row, true);
                SyncPrizeHeaderToDraft();
                break;
        }
    }

    private List<string> ValidateV051Step(int step)
        => step switch
        {
            1 => ValidateV051BasicAndAssets(),
            2 => ValidateV051GameArea(),
            3 => ValidateV051PrizePool(),
            _ => new List<string>()
        };

    private List<string> ValidateV051BasicAndAssets()
    {
        var issues = new List<string>();
        if (_draft is null)
            return new List<string> { "編輯內容尚未初始化" };

        if (_draft.Name.Length is < 1 or > 60) issues.Add("請填寫彩券名稱（1～60 字元）");
        if (_draft.Author.Length is < 1 or > 60) issues.Add("請填寫作者（1～60 字元）");
        if (_draft.Price <= 0) issues.Add("面額必須大於 0");
        if (_draft.GameType != "1") issues.Add("目前僅支援「星星連線」");
        if (_draft.Canvas != 1) issues.Add("目前僅支援 1080 × 882 px 彩券尺寸");

        if (_draft.TicketArt.Source == "builtin")
        {
            if (string.IsNullOrWhiteSpace(_draft.TicketArt.Reference) || !ScratchPackV1Loader.IsBuiltInTicketSupported(_draft.GameType, _draft.TicketArt.Reference))
                issues.Add("請選擇內建票面");
        }
        else if (!ValidTicketPackageAsset())
        {
            issues.Add("請選擇有效的 1080 × 882 PNG 票面圖片");
        }

        if (_draft.Foil.Source == "builtin")
        {
            if (string.IsNullOrWhiteSpace(_draft.Foil.Reference) || !ScratchPackV1Loader.IsBuiltInFoilSupported(_draft.Foil.Reference))
                issues.Add("請選擇內建銀膜");
        }
        else if (!ValidPackagePng(_draft.Foil.ExternalPath))
        {
            issues.Add("請選擇有效的 PNG 銀膜圖片");
        }
        return issues;
    }

    private List<string> ValidateV051GameArea()
    {
        var issues = new List<string>();
        if (_draft is null)
            return new List<string> { "編輯內容尚未初始化" };

        if (_draft.GridSize is not 3 and not 4 and not 5) issues.Add("網格尺寸必須為 3 × 3、4 × 4 或 5 × 5");
        if (_draft.ZoneWidth <= 0 || _draft.ZoneHeight <= 0) issues.Add("刮獎格尺寸必須大於 0");
        if (_draft.HorizontalGap < 0 || _draft.VerticalGap < 0) issues.Add("刮獎格間距不可為負數");
        if (_draft.ZoneX < 0 || _draft.ZoneY < 0) issues.Add("刮獎區起點不可為負數");
        var gridWidth = (long)_draft.ZoneWidth * _draft.GridSize + (long)_draft.HorizontalGap * (_draft.GridSize - 1);
        var gridHeight = (long)_draft.ZoneHeight * _draft.GridSize + (long)_draft.VerticalGap * (_draft.GridSize - 1);
        if (_draft.ZoneX + gridWidth > 1080 || _draft.ZoneY + gridHeight > 882) issues.Add("刮獎格區超出彩券範圍");
        if (_draft.ZoneShape is not "rectangle" and not "roundedRectangle" and not "circle" and not "ellipse") issues.Add("刮獎區形狀無效");
        if (_draft.ZoneShape == "roundedRectangle" && (_draft.CornerRadius < 0 || _draft.CornerRadius * 2 > Math.Min(_draft.ZoneWidth, _draft.ZoneHeight))) issues.Add("圓角數值無效");
        if (_draft.ZoneShape == "circle" && _draft.ZoneWidth != _draft.ZoneHeight) issues.Add("正圓刮獎格的寬與高必須相同");
        if (_draft.PriceDisplay && !RectInsideCanvas(_draft.PriceArea)) issues.Add("面額區超出彩券範圍或尺寸無效");
        if (!RectInsideCanvas(_draft.SerialArea)) issues.Add("票號區超出彩券範圍或尺寸無效");
        return issues;
    }

    private List<string> ValidateV051PrizePool()
    {
        var issues = new List<string>();
        if (_draft is null)
            return new List<string> { "編輯內容尚未初始化" };

        if (_draft.IssueSize <= 0 || _draft.TicketsPerBook <= 0 || _draft.IssueSize % _draft.TicketsPerBook != 0)
            issues.Add("發行張數與每本張數必須大於 0，且發行張數可被每本張數整除");
        if (_draft.Prizes.Count != 2 * _draft.GridSize + 1) issues.Add("獎金級距數量與目前網格尺寸不一致");
        if (_draft.Prizes.Any(p => p.Amount <= 0)) issues.Add("所有獎金必須大於 0");
        if (_draft.Prizes.Any(p => p.Count < 0)) issues.Add("中獎張數不可為負數");
        for (var i = 1; i < _draft.Prizes.Count; i++)
        {
            if (_draft.Prizes[i].Amount <= _draft.Prizes[i - 1].Amount)
            {
                issues.Add("獎金必須隨連線數增加而嚴格遞增");
                break;
            }
        }
        if (_draft.Prizes.Sum(p => p.Count) > _draft.IssueSize) issues.Add("中獎張數總和不可超過發行張數");
        return issues;
    }

    private void ShowStepIssues(int step, IReadOnlyList<string> issues)
    {
        var title = step switch { 1 => "基本資料與票面素材", 2 => "遊戲區域", 3 => "獎金池", _ => "檢查" };
        MessageBox.Show(this, $"請先完成「{title}」：\n\n" + string.Join("\n", issues.Select(issue => "• " + issue)), "尚未完成", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void V051ValidateDraft_OnClick(object sender, RoutedEventArgs e)
        => RunV051FullValidation();

    private bool RunV051FullValidation()
    {
        if (_draft is null)
            return false;

        SyncV051BasicAndResources();
        SyncLayoutFieldsToDraft();
        PrizeDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        PrizeDataGrid.CommitEdit(DataGridEditingUnit.Row, true);
        SyncPrizeHeaderToDraft();

        var issues = ValidateV051BasicAndAssets()
            .Concat(ValidateV051GameArea())
            .Concat(ValidateV051PrizePool())
            .ToList();
        if (issues.Count > 0)
        {
            _lastFullValidationPassed = false;
            CompleteExportButton.IsEnabled = false;
            ValidationResultText.Text = "✕ 驗證尚未通過：\n" + string.Join("\n", issues.Select(issue => "• " + issue));
            return false;
        }

        var tempPack = Path.Combine(Path.GetTempPath(), $"packeditor-validate-{Guid.NewGuid():N}.scratchpack");
        try
        {
            var loaded = PackExportService.ExportAndRoundTripValidate(_draft, tempPack);
            _lastFullValidationPassed = true;
            CompleteExportButton.IsEnabled = true;
            ValidationResultText.Text =
                "✓ 基本資料與票面素材\n" +
                "✓ 遊戲區域\n" +
                "✓ 獎金池\n" +
                "✓ ScratchPack Round-trip\n\n" +
                $"Package ID：{loaded.Manifest.PackageId:D}\n" +
                $"Content SHA-256：{loaded.ContentHash}";
            return true;
        }
        catch (Exception ex)
        {
            _lastFullValidationPassed = false;
            CompleteExportButton.IsEnabled = false;
            ValidationResultText.Text = $"✕ ScratchPack 完整驗證失敗：\n{ex.Message}";
            return false;
        }
        finally
        {
            try { if (File.Exists(tempPack)) File.Delete(tempPack); } catch { }
        }
    }

    private void V051CompleteAndExport_OnClick(object sender, RoutedEventArgs e)
    {
        if (_draft is null || !RunV051FullValidation())
            return;

        var dialog = new SaveFileDialog
        {
            Title = "完成並輸出 ScratchPack",
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
            ValidationResultText.Text =
                "✓ 完成並輸出\n" +
                $"{dialog.FileName}\n\n" +
                $"Package ID：{loaded.Manifest.PackageId:D}\n" +
                $"Content SHA-256：{loaded.ContentHash}";
            _lastFullValidationPassed = true;
            CompleteExportButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            _lastFullValidationPassed = false;
            CompleteExportButton.IsEnabled = false;
            ValidationResultText.Text = $"✕ 輸出失敗：\n{ex.Message}";
            MessageBox.Show(this, ex.Message, "ScratchPack 輸出失敗", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void MarkValidationDirty()
    {
        _lastFullValidationPassed = false;
        if (CompleteExportButton is not null)
            CompleteExportButton.IsEnabled = false;
        if (_currentWorkflowStep == 4 && ValidationResultText is not null)
            ValidationResultText.Text = "內容已變更，請重新驗證。";
    }

    private void RefreshV051Preview()
    {
        RefreshPreview();
        if (TicketPreviewImage.Visibility != Visibility.Visible)
            PreviewPlaceholderText.Text = "設定彩券內容後顯示";
        RefreshInteractiveOverlay();
    }

    private void RefreshInteractiveOverlay()
    {
        EditorOverlayCanvas.Children.Clear();
        if (_draft is null || TicketPreviewImage.Visibility != Visibility.Visible)
            return;

        foreach (var zone in _draft.BuildZones())
            AddPassiveOverlay(new Rect(zone.X, zone.Y, zone.Width, zone.Height), ZoneBrush, zone.CornerRadius ?? 0);

        var gridWidth = _draft.ZoneWidth * _draft.GridSize + _draft.HorizontalGap * (_draft.GridSize - 1);
        var gridHeight = _draft.ZoneHeight * _draft.GridSize + _draft.VerticalGap * (_draft.GridSize - 1);
        AddInteractiveOverlay(new Rect(_draft.ZoneX, _draft.ZoneY, gridWidth, gridHeight), ZoneBrush, "刮獎格區", OverlayTarget.Grid, 8);

        if (_draft.PriceDisplay)
            AddInteractiveOverlay(new Rect(_draft.PriceArea.X, _draft.PriceArea.Y, _draft.PriceArea.Width, _draft.PriceArea.Height), PriceBrush, $"面額 ${_draft.Price:N0}", OverlayTarget.Price, 8);
        AddInteractiveOverlay(new Rect(_draft.SerialArea.X, _draft.SerialArea.Y, _draft.SerialArea.Width, _draft.SerialArea.Height), SerialBrush, "票號", OverlayTarget.Serial, 8);
    }

    private void AddPassiveOverlay(Rect rect, Brush brush, double cornerRadius)
    {
        var color = ((SolidColorBrush)brush).Color;
        var border = new Border
        {
            Width = rect.Width,
            Height = rect.Height,
            BorderBrush = brush,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(Math.Max(0, cornerRadius)),
            Background = new SolidColorBrush(Color.FromArgb(20, color.R, color.G, color.B)),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(border, rect.X);
        Canvas.SetTop(border, rect.Y);
        EditorOverlayCanvas.Children.Add(border);
    }

    private void AddInteractiveOverlay(Rect rect, Brush brush, string label, OverlayTarget target, double cornerRadius)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
            return;
        var color = ((SolidColorBrush)brush).Color;
        var layout = new Grid();
        var labelBlock = new TextBlock
        {
            Text = label,
            Foreground = brush,
            Background = new SolidColorBrush(Color.FromArgb(210, 35, 18, 14)),
            FontFamily = new FontFamily("Microsoft JhengHei UI"),
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Padding = new Thickness(5, 2, 5, 2),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            IsHitTestVisible = false
        };
        layout.Children.Add(labelBlock);

        var handle = new Thumb
        {
            Width = 18,
            Height = 18,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, -1, -1),
            Background = brush,
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(1),
            Cursor = Cursors.SizeNWSE,
            Tag = target
        };
        handle.DragDelta += V051ResizeHandle_OnDragDelta;
        handle.DragCompleted += V051ResizeHandle_OnDragCompleted;
        layout.Children.Add(handle);

        var border = new Border
        {
            Width = rect.Width,
            Height = rect.Height,
            BorderBrush = brush,
            BorderThickness = new Thickness(3),
            CornerRadius = new CornerRadius(Math.Max(0, cornerRadius)),
            Background = new SolidColorBrush(Color.FromArgb(28, color.R, color.G, color.B)),
            Child = layout,
            Tag = target,
            Cursor = Cursors.SizeAll
        };
        border.MouseLeftButtonDown += V051OverlayBorder_OnMouseLeftButtonDown;
        Canvas.SetLeft(border, rect.X);
        Canvas.SetTop(border, rect.Y);
        EditorOverlayCanvas.Children.Add(border);
    }

    private void V051OverlayBorder_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_draft is null || sender is not Border border || border.Tag is not OverlayTarget target)
            return;
        if (FindParent<Thumb>(e.OriginalSource as DependencyObject) is not null)
            return;

        _dragTarget = target;
        _dragStartPoint = e.GetPosition(EditorOverlayCanvas);
        switch (target)
        {
            case OverlayTarget.Grid:
                _dragStartX = _draft.ZoneX; _dragStartY = _draft.ZoneY;
                _dragStartWidth = _draft.ZoneWidth * _draft.GridSize + _draft.HorizontalGap * (_draft.GridSize - 1);
                _dragStartHeight = _draft.ZoneHeight * _draft.GridSize + _draft.VerticalGap * (_draft.GridSize - 1);
                break;
            case OverlayTarget.Price:
                _dragStartX = _draft.PriceArea.X; _dragStartY = _draft.PriceArea.Y; _dragStartWidth = _draft.PriceArea.Width; _dragStartHeight = _draft.PriceArea.Height;
                break;
            case OverlayTarget.Serial:
                _dragStartX = _draft.SerialArea.X; _dragStartY = _draft.SerialArea.Y; _dragStartWidth = _draft.SerialArea.Width; _dragStartHeight = _draft.SerialArea.Height;
                break;
        }
        EditorOverlayCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void V051EditorOverlayCanvas_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_draft is null || _dragTarget == OverlayTarget.None || e.LeftButton != MouseButtonState.Pressed)
            return;

        var point = e.GetPosition(EditorOverlayCanvas);
        var newX = (int)Math.Round(_dragStartX + point.X - _dragStartPoint.X);
        var newY = (int)Math.Round(_dragStartY + point.Y - _dragStartPoint.Y);
        newX = Math.Clamp(newX, 0, Math.Max(0, 1080 - _dragStartWidth));
        newY = Math.Clamp(newY, 0, Math.Max(0, 882 - _dragStartHeight));

        switch (_dragTarget)
        {
            case OverlayTarget.Grid: _draft.ZoneX = newX; _draft.ZoneY = newY; break;
            case OverlayTarget.Price: _draft.PriceArea.X = newX; _draft.PriceArea.Y = newY; break;
            case OverlayTarget.Serial: _draft.SerialArea.X = newX; _draft.SerialArea.Y = newY; break;
        }
        UpdateV051LayoutControls();
        RefreshInteractiveOverlay();
        MarkValidationDirty();
        e.Handled = true;
    }

    private void V051EditorOverlayCanvas_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragTarget == OverlayTarget.None)
            return;
        _dragTarget = OverlayTarget.None;
        EditorOverlayCanvas.ReleaseMouseCapture();
        RefreshInteractiveOverlay();
        e.Handled = true;
    }

    private void V051ResizeHandle_OnDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (_draft is null || sender is not Thumb thumb || thumb.Tag is not OverlayTarget target)
            return;

        switch (target)
        {
            case OverlayTarget.Grid:
            {
                var maxCellWidth = Math.Max(20, (1080 - _draft.ZoneX - _draft.HorizontalGap * (_draft.GridSize - 1)) / _draft.GridSize);
                var maxCellHeight = Math.Max(20, (882 - _draft.ZoneY - _draft.VerticalGap * (_draft.GridSize - 1)) / _draft.GridSize);
                _draft.ZoneWidth = Math.Clamp(_draft.ZoneWidth + (int)Math.Round(e.HorizontalChange / _draft.GridSize), 20, maxCellWidth);
                _draft.ZoneHeight = Math.Clamp(_draft.ZoneHeight + (int)Math.Round(e.VerticalChange / _draft.GridSize), 20, maxCellHeight);
                break;
            }
            case OverlayTarget.Price:
                _draft.PriceArea.Width = Math.Clamp(_draft.PriceArea.Width + (int)Math.Round(e.HorizontalChange), 30, 1080 - _draft.PriceArea.X);
                _draft.PriceArea.Height = Math.Clamp(_draft.PriceArea.Height + (int)Math.Round(e.VerticalChange), 20, 882 - _draft.PriceArea.Y);
                break;
            case OverlayTarget.Serial:
                _draft.SerialArea.Width = Math.Clamp(_draft.SerialArea.Width + (int)Math.Round(e.HorizontalChange), 30, 1080 - _draft.SerialArea.X);
                _draft.SerialArea.Height = Math.Clamp(_draft.SerialArea.Height + (int)Math.Round(e.VerticalChange), 20, 882 - _draft.SerialArea.Y);
                break;
        }

        if (FindParent<Border>(thumb) is Border border)
        {
            if (target == OverlayTarget.Grid)
            {
                border.Width = _draft.ZoneWidth * _draft.GridSize + _draft.HorizontalGap * (_draft.GridSize - 1);
                border.Height = _draft.ZoneHeight * _draft.GridSize + _draft.VerticalGap * (_draft.GridSize - 1);
            }
            else if (target == OverlayTarget.Price)
            {
                border.Width = _draft.PriceArea.Width; border.Height = _draft.PriceArea.Height;
            }
            else
            {
                border.Width = _draft.SerialArea.Width; border.Height = _draft.SerialArea.Height;
            }
        }
        UpdateV051LayoutControls();
        MarkValidationDirty();
    }

    private void V051ResizeHandle_OnDragCompleted(object sender, DragCompletedEventArgs e)
        => RefreshInteractiveOverlay();

    private void UpdateV051LayoutControls()
    {
        if (_draft is null)
            return;
        _loadingUi = true;
        try
        {
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
        }
        finally { _loadingUi = false; }
    }

    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T match)
                return match;
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }
}
