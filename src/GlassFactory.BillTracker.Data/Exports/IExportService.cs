namespace GlassFactory.BillTracker.Data.Exports;

public interface IExportService
{
    Task<ExportResult> ExportExcelAsync(ExportOrderFilter filter, string dataDir, string? targetPath = null, CancellationToken cancellationToken = default);
    Task<ExportResult> ExportJsonAsync(ExportOrderFilter filter, string dataDir, string? targetPath = null, CancellationToken cancellationToken = default);
}
