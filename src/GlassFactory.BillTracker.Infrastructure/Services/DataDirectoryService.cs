using System.Text.Json;
using System.Windows.Forms;
using GlassFactory.BillTracker.Infrastructure.Abstractions;

namespace GlassFactory.BillTracker.Infrastructure.Services;

public class DataDirectoryService : IDataDirectoryService
{
    private const string SettingsFileName = "settings.json";

    public string GetOrSelectDataDir()
    {
        var settingsPath = GetSettingsFilePath();
        var existing = TryReadExistingDataDir(settingsPath);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            EnsureDataDirectories(existing);
            return existing;
        }

        var selectedDir = SelectDirectoryFromUser();
        EnsureDataDirectories(selectedDir);
        PersistDataDir(settingsPath, selectedDir);

        return selectedDir;
    }

    private static string SelectDirectoryFromUser()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "请选择数据目录（建议 D 或 E 盘）",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };

        var result = dialog.ShowDialog();
        if (result != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            throw new InvalidOperationException("必须选择数据目录才能继续使用系统。");
        }

        var selectedPath = dialog.SelectedPath;
        if (IsSystemDrive(selectedPath))
        {
            var confirmResult = MessageBox.Show(
                "你选择的是 C 盘目录。为避免系统盘空间问题，建议改为 D/E 盘。是否继续？",
                "系统盘提示",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirmResult != DialogResult.Yes)
            {
                return SelectDirectoryFromUser();
            }
        }

        return selectedPath;
    }

    private static bool IsSystemDrive(string path)
    {
        var root = Path.GetPathRoot(path);
        return string.Equals(root, "C:\\", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryReadExistingDataDir(string settingsPath)
    {
        if (!File.Exists(settingsPath))
        {
            return null;
        }

        var json = File.ReadAllText(settingsPath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        var settings = JsonSerializer.Deserialize<AppDataDirectorySettings>(json);
        return settings?.DataDirectory;
    }

    private static void PersistDataDir(string settingsPath, string dataDir)
    {
        var settingsFolder = Path.GetDirectoryName(settingsPath)!;
        Directory.CreateDirectory(settingsFolder);

        var content = JsonSerializer.Serialize(new AppDataDirectorySettings
        {
            DataDirectory = dataDir
        }, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(settingsPath, content);
    }

    private static string GetSettingsFilePath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var settingDir = Path.Combine(appDataPath, "GlassFactoryBillTracker");
        return Path.Combine(settingDir, SettingsFileName);
    }

    private static void EnsureDataDirectories(string dataDir)
    {
        Directory.CreateDirectory(dataDir);
        Directory.CreateDirectory(Path.Combine(dataDir, "attachments"));
        Directory.CreateDirectory(Path.Combine(dataDir, "exports"));
        Directory.CreateDirectory(Path.Combine(dataDir, "logs"));
    }
}
