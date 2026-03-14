using GlassFactory.BillTracker.Domain.Entities;
using GlassFactory.BillTracker.Domain.Services;

namespace GlassFactory.BillTracker.Tests;

public class OrderAmountCalculatorTests
{
    [Fact]
    public void CalculateAmountAndTotal_ShouldUseHoleAndOtherFeeAndRoundToInteger_AwayFromZero()
    {
        var item1 = new OrderItem
        {
            GlassLengthMm = 1234.5678m,
            GlassWidthMm = 987.6543m,
            Quantity = 2,
            GlassUnitPricePerM2 = 88.8888m,
            Model = "A-01",
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
            Model = "B-02",
            WireType = "丝B",
            WireUnitPrice = 2.2222m,
            OtherFee = 0.3333m
        };

        var amount1 = OrderAmountCalculator.CalculateAmount(item1);
        var amount2 = OrderAmountCalculator.CalculateAmount(item2);

        Assert.Equal(218m, amount1);
        Assert.Equal(360m, amount2);

        var total = OrderAmountCalculator.CalculateOrderTotal(new[] { item1, item2 });
        Assert.Equal(578m, total);

        var rounded = OrderAmountCalculator.Round(1.23445m);
        Assert.Equal(1.23m, rounded);

        var roundedAmount = OrderAmountCalculator.RoundAmount(1.5m);
        Assert.Equal(2m, roundedAmount);
    }

    [Fact]
    public void CalculateOrderTotal_ShouldEqualSumOfIntegerRoundedLineAmounts()
    {
        var item1 = new OrderItem
        {
            GlassLengthMm = 1000m,
            GlassWidthMm = 1000m,
            Quantity = 1,
            GlassUnitPricePerM2 = 1.4m,
            Model = "R-01",
            WireType = "丝A",
            WireUnitPrice = 0.2m,
            OtherFee = 0m
        };

        var item2 = new OrderItem
        {
            GlassLengthMm = 1000m,
            GlassWidthMm = 1000m,
            Quantity = 1,
            GlassUnitPricePerM2 = 1.4m,
            Model = "R-02",
            WireType = "丝B",
            WireUnitPrice = 0m,
            OtherFee = 0m
        };

        var rowAmount1 = OrderAmountCalculator.CalculateAmount(item1);
        var rowAmount2 = OrderAmountCalculator.CalculateAmount(item2);
        var total = OrderAmountCalculator.CalculateOrderTotal(new[] { item1, item2 });

        Assert.Equal(1m, rowAmount1);
        Assert.Equal(1m, rowAmount2);
        Assert.Equal(2m, total);

        var rawSumRounded = OrderAmountCalculator.RoundAmount(
            OrderAmountCalculator.CalculateRawAmount(item1) +
            OrderAmountCalculator.CalculateRawAmount(item2));

        Assert.Equal(3m, rawSumRounded);
        Assert.NotEqual(rawSumRounded, total);
    }

    [Fact]
    public void CalculateAmountAndTotal_ShouldIncludeHoleFee_Regression()
    {
        var item = new OrderItem
        {
            GlassLengthMm = 1000m,
            GlassWidthMm = 1000m,
            Quantity = 1,
            GlassUnitPricePerM2 = 10m,
            HoleFee = 3m,
            OtherFee = 2m,
            Model = "H-01",
            WireType = "",
            WireUnitPrice = 999m
        };

        var rowAmount = OrderAmountCalculator.CalculateAmount(item);
        var totalAmount = OrderAmountCalculator.CalculateOrderTotal(new[] { item });

        Assert.Equal(15m, rowAmount);
        Assert.Equal(15m, totalAmount);
    }
}
