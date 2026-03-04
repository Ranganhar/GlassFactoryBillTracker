using System;
using System.IO;
using System.Windows;
using System.Windows.Forms;

namespace GlassFactory.BillTracker.App.Win7
{
    public partial class App : Application
    {
        public static string DataDir { get; private set; } = string.Empty;
        public static string DbPath { get; private set; } = string.Empty;
        public static string LogsDir { get; private set; } = string.Empty;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var defaultDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GlassFactory.BillTracker.Win7");
            var selectedDir = SelectDataDir(defaultDir);
            DataDir = selectedDir;
            DbPath = Path.Combine(DataDir, "billtracker.db");
            LogsDir = Path.Combine(DataDir, "logs");

            Directory.CreateDirectory(DataDir);
            Directory.CreateDirectory(Path.Combine(DataDir, "attachments"));
            Directory.CreateDirectory(Path.Combine(DataDir, "exports"));
            Directory.CreateDirectory(LogsDir);
        }

        private static string SelectDataDir(string fallback)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select data directory";
                dialog.SelectedPath = fallback;
                dialog.ShowNewFolderButton = true;
                var result = dialog.ShowDialog();
                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    return dialog.SelectedPath;
                }
            }

            return fallback;
        }
    }
}
