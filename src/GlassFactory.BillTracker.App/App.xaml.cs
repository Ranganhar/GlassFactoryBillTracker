using System.Windows;
using System.IO;
using GlassFactory.BillTracker.Data.Persistence;
using GlassFactory.BillTracker.Infrastructure.Abstractions;
using GlassFactory.BillTracker.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;
using GlassFactory.BillTracker.App.Views;
using GlassFactory.BillTracker.App.Services;

namespace GlassFactory.BillTracker.App;

public partial class App : Application
{
	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		try
		{
			IDataDirectoryService dataDirectoryService = new DataDirectoryService();
			IAppDbPathProvider dbPathProvider = new AppDbPathProvider();

			var dataDir = dataDirectoryService.GetOrSelectDataDir();
			var dbPath = dbPathProvider.GetDbPath(dataDir);

			AppRuntimeContext.Initialize(dataDir, dbPath);

			Log.Logger = new LoggerConfiguration()
				.MinimumLevel.Information()
				.WriteTo.File(
					Path.Combine(dataDir, "logs", "app.log"),
					rollingInterval: RollingInterval.Day,
					retainedFileCountLimit: 30)
				.CreateLogger();

			using (var dbContext = CreateDbContext(dbPath))
			{
				dbContext.Database.Migrate();
			}

			Log.Information("应用启动成功，数据库路径: {DbPath}", dbPath);

			var window = new MainWindow();
			window.Show();
		}
		catch (Exception ex)
		{
			Log.Error(ex, "应用启动失败");
			MessageBox.Show($"应用启动失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
			Shutdown(-1);
		}
	}

	private static BillTrackerDbContext CreateDbContext(string dbPath)
	{
		var optionsBuilder = new DbContextOptionsBuilder<BillTrackerDbContext>();
		optionsBuilder.UseSqlite($"Data Source={dbPath}");
		return new BillTrackerDbContext(optionsBuilder.Options);
	}

	protected override void OnExit(ExitEventArgs e)
	{
		Log.Information("应用退出。Code={Code}", e.ApplicationExitCode);
		Log.CloseAndFlush();
		base.OnExit(e);
	}
}
