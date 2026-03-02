namespace GlassFactory.BillTracker.App.Models;

public sealed class LookupItem<T>
{
    public T Value { get; set; } = default!;
    public string DisplayName { get; set; } = string.Empty;
}
