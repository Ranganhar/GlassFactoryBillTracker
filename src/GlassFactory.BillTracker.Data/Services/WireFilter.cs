namespace GlassFactory.BillTracker.Data.Services;

public sealed class WireFilter
{
    public string? Model { get; set; }
    public decimal? PriceMin { get; set; }
    public decimal? PriceMax { get; set; }
    public DateTime? PurchaseFrom { get; set; }
    public DateTime? PurchaseTo { get; set; }
    public string? Note { get; set; }
}
