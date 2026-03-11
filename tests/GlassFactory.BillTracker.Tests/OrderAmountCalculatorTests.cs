using GlassFactory.BillTracker.Domain.Entities;
using GlassFactory.BillTracker.Domain.Services;

namespace GlassFactory.BillTracker.Tests;

public class OrderAmountCalculatorTests
{
    [Fact]
    public void CalculateAmountAndTotal_ShouldUseMmToM2AndRoundTo2Digits_AwayFromZero()
    {
        var item1 = new OrderItem
        {
            GlassLengthMm = 1234.5678m,
            GlassWidthMm = 987.6543m,
            Quantity = 2,
            GlassUnitPricePerM2 = 88.8888m,
            Model = "A-01",
            WireType = "丝A",
            HoleFee = 5.5555m,
            OtherFee = 1.2345m
        };

        var item2 = new OrderItem
        {
            GlassLengthMm = 1500m,
            GlassWidthMm = 800m,
            Quantity = 3,
            GlassUnitPricePerM2 = 99.9999m,
            Model = "B-02",
            WireType = "丝B",
            HoleFee = 2.2222m,
            OtherFee = 0.3333m
        };

        var amount1 = OrderAmountCalculator.CalculateAmount(item1);
        var amount2 = OrderAmountCalculator.CalculateAmount(item2);

        Assert.Equal(223.56m, amount1);
        Assert.Equal(362.56m, amount2);

        var total = OrderAmountCalculator.CalculateOrderTotal(new[] { item1, item2 });
        Assert.Equal(586.12m, total);

        var rounded = OrderAmountCalculator.Round(1.23445m);
        Assert.Equal(1.23m, rounded);
    }
}
