using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GlassFactory.BillTracker.Data.Persistence;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<BillTrackerDbContext>
{
    public BillTrackerDbContext CreateDbContext(string[] args)
    {
        var dataDir = Environment.GetEnvironmentVariable("BILLTRACKER_DATA_DIR");
        if (string.IsNullOrWhiteSpace(dataDir))
        {
            dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GlassFactoryBillTracker", "data");
        }

        Directory.CreateDirectory(dataDir);
        var dbPath = Path.Combine(dataDir, "billtracker.db");

        var optionsBuilder = new DbContextOptionsBuilder<BillTrackerDbContext>();
        optionsBuilder.UseSqlite($"Data Source={dbPath}");

        return new BillTrackerDbContext(optionsBuilder.Options);
    }
}
