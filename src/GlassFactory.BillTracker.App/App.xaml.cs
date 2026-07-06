using System.Windows;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Threading;
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
	private readonly object _loggerSync = new();
	private readonly string _settingsPath;
	private readonly string _emergencyLogDir;
	private string _activeLogDir;
	private string _crashLogPath;

	public App()
	{
		_settingsPath = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
			"GlassFactoryBillTracker",
			"settings.json");

		_emergencyLogDir = Path.Combine(
			Path.GetTempPath(),
			"GlassFactoryBillTracker",
			"logs");

		_activeLogDir = _emergencyLogDir;
		_crashLogPath = Path.Combine(_activeLogDir, "crash.log");

		ConfigureLogger(_activeLogDir, "应急日志模式已启用");
		RegisterGlobalExceptionHandlers();
	}

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		try
		{
			WriteStartupDiagnostics(dataDir: null, dbPath: null);

			if (HasArgument(e.Args, "--force-crash"))
			{
				throw new InvalidOperationException("FORCED_CRASH_TEST");
			}

			IDataDirectoryService dataDirectoryService = new DataDirectoryService();
			IAppDbPathProvider dbPathProvider = new AppDbPathProvider();

			var dataDir = dataDirectoryService.GetOrSelectDataDir();
			var dbPath = dbPathProvider.GetDbPath(dataDir);
			var dataLogDir = Path.Combine(dataDir, "logs");

			if (!string.Equals(
				_activeLogDir,
				dataLogDir,
				StringComparison.OrdinalIgnoreCase))
			{
				ConfigureLogger(dataLogDir, $"Log directory switched: {_activeLogDir} -> {dataLogDir}");
			}

			AppRuntimeContext.Initialize(dataDir, dbPath);
			WriteStartupDiagnostics(dataDir, dbPath);

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
			HandleFatal(ex, "应用启动失败", recoverable: false);
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

	private void RegisterGlobalExceptionHandlers()
	{
		DispatcherUnhandledException += OnDispatcherUnhandledException;
		Dispatcher.UnhandledExceptionFilter += OnDispatcherUnhandledExceptionFilter;
		AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
		TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
	}

	private void OnDispatcherUnhandledExceptionFilter(object sender, DispatcherUnhandledExceptionFilterEventArgs e)
	{
		if (e.Exception is null)
		{
			return;
		}

		Log.Warning(e.Exception, "DispatcherUnhandledExceptionFilter 捕获到异常（将进入 DispatcherUnhandledException）");
	}

	private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
	{
		HandleFatal(e.Exception, "UI线程未处理异常", recoverable: true);
		e.Handled = true;
	}

	private void OnDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
	{
		var ex = e.ExceptionObject as Exception ?? new Exception("未知 AppDomain 未处理异常");
		HandleFatal(ex, "AppDomain 未处理异常", recoverable: false);
	}

	private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
	{
		HandleFatal(e.Exception, "TaskScheduler 未观察任务异常", recoverable: true);
		e.SetObserved();
	}

	private void HandleFatal(Exception ex, string source, bool recoverable)
	{
		try
		{
			Log.Fatal(ex, "{Source}。ExceptionType={ExceptionType}", source, ex.GetType().FullName);
			Log.Information("Crash log path: {CrashLogPath}", _crashLogPath);
			Log.CloseAndFlush();
		}
		catch
		{
		}

		try
		{
			MessageBox.Show(
				$"程序发生错误，日志已保存到：{_crashLogPath}\n\n请将该日志发给开发者。",
				"程序异常",
				MessageBoxButton.OK,
				MessageBoxImage.Error);
		}
		catch
		{
		}

		if (!recoverable)
		{
			Environment.Exit(1);
		}
	}

	private void ConfigureLogger(string logDir, string? bootstrapMessage = null)
	{
		lock (_loggerSync)
		{
			Directory.CreateDirectory(logDir);
			var appLogPath = Path.Combine(logDir, "app.log");
			var crashLogPath = Path.Combine(logDir, "crash.log");

			try
			{
				Log.CloseAndFlush();
			}
			catch
			{
			}

			Log.Logger = new LoggerConfiguration()
				.MinimumLevel.Information()
				.Enrich.WithProperty("LogDirectory", logDir)
				.WriteTo.File(
					appLogPath,
					rollingInterval: RollingInterval.Day,
					retainedFileCountLimit: 30)
				.WriteTo.File(
					crashLogPath,
					rollingInterval: RollingInterval.Day,
					retainedFileCountLimit: 30,
					restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Error)
				.CreateLogger();

			_activeLogDir = logDir;
			_crashLogPath = crashLogPath;

			if (!string.IsNullOrWhiteSpace(bootstrapMessage))
			{
				Log.Information(bootstrapMessage);
			}
		}
	}

	private void WriteStartupDiagnostics(string? dataDir, string? dbPath)
	{
		var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
		Log.Information("App startup diagnostics: Version={Version}, OS={OSVersion}, DataDir={DataDir}, SettingsPath={SettingsPath}, DbPath={DbPath}",
			assemblyVersion,
			Environment.OSVersion.VersionString,
			dataDir ?? "(unknown)",
			_settingsPath,
			dbPath ?? "(unknown)");
	}

	private static bool HasArgument(string[] args, string expected)
	{
		return args.Any(x => string.Equals(x, expected, StringComparison.OrdinalIgnoreCase));
	}
}
