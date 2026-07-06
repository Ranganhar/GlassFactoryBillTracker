using GlassFactory.BillTracker.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GlassFactory.BillTracker.App.Services;

public static class AppRuntimeContext
{
    public static string DataDir { get; private set; } = string.Empty;
    public static string DbPath { get; private set; } = string.Empty;

    public static void Initialize(string dataDir, string dbPath)
    {
        DataDir = dataDir;
        DbPath = dbPath;
    }

    public static BillTrackerDbContext CreateDbContext()
    {
        if (string.IsNullOrWhiteSpace(DbPath))
        {
            throw new InvalidOperationException("应用数据库路径尚未初始化。");
        }

        var optionsBuilder = new DbContextOptionsBuilder<BillTrackerDbContext>();
        optionsBuilder.UseSqlite($"Data Source={DbPath}");
        return new BillTrackerDbContext(optionsBuilder.Options);
    }
}
