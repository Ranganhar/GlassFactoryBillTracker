using GlassFactory.BillTracker.App.ViewModels.Base;
using GlassFactory.BillTracker.Domain.Entities;
using GlassFactory.BillTracker.Domain.Services;

namespace GlassFactory.BillTracker.App.ViewModels.Rows;

public sealed class OrderItemRowViewModel : ObservableObject
{
    private Guid _id;
    private decimal _glassLengthMm;
    private decimal _glassWidthMm;
    private int _quantity = 1;
    private decimal _glassUnitPricePerM2;
    private string _model = string.Empty;
    private string _wireType = string.Empty;
    private decimal _wireUnitPrice;
    private decimal _holeFee;
    private decimal _otherFee;
    private decimal _amount;
    private string? _note;

    private string? _sampleBlockModel;
    private readonly Action? _recalculateCallback;
    private readonly Func<string, (string WireModel, decimal Price)?>? _sampleBlockResolver;

    public OrderItemRowViewModel(
        Action? recalculateCallback = null,
        Func<string, (string WireModel, decimal Price)?>? sampleBlockResolver = null)
    {
        _recalculateCallback = recalculateCallback;
        _sampleBlockResolver = sampleBlockResolver;
    }

    public Guid Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public decimal GlassLengthMm
    {
        get => _glassLengthMm;
        set
        {
            if (SetProperty(ref _glassLengthMm, value))
            {
                Recalculate();
                OnPropertyChanged(nameof(AreaM2));
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
                OnPropertyChanged(nameof(AreaM2));
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

    public string Model
    {
        get => _model;
        set
        {
            var normalized = NormalizeModel(value);
            if (SetProperty(ref _model, normalized))
            {
                Recalculate();
            }
        }
    }

    public string? SampleBlockModel
    {
        get => _sampleBlockModel;
        set
        {
            var normalized = (value ?? string.Empty).Trim();
            if (SetProperty(ref _sampleBlockModel, normalized))
            {
                var resolved = _sampleBlockResolver?.Invoke(normalized);
                if (resolved is { } r)
                {
                    WireType = r.WireModel;
                    WireUnitPrice = r.Price;
                }
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

    public decimal HoleFee
    {
        get => _holeFee;
        set
        {
            if (SetProperty(ref _holeFee, value))
            {
                Recalculate();
            }
        }
    }

    public decimal Amount
    {
        get => _amount;
        private set => SetProperty(ref _amount, value);
    }

    public decimal AreaM2 => OrderAmountCalculator.CalculateAreaM2Rounded(GlassLengthMm, GlassWidthMm);

    public string? Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
    }

    public OrderItem ToEntity()
    {
        var item = new OrderItem
        {
            Id = Id,
            GlassLengthMm = GlassLengthMm,
            GlassWidthMm = GlassWidthMm,
            Quantity = Quantity,
            GlassUnitPricePerM2 = GlassUnitPricePerM2,
            Model = string.IsNullOrWhiteSpace(Model) ? string.Empty : Model.Trim(),
            SampleBlockModel = string.IsNullOrWhiteSpace(SampleBlockModel) ? null : SampleBlockModel.Trim(),
            WireType = WireType,
            WireUnitPrice = WireUnitPrice,
            HoleFee = HoleFee,
            OtherFee = OtherFee,
            Note = Note
        };

        item.Amount = OrderAmountCalculator.CalculateAmount(item);
        return item;
    }

    public static OrderItemRowViewModel FromEntity(
        OrderItem item,
        Action? recalculateCallback = null,
        Func<string, (string WireModel, decimal Price)?>? sampleBlockResolver = null)
    {
        var row = new OrderItemRowViewModel(recalculateCallback, sampleBlockResolver)
        {
            Id = item.Id,
            GlassLengthMm = item.GlassLengthMm,
            GlassWidthMm = item.GlassWidthMm,
            Quantity = item.Quantity,
            GlassUnitPricePerM2 = item.GlassUnitPricePerM2,
            Model = item.Model,
            SampleBlockModel = item.SampleBlockModel, // set first — may trigger resolver
            WireType = item.WireType,                 // overwrite with stored snapshot
            WireUnitPrice = item.WireUnitPrice,
            HoleFee = item.HoleFee,
            OtherFee = item.OtherFee,
            Note = item.Note
        };

        row.Recalculate();
        return row;
    }

    public OrderItemRowViewModel CloneForCopy(
        Action? recalculateCallback = null,
        Func<string, (string WireModel, decimal Price)?>? sampleBlockResolver = null)
    {
        return new OrderItemRowViewModel(recalculateCallback, sampleBlockResolver)
        {
            Id = Guid.Empty,
            GlassLengthMm = GlassLengthMm,
            GlassWidthMm = GlassWidthMm,
            Quantity = Quantity,
            GlassUnitPricePerM2 = GlassUnitPricePerM2,
            Model = Model,
            SampleBlockModel = SampleBlockModel, // set first — may trigger resolver
            WireType = WireType,                 // overwrite with stored snapshot
            WireUnitPrice = WireUnitPrice,
            HoleFee = HoleFee,
            OtherFee = OtherFee,
            Note = Note
        };
    }

    public void Recalculate()
    {
        var snapshot = ToEntity();
        Amount = snapshot.Amount;
        _recalculateCallback?.Invoke();
    }

    private static string NormalizeModel(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        return trimmed.Length <= 13 ? trimmed : trimmed[..13];
    }
}
