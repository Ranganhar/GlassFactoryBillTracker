using System.Collections.ObjectModel;
using System.Windows.Documents;
using System.Windows.Threading;
using GlassFactory.BillTracker.App.Models;
using GlassFactory.BillTracker.App.Services;
using GlassFactory.BillTracker.App.ViewModels.Base;

namespace GlassFactory.BillTracker.App.ViewModels;

public sealed class PrintBillsViewModel : ObservableObject
{
    private const string PrintFontFamilyName = "Microsoft YaHei";

    private readonly IReadOnlyList<OrderExportDto> _orders;
    private readonly IPrintService _printService;
    private readonly DispatcherTimer _previewDebounceTimer;
    private readonly Dictionary<string, FixedDocument> _previewCache = new(StringComparer.Ordinal);
    private bool _forceRegeneratePreview;

    private string _headerText = "亿达夹丝玻璃";
    private bool _useCustomerPhone = true;
    private string? _customPhone;
    private PrintTemplateKind _selectedTemplate = PrintTemplateKind.DotMatrix;
    private DotMatrixHeightMode _selectedPaperMode = DotMatrixHeightMode.Third;
    private int _fontSize = 12;
    private bool _fitToPageScale = true;
    private int _manualScalePercent = 100;
    private string _currentScaleDisplay = "缩放比例: 100% (默认打印机)";
    private FixedDocument _previewDocument = new();

    private string? _printerName;
    private bool _printerImageableAreaKnown;
    private bool _printerImageableAreaFromCapabilities;
    private double _printerImageableOriginXDip;
    private double _printerImageableOriginYDip;
    private double _printerImageableWidthDip;
    private double _printerImageableHeightDip;

    public PrintBillsViewModel(IReadOnlyList<OrderExportDto> orders, IPrintService printService)
    {
        _orders = orders ?? Array.Empty<OrderExportDto>();
        _printService = printService;

        Orders = new ObservableCollection<OrderExportDto>(_orders);
        FontSizes = new ReadOnlyCollection<int>(new[] { 10, 12, 14, 16, 18 });
        ScalePercentOptions = new ReadOnlyCollection<int>(new[] { 90, 95, 100, 105, 110 });

        _previewDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _previewDebounceTimer.Tick += (_, _) =>
        {
            _previewDebounceTimer.Stop();
            RegeneratePreview(_forceRegeneratePreview);
            _forceRegeneratePreview = false;
        };

        RegeneratePreview(force: true);
        UpdateScaleDisplay();
    }

    public ObservableCollection<OrderExportDto> Orders { get; }

    public IReadOnlyList<int> FontSizes { get; }

    public IReadOnlyList<int> ScalePercentOptions { get; }

    public string HeaderText
    {
        get => _headerText;
        set
        {
            if (SetProperty(ref _headerText, value))
            {
                RequestPreviewRefresh();
            }
        }
    }

    public bool UseCustomerPhone
    {
        get => _useCustomerPhone;
        set
        {
            if (SetProperty(ref _useCustomerPhone, value))
            {
                RequestPreviewRefresh();
            }
        }
    }

    public string? CustomPhone
    {
        get => _customPhone;
        set
        {
            if (SetProperty(ref _customPhone, value))
            {
                RequestPreviewRefresh();
            }
        }
    }

