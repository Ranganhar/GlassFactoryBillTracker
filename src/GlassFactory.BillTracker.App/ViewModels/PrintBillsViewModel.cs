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
    private FixedDocument _previewDocument = new();

    public PrintBillsViewModel(IReadOnlyList<OrderExportDto> orders, IPrintService printService)
    {
        _orders = orders ?? Array.Empty<OrderExportDto>();
        _printService = printService;

        Orders = new ObservableCollection<OrderExportDto>(_orders);
        FontSizes = new ReadOnlyCollection<int>(new[] { 10, 12, 14, 16, 18 });

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
    }

    public ObservableCollection<OrderExportDto> Orders { get; }

    public IReadOnlyList<int> FontSizes { get; }

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
                // Force a full rerender so column widths are recalculated for the new font size.
                RequestPreviewRefresh(forceRegenerate: true);
            }
        }
    }

    public FixedDocument PreviewDocument
    {
        get => _previewDocument;
        private set => SetProperty(ref _previewDocument, value);
    }

    public bool IsDotMatrixTemplate => SelectedTemplate == PrintTemplateKind.DotMatrix;

    public PrintBillOptions BuildOptions()
    {
        return new PrintBillOptions
        {
            HeaderText = string.IsNullOrWhiteSpace(HeaderText) ? "亿达夹丝玻璃" : HeaderText.Trim(),
            UseCustomerPhone = UseCustomerPhone,
            CustomPhone = string.IsNullOrWhiteSpace(CustomPhone) ? null : CustomPhone.Trim(),
            TemplateKind = SelectedTemplate,
            DotMatrixHeightMode = DotMatrixHeightMode.Third,
            FontSize = FontSize
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
            DotMatrixHeightMode.Third.ToString(),
            options.FontSize.ToString("F0"),
            PrintFontFamilyName,
            options.HeaderText ?? string.Empty,
            options.UseCustomerPhone ? "1" : "0",
            options.CustomPhone ?? string.Empty
        });
    }
}
