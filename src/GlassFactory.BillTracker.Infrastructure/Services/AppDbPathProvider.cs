using GlassFactory.BillTracker.Infrastructure.Abstractions;

namespace GlassFactory.BillTracker.Infrastructure.Services;

public class AppDbPathProvider : IAppDbPathProvider
{
    public string GetDbPath(string dataDir)
    {
        return Path.Combine(dataDir, "billtracker.db");
    }
}
