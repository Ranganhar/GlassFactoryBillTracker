using GlassFactory.BillTracker.Domain.Entities;
using GlassFactory.BillTracker.Domain.Services;

namespace GlassFactory.BillTracker.Tests;

public class OrderAmountCalculatorTests
{
    [Fact]
    public void CalculateLineAndTotal_ShouldUseMmToM2AndRoundTo4Digits_AwayFromZero()
    {
        var item1 = new OrderItem
        {
            GlassLengthMm = 1234.5678m,
            GlassWidthMm = 987.6543m,
            Quantity = 2,
            GlassUnitPricePerM2 = 88.8888m,
            WireType = "丝A",
            WireUnitPrice = 5.5555m,
            OtherFee = 1.2345m
        };

        var item2 = new OrderItem
        {
            GlassLengthMm = 1500m,
            GlassWidthMm = 800m,
            Quantity = 3,
            GlassUnitPricePerM2 = 99.9999m,
            WireType = "丝B",
            WireUnitPrice = 2.2222m,
            OtherFee = 0.3333m
        };

        var line1 = OrderAmountCalculator.CalculateLineAmount(item1);
        var line2 = OrderAmountCalculator.CalculateLineAmount(item2);

        Assert.Equal(223.5542m, line1);
        Assert.Equal(362.5551m, line2);

        var total = OrderAmountCalculator.CalculateOrderTotal(new[] { item1, item2 });
        Assert.Equal(586.1093m, total);

        var rounded = OrderAmountCalculator.Round(1.23445m);
        Assert.Equal(1.2345m, rounded);
    }
}
