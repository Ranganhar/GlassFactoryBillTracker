namespace GlassFactory.BillTracker.App.ViewModels.Rows;

public sealed class CustomerListItemViewModel
{
    public Guid? Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsAllCustomers { get; init; }

    public override string ToString()
    {
        return Name;
    }
}
