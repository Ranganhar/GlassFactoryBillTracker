using System.IO;

namespace GlassFactory.BillTracker.App.ViewModels.Rows;

public sealed class OrderAttachmentRowViewModel
{
    public Guid? AttachmentId { get; init; }
    public string? RelativePath { get; init; }
    public string? SourcePath { get; init; }
    public DateTime CreatedAt { get; init; }

    public string? AbsolutePath
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(SourcePath) && File.Exists(SourcePath))
            {
                return SourcePath;
            }

            if (string.IsNullOrWhiteSpace(RelativePath))
            {
                return null;
            }

            var absolute = Path.Combine(
                Services.AppRuntimeContext.DataDir,
                RelativePath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(absolute) ? absolute : null;
        }
    }

    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(SourcePath))
            {
                return Path.GetFileName(SourcePath);
            }

            if (!string.IsNullOrWhiteSpace(RelativePath))
            {
                return Path.GetFileName(RelativePath);
            }

            return "未命名附件";
        }
    }

    public bool IsPersisted => AttachmentId.HasValue && AttachmentId.Value != Guid.Empty;
}