    public PrintTemplateKind SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (SetProperty(ref _selectedTemplate, value))
            {
                OnPropertyChanged(nameof(IsDotMatrixTemplate));
                RequestPreviewRefresh();
            }
        }
    }

    public DotMatrixHeightMode SelectedPaperMode
    {
        get => _selectedPaperMode;
        set
        {
            if (SetProperty(ref _selectedPaperMode, value))
            {
                RequestPreviewRefresh();
            }
        }
    }

    public int FontSize
    {
        get => _fontSize;
        set
        {
            if (SetProperty(ref _fontSize, value))
            {
                _previewCache.Clear();
                RegeneratePreview(force: true);
            }
        }
    }

    public bool FitToPageScale
    {
        get => _fitToPageScale;
        set
        {
            if (SetProperty(ref _fitToPageScale, value))
            {
                OnPropertyChanged(nameof(IsManualScaleEnabled));
                UpdateScaleDisplay();
                RequestPreviewRefresh(forceRegenerate: true);
            }
        }
    }

    public int ManualScalePercent
    {
        get => _manualScalePercent;
        set
        {
            var normalized = Math.Clamp(value, 50, 150);
            if (SetProperty(ref _manualScalePercent, normalized))
            {
                UpdateScaleDisplay();
                RequestPreviewRefresh(forceRegenerate: true);
            }
        }
    }

    public bool IsManualScaleEnabled => !FitToPageScale;

    public string CurrentScaleDisplay
    {
        get => _currentScaleDisplay;
        private set => SetProperty(ref _currentScaleDisplay, value);
    }

    public FixedDocument PreviewDocument
    {
        get => _previewDocument;
        private set => SetProperty(ref _previewDocument, value);
    }

    public bool IsDotMatrixTemplate => SelectedTemplate == PrintTemplateKind.DotMatrix;

    public void SetPrinterImageableArea(
        string? printerName,
        bool fromCapabilities,
        double originX,
        double originY,
        double width,
        double height)
    {
        _printerName = printerName;
        _printerImageableAreaKnown = width > 0d && height > 0d;
        _printerImageableAreaFromCapabilities = fromCapabilities;
        _printerImageableOriginXDip = Math.Max(0d, originX);
        _printerImageableOriginYDip = Math.Max(0d, originY);
        _printerImageableWidthDip = Math.Max(0d, width);
        _printerImageableHeightDip = Math.Max(0d, height);

        _previewCache.Clear();
        UpdateScaleDisplay();
        RequestPreviewRefresh(forceRegenerate: true);
    }

    public void ClearPrinterImageableArea()
    {
        _printerName = null;
        _printerImageableAreaKnown = false;
        _printerImageableAreaFromCapabilities = false;
        _printerImageableOriginXDip = 0d;
        _printerImageableOriginYDip = 0d;
        _printerImageableWidthDip = 0d;
        _printerImageableHeightDip = 0d;

        _previewCache.Clear();
        UpdateScaleDisplay();
        RequestPreviewRefresh(forceRegenerate: true);
    }

    public PrintBillOptions BuildOptions()
    {
        return new PrintBillOptions
        {
            HeaderText = string.IsNullOrWhiteSpace(HeaderText) ? "亿达夹丝玻璃" : HeaderText.Trim(),
            UseCustomerPhone = UseCustomerPhone,
            CustomPhone = string.IsNullOrWhiteSpace(CustomPhone) ? null : CustomPhone.Trim(),
            TemplateKind = SelectedTemplate,
            DotMatrixHeightMode = SelectedPaperMode,
            FontSize = FontSize,
            FitToPageScale = FitToPageScale,
            ManualScalePercent = ManualScalePercent,
            PrinterName = _printerName,
            PrinterImageableAreaKnown = _printerImageableAreaKnown,
            PrinterImageableAreaFromCapabilities = _printerImageableAreaFromCapabilities,
            PrinterImageableOriginXDip = _printerImageableOriginXDip,
            PrinterImageableOriginYDip = _printerImageableOriginYDip,
            PrinterImageableWidthDip = _printerImageableWidthDip,
            PrinterImageableHeightDip = _printerImageableHeightDip
        };
    }

    public void RequestPreviewRefresh(bool forceRegenerate = false)
    {
        _forceRegeneratePreview = _forceRegeneratePreview || forceRegenerate;
        _previewDebounceTimer.Stop();
        _previewDebounceTimer.Start();
    }

    public void RefreshPreview()
    {
        RegeneratePreview(force: false);
    }

    public Task RegeneratePreviewAsync()
    {
        _previewDebounceTimer.Stop();
        RegeneratePreview(force: true);
        return Task.CompletedTask;
    }

    public void RegeneratePreview(bool force)
    {
        if (_orders.Count == 0)
        {
            PreviewDocument = new FixedDocument();
            return;
        }

        var options = BuildOptions();
        var cacheKey = BuildCacheKey(options);
        if (force || !_previewCache.TryGetValue(cacheKey, out var document))
        {
            document = options.TemplateKind == PrintTemplateKind.DotMatrix
                ? _printService.RenderDotMatrixTriplicate(_orders, options)
                : _printService.RenderA4(_orders, options);
            _previewCache[cacheKey] = document;
        }

        PreviewDocument = document;
    }

    private static string BuildCacheKey(PrintBillOptions options)
    {
        return string.Join("|", new[]
        {
            options.TemplateKind.ToString(),
            options.DotMatrixHeightMode.ToString(),
            options.FontSize.ToString("F0"),
            options.FitToPageScale ? "fit" : "manual",
            options.ManualScalePercent.ToString(),
            PrintFontFamilyName,
            options.HeaderText ?? string.Empty,
            options.UseCustomerPhone ? "1" : "0",
            options.CustomPhone ?? string.Empty,
            options.PrinterName ?? string.Empty,
            options.PrinterImageableAreaKnown ? "1" : "0",
            options.PrinterImageableAreaFromCapabilities ? "1" : "0",
            options.PrinterImageableOriginXDip.ToString("F2"),
            options.PrinterImageableOriginYDip.ToString("F2"),
            options.PrinterImageableWidthDip.ToString("F2"),
            options.PrinterImageableHeightDip.ToString("F2")
        });
    }

    private void UpdateScaleDisplay()
    {
        var result = PrintScaleCalculator.Compute(BuildOptions());
        var percent = (int)Math.Round(result.Scale * 100d, MidpointRounding.AwayFromZero);
        if (!FitToPageScale)
        {
            CurrentScaleDisplay = $"缩放比例: {percent}% (手动)";
            return;
        }

        if (result.IsFromPrinter)
        {
            CurrentScaleDisplay = $"缩放比例: {percent}% (基于打印机)";
            return;
        }

        CurrentScaleDisplay = "缩放比例: 100% (默认打印机)";
    }
}
