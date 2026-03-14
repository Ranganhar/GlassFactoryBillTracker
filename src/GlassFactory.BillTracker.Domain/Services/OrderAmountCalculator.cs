using GlassFactory.BillTracker.Domain.Entities;

namespace GlassFactory.BillTracker.Domain.Services;

public static class OrderAmountCalculator
{
    public static decimal CalculateAreaM2(decimal glassLengthMm, decimal glassWidthMm)
    {
        return (glassLengthMm / 1000m) * (glassWidthMm / 1000m);
    }

    public static decimal CalculateGlassCost(OrderItem item)
    {
        var areaM2 = CalculateAreaM2(item.GlassLengthMm, item.GlassWidthMm);
        return areaM2 * item.Quantity * item.GlassUnitPricePerM2;
    }

    public static decimal CalculateRawAmount(OrderItem item)
    {
        return CalculateGlassCost(item) + item.HoleFee + item.OtherFee;
    }

    public static decimal CalculateAmount(OrderItem item)
    {
        var rawAmount = CalculateRawAmount(item);
        return Round(rawAmount);
    }

    public static decimal CalculateOrderTotal(IEnumerable<OrderItem> items)
    {
        // Total must be the sum of per-row rounded amounts.
        var sum = items.Sum(item => Round(CalculateRawAmount(item)));
        return Round(sum);
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
}
