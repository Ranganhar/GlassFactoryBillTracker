using GlassFactory.BillTracker.Domain.Enums;
using GlassFactory.BillTracker.App.ViewModels.Base;

namespace GlassFactory.BillTracker.App.ViewModels.Rows;

public sealed class OrderListItemViewModel : ObservableObject
{
    private bool _isSelected;

    public Guid Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public DateTime DateTime { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public PaymentMethod PaymentMethod { get; set; }
    public OrderStatus OrderStatus { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Note { get; set; }
    public string? AttachmentPath { get; set; }

    public Action<OrderListItemViewModel, bool>? SelectionChangedCallback { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                SelectionChangedCallback?.Invoke(this, value);
            }
        }
    }
}
