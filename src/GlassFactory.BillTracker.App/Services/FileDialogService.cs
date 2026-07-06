using Microsoft.Win32;

namespace GlassFactory.BillTracker.App.Services;

public sealed class FileDialogService : IFileDialogService
{
    public string? SelectSaveFilePath(string title, string filter, string defaultExtension, string initialDirectory, string fileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = filter,
            DefaultExt = defaultExtension,
            AddExtension = true,
            OverwritePrompt = true,
            InitialDirectory = initialDirectory,
            FileName = fileName
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
