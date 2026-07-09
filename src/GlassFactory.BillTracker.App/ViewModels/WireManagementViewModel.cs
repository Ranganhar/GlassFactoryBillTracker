using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using GlassFactory.BillTracker.App.Commands;
using GlassFactory.BillTracker.App.ViewModels.Base;
using GlassFactory.BillTracker.App.ViewModels.Rows;
using GlassFactory.BillTracker.Data.Services;
using GlassFactory.BillTracker.Domain.Entities;

namespace GlassFactory.BillTracker.App.ViewModels;

public sealed class WireManagementViewModel : ObservableObject
{
    private readonly IWireService _wireService;

    private Guid _editingId;
    private string _model = string.Empty;
    private string? _priceText;
    private DateTime? _purchaseDate;
    private string? _note;
    private Wire? _selectedWire;

    private string? _filterModel;
    private string? _filterPriceMin;
    private string? _filterPriceMax;
    private DateTime? _filterFrom;
    private DateTime? _filterTo;
    private string? _filterNote;

    private readonly List<string> _newAttachmentPaths = new();
    private readonly List<Guid> _removedAttachmentIds = new();

    public ObservableCollection<Wire> Wires { get; } = new();
    public ObservableCollection<ManagedAttachmentViewModel> Attachments { get; } = new();

    public WireManagementViewModel(IWireService wireService)
    {
        _wireService = wireService;
        NewCommand = new RelayCommand(ResetForm);
        SaveCommand = new RelayCommand(() => _ = SaveAsync());
        DeleteCommand = new RelayCommand(() => _ = DeleteAsync());
        SearchCommand = new RelayCommand(() => _ = LoadAsync());
        ClearFilterCommand = new RelayCommand(ClearFilter);
        AddImageCommand = new RelayCommand(AddImages);
        RemoveImageCommand = new RelayCommand<ManagedAttachmentViewModel>(RemoveImage);
        _ = LoadAsync();
    }

    public string? FilterModel { get => _filterModel; set => SetProperty(ref _filterModel, value); }
    public string? FilterPriceMin { get => _filterPriceMin; set => SetProperty(ref _filterPriceMin, value); }
    public string? FilterPriceMax { get => _filterPriceMax; set => SetProperty(ref _filterPriceMax, value); }
    public DateTime? FilterFrom { get => _filterFrom; set => SetProperty(ref _filterFrom, value); }
    public DateTime? FilterTo { get => _filterTo; set => SetProperty(ref _filterTo, value); }
    public string? FilterNote { get => _filterNote; set => SetProperty(ref _filterNote, value); }

    public Wire? SelectedWire
    {
        get => _selectedWire;
        set { if (SetProperty(ref _selectedWire, value) && value is not null) LoadForm(value); }
    }

    public string Model { get => _model; set => SetProperty(ref _model, value); }
    public string? PriceText { get => _priceText; set => SetProperty(ref _priceText, value); }
    public DateTime? PurchaseDate { get => _purchaseDate; set => SetProperty(ref _purchaseDate, value); }
    public string? Note { get => _note; set => SetProperty(ref _note, value); }

    public RelayCommand NewCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand SearchCommand { get; }
    public RelayCommand ClearFilterCommand { get; }
    public RelayCommand AddImageCommand { get; }
    public RelayCommand<ManagedAttachmentViewModel> RemoveImageCommand { get; }

    public async Task LoadAsync()
    {
        try
        {
            var filter = new WireFilter
            {
                Model = FilterModel,
                PriceMin = ParseDecimal(FilterPriceMin),
                PriceMax = ParseDecimal(FilterPriceMax),
                PurchaseFrom = FilterFrom?.Date,
                PurchaseTo = FilterTo?.Date,
                Note = FilterNote
            };
            var items = await _wireService.GetWiresAsync(filter);
            Wires.Clear();
            foreach (var w in items) Wires.Add(w);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载丝列表失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClearFilter()
    {
        FilterModel = null; FilterPriceMin = null; FilterPriceMax = null;
        FilterFrom = null; FilterTo = null; FilterNote = null;
        _ = LoadAsync();
    }

    private void LoadForm(Wire wire)
    {
        _editingId = wire.Id;
        Model = wire.Model;
        PriceText = wire.Price.ToString(CultureInfo.InvariantCulture);
        PurchaseDate = wire.PurchaseDate;
        Note = wire.Note;
        _newAttachmentPaths.Clear();
        _removedAttachmentIds.Clear();
        Attachments.Clear();
        foreach (var a in wire.Attachments)
            Attachments.Add(new ManagedAttachmentViewModel { AttachmentId = a.Id, RelativePath = a.RelativePath });
    }

    private void ResetForm()
    {
        _editingId = Guid.Empty;
        Model = string.Empty; PriceText = "0"; PurchaseDate = null; Note = null;
        SelectedWire = null;
        _newAttachmentPaths.Clear();
        _removedAttachmentIds.Clear();
        Attachments.Clear();
    }

    private void AddImages()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp|所有文件|*.*",
            Multiselect = true
        };
        if (dialog.ShowDialog() != true) return;
        foreach (var path in dialog.FileNames)
        {
            if (!File.Exists(path)) continue;
            _newAttachmentPaths.Add(path);
            Attachments.Add(new ManagedAttachmentViewModel { SourcePath = path });
        }
    }

    private void RemoveImage(ManagedAttachmentViewModel? att)
    {
        if (att is null) return;
        if (att.IsPersisted && att.AttachmentId.HasValue) _removedAttachmentIds.Add(att.AttachmentId.Value);
        else if (!string.IsNullOrWhiteSpace(att.SourcePath)) _newAttachmentPaths.Remove(att.SourcePath);
        Attachments.Remove(att);
    }

    private async Task SaveAsync()
    {
        var price = ParseDecimal(PriceText);
        if (price is null || price < 0)
        {
            MessageBox.Show("价格必须为非负数字。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            var saved = await _wireService.SaveAsync(new Wire
            {
                Id = _editingId, Model = Model, Price = price.Value, PurchaseDate = PurchaseDate?.Date, Note = Note
            });
            foreach (var rid in _removedAttachmentIds) await _wireService.RemoveAttachmentAsync(rid);
            foreach (var p in _newAttachmentPaths) await _wireService.AddAttachmentAsync(saved.Id, p);
            await LoadAsync();
            SelectedWire = Wires.FirstOrDefault(x => x.Id == saved.Id);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task DeleteAsync()
    {
        if (_editingId == Guid.Empty)
        {
            MessageBox.Show("请先在列表中选择要删除的丝。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (MessageBox.Show($"确认删除丝 \"{Model}\" 吗？", "删除确认", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try
        {
            await _wireService.DeleteAsync(_editingId);
            ResetForm();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "删除失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static decimal? ParseDecimal(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        return decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
