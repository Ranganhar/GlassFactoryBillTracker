using TinyPinyin;

namespace GlassFactory.BillTracker.App.Services;

public sealed class CustomerSearchKey
{
    public string OriginalNormalized { get; init; } = string.Empty;
    public string FullPinyinNormalized { get; init; } = string.Empty;
    public string InitialsNormalized { get; init; } = string.Empty;
}

public static class CustomerSearchKeyBuilder
{
    // TinyPinyin.Net package is used to generate pinyin/full initials for Chinese text.
    public static string NormalizeQuery(string? q)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return string.Empty;
        }

        var chars = q
            .Trim()
            .ToLowerInvariant()
            .Where(ch => !char.IsWhiteSpace(ch) && ch != '-' && ch != '_')
            .ToArray();

        return new string(chars);
    }

    public static CustomerSearchKey Build(string? customerName)
    {
        var name = customerName ?? string.Empty;
        var normalizedOriginal = NormalizeQuery(name);

        string fullPinyin;
        string initials;
        try
        {
            fullPinyin = NormalizeQuery(PinyinHelper.GetPinyin(name));
            initials = NormalizeQuery(PinyinHelper.GetPinyinInitials(name));
        }
        catch
        {
            fullPinyin = string.Empty;
            initials = string.Empty;
        }

        return new CustomerSearchKey
        {
            OriginalNormalized = normalizedOriginal,
            FullPinyinNormalized = fullPinyin,
            InitialsNormalized = initials
        };
    }
}
