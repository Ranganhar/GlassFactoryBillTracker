using GlassFactory.BillTracker.Domain.Entities;

namespace GlassFactory.BillTracker.Domain.Services;

public static class OrderAmountCalculator
{
    public static decimal CalculateAreaM2(decimal glassLengthMm, decimal glassWidthMm)
    {
        var area = (glassLengthMm / 1000m) * (glassWidthMm / 1000m);
        return Round(area);
    }

    public static decimal CalculateGlassCost(OrderItem item)
    {
        var areaM2 = CalculateAreaM2(item.GlassLengthMm, item.GlassWidthMm);
        var glassCost = areaM2 * item.Quantity * item.GlassUnitPricePerM2;
        return Round(glassCost);
    }

    public static decimal CalculateLineAmount(OrderItem item)
    {
        var lineAmount = CalculateGlassCost(item) + item.WireUnitPrice + item.OtherFee;
        return Round(lineAmount);
    }

    public static decimal CalculateOrderTotal(IEnumerable<OrderItem> items)
    {
        var sum = items.Sum(CalculateLineAmount);
        return Round(sum);
    }

    public static void ApplyLineAmount(OrderItem item)
    {
        item.LineAmount = CalculateLineAmount(item);
    }

    public static void ApplyOrderTotal(Order order)
    {
        foreach (var item in order.Items)
        {
            ApplyLineAmount(item);
        }

        order.TotalAmount = CalculateOrderTotal(order.Items);
    }

    public static decimal Round(decimal value)
    {
        return Math.Round(value, 4, MidpointRounding.AwayFromZero);
    }
}
