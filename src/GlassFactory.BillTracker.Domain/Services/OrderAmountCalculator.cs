using GlassFactory.BillTracker.Domain.Entities;

namespace GlassFactory.BillTracker.Domain.Services;

public static class OrderAmountCalculator
{
    public static decimal CalculateAreaM2(decimal glassLengthMm, decimal glassWidthMm)
    {
        return (glassLengthMm / 1000m) * (glassWidthMm / 1000m);
    }

    public static decimal CalculateAreaM2Rounded(decimal glassLengthMm, decimal glassWidthMm)
    {
        return Math.Round(CalculateAreaM2(glassLengthMm, glassWidthMm), 2, MidpointRounding.AwayFromZero);
    }

    public static decimal CalculateLineAreaM2(decimal glassLengthMm, decimal glassWidthMm, int quantity)
    {
        return CalculateAreaM2(glassLengthMm, glassWidthMm) * quantity;
    }

    public static decimal CalculateLineAreaM2Rounded(decimal glassLengthMm, decimal glassWidthMm, int quantity)
    {
        return Math.Round(
            CalculateLineAreaM2(glassLengthMm, glassWidthMm, quantity),
            2,
            MidpointRounding.AwayFromZero);
    }

    public static decimal CalculateTotalAreaM2(IEnumerable<OrderItem> items)
    {
        var sum = items.Sum(item => CalculateLineAreaM2(item.GlassLengthMm, item.GlassWidthMm, item.Quantity));
        return Math.Round(sum, 2, MidpointRounding.AwayFromZero);
    }

    public static int CalculateTotalQuantity(IEnumerable<OrderItem> items)
    {
        return items.Sum(item => item.Quantity);
    }

    public static decimal CalculateGlassCost(OrderItem item)
    {
        var lineAreaM2 = CalculateLineAreaM2(item.GlassLengthMm, item.GlassWidthMm, item.Quantity);
        return lineAreaM2 * item.GlassUnitPricePerM2;
    }

    public static decimal CalculateRawAmount(OrderItem item)
    {
        return CalculateGlassCost(item) + item.HoleFee + item.OtherFee;
    }

    public static decimal CalculateAmount(OrderItem item)
    {
        var rawAmount = CalculateRawAmount(item);
        return RoundAmount(rawAmount);
    }

    public static decimal CalculateOrderTotal(IEnumerable<OrderItem> items)
    {
        var sum = items.Sum(item => RoundAmount(CalculateRawAmount(item)));
        return RoundAmount(sum);
    }

    public static void ApplyAmount(OrderItem item)
    {
        item.Amount = CalculateAmount(item);
    }

    public static void ApplyOrderTotal(Order order)
    {
        foreach (var item in order.Items)
        {
            ApplyAmount(item);
        }

        order.TotalAmount = CalculateOrderTotal(order.Items);
    }

    public static decimal Round(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal RoundAmount(decimal value)
    {
        return Math.Round(value, 0, MidpointRounding.AwayFromZero);
    }
}
