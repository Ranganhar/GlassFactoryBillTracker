using GlassFactory.BillTracker.App.Commands;
using GlassFactory.BillTracker.App.ViewModels.Base;
using GlassFactory.BillTracker.Domain.Entities;

namespace GlassFactory.BillTracker.App.ViewModels;

public sealed class CustomerEditViewModel : ObservableObject
{
    private string _name = string.Empty;
    private string? _phone;
    private string? _address;
    private string? _note;

    public Guid Id { get; }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string? Phone
    {
        get => _phone;
        set => SetProperty(ref _phone, value);
    }

    public string? Address
    {
        get => _address;
        set => SetProperty(ref _address, value);
    }

    public string? Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
    }

    public RelayCommand SaveCommand { get; }
    public RelayCommand CancelCommand { get; }

    public event Action? Saved;
    public event Action? Canceled;

    public CustomerEditViewModel(Customer? existing = null)
    {
        Id = existing?.Id ?? Guid.Empty;
        Name = existing?.Name ?? string.Empty;
        Phone = existing?.Phone;
        Address = existing?.Address;
        Note = existing?.Note;

        SaveCommand = new RelayCommand(OnSave);
        CancelCommand = new RelayCommand(() => Canceled?.Invoke());
    }

    public Customer BuildCustomer()
    {
        return new Customer
        {
            Id = Id,
            Name = Name.Trim(),
            Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim(),
            Address = string.IsNullOrWhiteSpace(Address) ? null : Address.Trim(),
            Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim()
        };
    }

    private void OnSave()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException("客户名称不能为空。");
        }

        Saved?.Invoke();
    }
}
