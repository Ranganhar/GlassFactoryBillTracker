namespace GlassFactory.BillTracker.App.Services;

public interface IFileDialogService
{
    string? SelectSaveFilePath(string title, string filter, string defaultExtension, string initialDirectory, string fileName);
}
