namespace GlassFactory.BillTracker.App.Models;

public sealed class ExportExcelOptions
{
    public bool UseSelectedOrders { get; init; }
    public bool UseDateRange { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public bool UseCustomerFilter { get; init; }
    public Guid? CustomerId { get; init; }
    public string OutputPath { get; init; } = string.Empty;
}
