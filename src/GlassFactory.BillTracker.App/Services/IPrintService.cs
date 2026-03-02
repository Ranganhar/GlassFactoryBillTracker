using System.Windows.Documents;
using GlassFactory.BillTracker.App.Models;

namespace GlassFactory.BillTracker.App.Services;

public interface IPrintService
{
    FixedDocument RenderDotMatrixTriplicate(IReadOnlyList<OrderExportDto> orders, PrintBillOptions options);
    FixedDocument RenderA4(IReadOnlyList<OrderExportDto> orders, PrintBillOptions options);
}
