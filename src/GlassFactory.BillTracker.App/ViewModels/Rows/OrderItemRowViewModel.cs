using GlassFactory.BillTracker.App.ViewModels.Base;
using GlassFactory.BillTracker.Domain.Entities;
using GlassFactory.BillTracker.Domain.Services;

namespace GlassFactory.BillTracker.App.ViewModels.Rows;

public sealed class OrderItemRowViewModel : ObservableObject
{
    private decimal _glassLengthMm;
    private decimal _glassWidthMm;
    private int _quantity = 1;
    private decimal _glassUnitPricePerM2;
    private string _wireType = string.Empty;
    private decimal _wireUnitPrice;
    private decimal _otherFee;
    private decimal _lineAmount;
    private string? _note;

    private readonly Action? _recalculateCallback;

    public OrderItemRowViewModel(Action? recalculateCallback = null)
    {
        _recalculateCallback = recalculateCallback;
    }

    public decimal GlassLengthMm
    {
        get => _glassLengthMm;
        set
        {
            if (SetProperty(ref _glassLengthMm, value))
            {
                Recalculate();
            }
        }
    }

    public decimal GlassWidthMm
    {
        get => _glassWidthMm;
        set
        {
            if (SetProperty(ref _glassWidthMm, value))
            {
                Recalculate();
            }
        }
    }

    public int Quantity
    {
        get => _quantity;
        set
        {
            if (SetProperty(ref _quantity, value))
            {
                Recalculate();
            }
        }
    }

    public decimal GlassUnitPricePerM2
    {
        get => _glassUnitPricePerM2;
        set
        {
            if (SetProperty(ref _glassUnitPricePerM2, value))
            {
                Recalculate();
            }
        }
    }

    public string WireType
    {
        get => _wireType;
        set
        {
            if (SetProperty(ref _wireType, value))
            {
                Recalculate();
            }
        }
    }

    public decimal WireUnitPrice
    {
        get => _wireUnitPrice;
        set
        {
            if (SetProperty(ref _wireUnitPrice, value))
            {
                Recalculate();
            }
        }
    }

    public decimal OtherFee
    {
        get => _otherFee;
        set
        {
            if (SetProperty(ref _otherFee, value))
            {
                Recalculate();
            }
        }
    }

    public decimal LineAmount
    {
        get => _lineAmount;
        private set => SetProperty(ref _lineAmount, value);
    }

    public string? Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
    }

    public OrderItem ToEntity()
    {
        var item = new OrderItem
        {
            GlassLengthMm = GlassLengthMm,
            GlassWidthMm = GlassWidthMm,
            Quantity = Quantity,
            GlassUnitPricePerM2 = GlassUnitPricePerM2,
            WireType = WireType,
            WireUnitPrice = WireUnitPrice,
            OtherFee = OtherFee,
            Note = Note
        };

        item.LineAmount = OrderAmountCalculator.CalculateLineAmount(item);
        return item;
    }

    public static OrderItemRowViewModel FromEntity(OrderItem item, Action? recalculateCallback = null)
    {
        var row = new OrderItemRowViewModel(recalculateCallback)
        {
            GlassLengthMm = item.GlassLengthMm,
            GlassWidthMm = item.GlassWidthMm,
            Quantity = item.Quantity,
            GlassUnitPricePerM2 = item.GlassUnitPricePerM2,
            WireType = item.WireType,
            WireUnitPrice = item.WireUnitPrice,
            OtherFee = item.OtherFee,
            Note = item.Note
        };

        row.Recalculate();
        return row;
    }

    public void Recalculate()
    {
        var snapshot = ToEntity();
        LineAmount = snapshot.LineAmount;
        _recalculateCallback?.Invoke();
    }
}
