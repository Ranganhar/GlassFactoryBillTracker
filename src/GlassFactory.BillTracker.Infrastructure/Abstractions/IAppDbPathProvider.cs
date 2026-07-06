namespace GlassFactory.BillTracker.Infrastructure.Abstractions;

public interface IAppDbPathProvider
{
    string GetDbPath(string dataDir);
}
