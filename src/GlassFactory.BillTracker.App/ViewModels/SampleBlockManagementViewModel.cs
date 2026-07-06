using System.Collections.ObjectModel;
using System.Windows;
using GlassFactory.BillTracker.App.Commands;
using GlassFactory.BillTracker.App.ViewModels.Base;
using GlassFactory.BillTracker.Data.Services;
using GlassFactory.BillTracker.Domain.Entities;

namespace GlassFactory.BillTracker.App.ViewModels;

public sealed class SampleBlockManagementViewModel : ObservableObject
{
    private readonly ISampleBlockService _sampleBlockService;
    private readonly IWireService _wireService;

    private Guid _editingId;
    private string _model = string.Empty;
    private Wire? _selectedWire;
    private decimal _price;
    private string? _note;
    private string? _searchKeyword;
    private SampleBlock? _selectedSampleBlock;

    public ObservableCollection<SampleBlock> SampleBlocks { get; } = new();
    public ObservableCollection<Wire> Wires { get; } = new();

    public SampleBlockManagementViewModel(ISampleBlockService sampleBlockService, IWireService wireService)
    {
        _sampleBlockService = sampleBlockService;
        _wireService = wireService;

        NewCommand = new RelayCommand(ResetForm);
        SaveCommand = new RelayCommand(() => _ = SaveAsync());
        DeleteCommand = new RelayCommand(() => _ = DeleteAsync());
        SearchCommand = new RelayCommand(() => _ = LoadSampleBlocksAsync());

        _ = InitAsync();
    }

    public string? SearchKeyword
    {
        get => _searchKeyword;
        set => SetProperty(ref _searchKeyword, value);
    }

    public SampleBlock? SelectedSampleBlock
    {
        get => _selectedSampleBlock;
        set
        {
            if (SetProperty(ref _selectedSampleBlock, value) && value is not null)
            {
                LoadForm(value);
            }
        }
    }

    public string Model
    {
        get => _model;
        set => SetProperty(ref _model, value);
    }

    public Wire? SelectedWire
    {
        get => _selectedWire;
        set => SetProperty(ref _selectedWire, value);
    }

    public decimal Price
    {
        get => _price;
        set => SetProperty(ref _price, value);
    }

    public string? Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
    }

    public RelayCommand NewCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand SearchCommand { get; }

    private async Task InitAsync()
    {
        await LoadWiresAsync();
        await LoadSampleBlocksAsync();
    }

    private async Task LoadWiresAsync()
    {
        try
        {
            var wires = await _wireService.GetWiresAsync();
            Wires.Clear();
            foreach (var w in wires)
            {
                Wires.Add(w);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载丝列表失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public async Task LoadSampleBlocksAsync()
    {
        try
        {
            var items = await _sampleBlockService.GetSampleBlocksAsync(SearchKeyword);
            SampleBlocks.Clear();
            foreach (var sb in items)
            {
                SampleBlocks.Add(sb);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载样块列表失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadForm(SampleBlock sb)
    {
        _editingId = sb.Id;
        Model = sb.Model;
        SelectedWire = Wires.FirstOrDefault(x => x.Id == sb.WireId);
        Price = sb.Price;
        Note = sb.Note;
    }

    private void ResetForm()
    {
        _editingId = Guid.Empty;
        Model = string.Empty;
        SelectedWire = null;
        Price = 0m;
        Note = null;
        SelectedSampleBlock = null;
    }

    private async Task SaveAsync()
    {
        if (SelectedWire is null)
        {
            MessageBox.Show("请为样块选择丝。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var saved = await _sampleBlockService.SaveAsync(new SampleBlock
            {
                Id = _editingId,
                Model = Model,
                WireId = SelectedWire.Id,
                Price = Price,
                Note = Note
            });
            await LoadSampleBlocksAsync();
            SelectedSampleBlock = SampleBlocks.FirstOrDefault(x => x.Id == saved.Id);
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

        if (MessageBox.Show($"确认删除样块\"{Model}\"吗？", "删除确认", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _sampleBlockService.DeleteAsync(_editingId);
            ResetForm();
            await LoadSampleBlocksAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "删除失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
