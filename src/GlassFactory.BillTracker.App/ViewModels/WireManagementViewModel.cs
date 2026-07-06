using System.Collections.ObjectModel;
using System.Windows;
using GlassFactory.BillTracker.App.Commands;
using GlassFactory.BillTracker.App.ViewModels.Base;
using GlassFactory.BillTracker.Data.Services;
using GlassFactory.BillTracker.Domain.Entities;

namespace GlassFactory.BillTracker.App.ViewModels;

public sealed class WireManagementViewModel : ObservableObject
{
    private readonly IWireService _wireService;

    private Guid _editingId;
    private string _model = string.Empty;
    private string? _manufacturer;
    private decimal _price;
    private string? _note;
    private string? _searchKeyword;
    private Wire? _selectedWire;

    public ObservableCollection<Wire> Wires { get; } = new();

    public WireManagementViewModel(IWireService wireService)
    {
        _wireService = wireService;

        NewCommand = new RelayCommand(ResetForm);
        SaveCommand = new RelayCommand(() => _ = SaveAsync());
        DeleteCommand = new RelayCommand(() => _ = DeleteAsync());
        SearchCommand = new RelayCommand(() => _ = LoadAsync());

        _ = LoadAsync();
    }

    public string? SearchKeyword
    {
        get => _searchKeyword;
        set => SetProperty(ref _searchKeyword, value);
    }

    public Wire? SelectedWire
    {
        get => _selectedWire;
        set
        {
            if (SetProperty(ref _selectedWire, value) && value is not null)
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

    public string? Manufacturer
    {
        get => _manufacturer;
        set => SetProperty(ref _manufacturer, value);
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

    public async Task LoadAsync()
    {
        try
        {
            var items = await _wireService.GetWiresAsync(SearchKeyword);
            Wires.Clear();
            foreach (var w in items)
            {
                Wires.Add(w);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载丝列表失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadForm(Wire wire)
    {
        _editingId = wire.Id;
        Model = wire.Model;
        Manufacturer = wire.Manufacturer;
        Price = wire.Price;
        Note = wire.Note;
    }

    private void ResetForm()
    {
        _editingId = Guid.Empty;
        Model = string.Empty;
        Manufacturer = null;
        Price = 0m;
        Note = null;
        SelectedWire = null;
    }

    private async Task SaveAsync()
    {
        try
        {
            var saved = await _wireService.SaveAsync(new Wire
            {
                Id = _editingId,
                Model = Model,
                Manufacturer = Manufacturer,
                Price = Price,
                Note = Note
            });
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

        if (MessageBox.Show($"确认删除丝\"{Model}\"吗？", "删除确认", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

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
}
