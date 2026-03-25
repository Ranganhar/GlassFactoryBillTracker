using System.Globalization;
using System.Collections.Concurrent;
using GlassFactory.BillTracker.Domain.Entities;

namespace GlassFactory.BillTracker.App.Services;

public static class CustomerOrdering
{
    private static readonly CompareInfo ZhCnCompareInfo = new CultureInfo("zh-CN").CompareInfo;
    private static readonly ConcurrentDictionary<Guid, CachedSearchEntry> SearchKeyCache = new();

    public static List<Customer> SortCustomers(IEnumerable<Customer> customers)
    {
        return (customers ?? Enumerable.Empty<Customer>())
            .OrderBy(x => x.Name ?? string.Empty, Comparer<string>.Create(CompareCustomerName))
            .ThenBy(x => x.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Id)
            .ToList();
    }

    public static List<Customer> FilterAndSortCustomers(IEnumerable<Customer> customers, string? keyword)
    {
        var sorted = SortCustomers(customers);
        return FilterCustomersInOrder(sorted, keyword);
    }

    public static List<Customer> FilterCustomersInOrder(IEnumerable<Customer> sortedCustomers, string? keyword)
    {
        var ordered = (sortedCustomers ?? Enumerable.Empty<Customer>()).ToList();
        var normalizedQuery = CustomerSearchKeyBuilder.NormalizeQuery(keyword);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return ordered;
        }

        return ordered
            .Where(c => MatchCustomer(c, normalizedQuery))
            .ToList();
    }

    public static void UpsertSearchKeys(IEnumerable<Customer> customers)
    {
        foreach (var customer in customers ?? Enumerable.Empty<Customer>())
        {
            if (customer.Id == Guid.Empty)
            {
                continue;
            }

            SearchKeyCache.AddOrUpdate(
                customer.Id,
                static (_, c) => CachedSearchEntry.Create(c.Name),
                static (_, existing, c) => existing.UpdateIfChanged(c.Name),
                customer);
        }
    }

    public static void RemoveSearchKey(Guid customerId)
    {
        if (customerId == Guid.Empty)
        {
            return;
        }

        SearchKeyCache.TryRemove(customerId, out _);
    }

    private static int CompareCustomerName(string? a, string? b)
    {
        return ZhCnCompareInfo.Compare(a ?? string.Empty, b ?? string.Empty, CompareOptions.StringSort | CompareOptions.IgnoreCase);
    }

    private static bool MatchCustomer(Customer customer, string normalizedQuery)
    {
        if (customer.Id == Guid.Empty)
        {
            var transient = CustomerSearchKeyBuilder.Build(customer.Name);
            return IsMatch(transient, normalizedQuery);
        }

        var entry = SearchKeyCache.AddOrUpdate(
            customer.Id,
            static (_, c) => CachedSearchEntry.Create(c.Name),
            static (_, existing, c) => existing.UpdateIfChanged(c.Name),
            customer);

        return IsMatch(entry.Key, normalizedQuery);
    }

    private static bool IsMatch(CustomerSearchKey key, string normalizedQuery)
    {
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return true;
        }

        return key.OriginalNormalized.Contains(normalizedQuery, StringComparison.Ordinal)
               || key.FullPinyinNormalized.Contains(normalizedQuery, StringComparison.Ordinal)
               || key.InitialsNormalized.Contains(normalizedQuery, StringComparison.Ordinal);
    }

    private sealed record CachedSearchEntry(string NameSnapshot, CustomerSearchKey Key)
    {
        public static CachedSearchEntry Create(string? customerName)
        {
            var snapshot = customerName ?? string.Empty;
            return new CachedSearchEntry(snapshot, CustomerSearchKeyBuilder.Build(snapshot));
        }

        public CachedSearchEntry UpdateIfChanged(string? customerName)
        {
            var snapshot = customerName ?? string.Empty;
            if (string.Equals(NameSnapshot, snapshot, StringComparison.Ordinal))
            {
                return this;
            }

            return Create(snapshot);
        }
    }
}
