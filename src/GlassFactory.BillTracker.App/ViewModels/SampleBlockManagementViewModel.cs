using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using GlassFactory.BillTracker.App.Commands;
using GlassFactory.BillTracker.App.ViewModels.Base;
using GlassFactory.BillTracker.App.ViewModels.Rows;
using GlassFactory.BillTracker.Data.Services;
using GlassFactory.BillTracker.Domain.Entities;

namespace GlassFactory.BillTracker.App.ViewModels;

public sealed class SampleBlockManagementViewModel : ObservableObject
{
    private readonly ISampleBlockService _service;

    private Guid _editingId;
    private string _model = string.Empty;
    private string? _customer;
    private DateTime? _orderTime;
    private string? _note;
    private SampleBlock? _selected;

    private string? _filterModel;
    private string? _filterCustomer;
    private DateTime? _filterFrom;
    private DateTime? _filterTo;
    private string? _filterNote;

    private readonly List<string> _newAttachmentPaths = new();
    private readonly List<Guid> _removedAttachmentIds = new();

    public ObservableCollection<SampleBlock> SampleBlocks { get; } = new();
    public ObservableCollection<ManagedAttachmentViewModel> Attachments { get; } = new();

    public SampleBlockManagementViewModel(ISampleBlockService service)
    {
        _service = service;
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
    public string? FilterCustomer { get => _filterCustomer; set => SetProperty(ref _filterCustomer, value); }
    public DateTime? FilterFrom { get => _filterFrom; set => SetProperty(ref _filterFrom, value); }
    public DateTime? FilterTo { get => _filterTo; set => SetProperty(ref _filterTo, value); }
    public string? FilterNote { get => _filterNote; set => SetProperty(ref _filterNote, value); }

    public SampleBlock? Selected
    {
        get => _selected;
        set { if (SetProperty(ref _selected, value) && value is not null) LoadForm(value); }
    }

    public string Model { get => _model; set => SetProperty(ref _model, value); }
    public string? Customer { get => _customer; set => SetProperty(ref _customer, value); }
    public DateTime? OrderTime { get => _orderTime; set => SetProperty(ref _orderTime, value); }
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
            var filter = new SampleBlockFilter
            {
                Model = FilterModel, Customer = FilterCustomer,
                OrderFrom = FilterFrom?.Date, OrderTo = FilterTo?.Date, Note = FilterNote
            };
            var items = await _service.GetSampleBlocksAsync(filter);
            SampleBlocks.Clear();
            foreach (var s in items) SampleBlocks.Add(s);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载样块列表失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClearFilter()
    {
        FilterModel = null; FilterCustomer = null; FilterFrom = null; FilterTo = null; FilterNote = null;
        _ = LoadAsync();
    }

    private void LoadForm(SampleBlock sb)
    {
        _editingId = sb.Id;
        Model = sb.Model; Customer = sb.Customer; OrderTime = sb.OrderTime; Note = sb.Note;
        _newAttachmentPaths.Clear(); _removedAttachmentIds.Clear(); Attachments.Clear();
        foreach (var a in sb.Attachments)
            Attachments.Add(new ManagedAttachmentViewModel { AttachmentId = a.Id, RelativePath = a.RelativePath });
    }

    private void ResetForm()
    {
        _editingId = Guid.Empty;
        Model = string.Empty; Customer = null; OrderTime = null; Note = null;
        Selected = null;
        _newAttachmentPaths.Clear(); _removedAttachmentIds.Clear(); Attachments.Clear();
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
        try
        {
            var saved = await _service.SaveAsync(new SampleBlock
            {
                Id = _editingId, Model = Model, Customer = Customer, OrderTime = OrderTime?.Date, Note = Note
            });
            foreach (var rid in _removedAttachmentIds) await _service.RemoveAttachmentAsync(rid);
            foreach (var p in _newAttachmentPaths) await _service.AddAttachmentAsync(saved.Id, p);
            await LoadAsync();
            Selected = SampleBlocks.FirstOrDefault(x => x.Id == saved.Id);
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
            MessageBox.Show("请先在列表中选择要删除的样块。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (MessageBox.Show($"确认删除样块 \"{Model}\" 吗？", "删除确认", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try
        {
            await _service.DeleteAsync(_editingId);
            ResetForm();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "删除失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
