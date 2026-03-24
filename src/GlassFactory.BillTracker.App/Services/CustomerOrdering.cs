using System.Globalization;
using GlassFactory.BillTracker.Domain.Entities;

namespace GlassFactory.BillTracker.App.Services;

public static class CustomerOrdering
{
    private static readonly CompareInfo ZhCnCompareInfo = new CultureInfo("zh-CN").CompareInfo;
    private static readonly Dictionary<char, string> CommonCharPinyin = new()
    {
        ['张'] = "zhang",
        ['赵'] = "zhao",
        ['周'] = "zhou",
        ['郑'] = "zheng",
        ['朱'] = "zhu",
        ['陈'] = "chen",
        ['程'] = "cheng",
        ['崔'] = "cui",
        ['曹'] = "cao",
        ['常'] = "chang",
        ['李'] = "li",
        ['刘'] = "liu",
        ['吕'] = "lv",
        ['梁'] = "liang",
        ['林'] = "lin",
        ['罗'] = "luo",
        ['陆'] = "lu",
        ['雷'] = "lei",
        ['王'] = "wang",
        ['吴'] = "wu",
        ['魏'] = "wei",
        ['文'] = "wen",
        ['温'] = "wen",
        ['许'] = "xu",
        ['徐'] = "xu",
        ['谢'] = "xie",
        ['肖'] = "xiao",
        ['孙'] = "sun",
        ['沈'] = "shen",
        ['施'] = "shi",
        ['石'] = "shi",
        ['宋'] = "song",
        ['苏'] = "su",
        ['胡'] = "hu",
        ['黄'] = "huang",
        ['何'] = "he",
        ['韩'] = "han",
        ['贺'] = "he",
        ['郭'] = "guo",
        ['高'] = "gao",
        ['龚'] = "gong",
        ['马'] = "ma",
        ['孟'] = "meng",
        ['苗'] = "miao",
        ['莫'] = "mo",
        ['潘'] = "pan",
        ['彭'] = "peng",
        ['裴'] = "pei",
        ['齐'] = "qi",
        ['钱'] = "qian",
        ['秦'] = "qin",
        ['乔'] = "qiao",
        ['邱'] = "qiu",
        ['任'] = "ren",
        ['冉'] = "ran",
        ['邵'] = "shao",
        ['田'] = "tian",
        ['唐'] = "tang",
        ['谭'] = "tan",
        ['陶'] = "tao",
        ['袁'] = "yuan",
        ['余'] = "yu",
        ['于'] = "yu",
        ['严'] = "yan",
        ['杨'] = "yang",
        ['叶'] = "ye",
        ['曾'] = "zeng",
        ['钟'] = "zhong",
        ['邹'] = "zou"
    };

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
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return sorted;
        }

        var term = keyword.Trim();
        return sorted
            .Where(c => MatchCustomer(c, term))
            .ToList();
    }

    private static int CompareCustomerName(string? a, string? b)
    {
        return ZhCnCompareInfo.Compare(a ?? string.Empty, b ?? string.Empty, CompareOptions.StringSort | CompareOptions.IgnoreCase);
    }

    private static bool MatchCustomer(Customer customer, string term)
    {
        var name = customer.Name ?? string.Empty;
        if (name.Contains(term, StringComparison.CurrentCultureIgnoreCase))
        {
            return true;
        }

        if (!IsAsciiKeyword(term))
        {
            return false;
        }

        var loweredTerm = term.ToLowerInvariant();
        var pinyin = BuildMappedPinyin(name);
        if (!string.IsNullOrWhiteSpace(pinyin) && pinyin.Contains(loweredTerm, StringComparison.Ordinal))
        {
            return true;
        }

        var initials = BuildMappedInitials(name);
        if (!string.IsNullOrWhiteSpace(initials) && initials.Contains(loweredTerm, StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private static bool IsAsciiKeyword(string value)
    {
        foreach (var ch in value)
        {
            if (ch > 127)
            {
                return false;
            }
        }

        return true;
    }

    private static string BuildMappedPinyin(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var parts = new List<string>();
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch))
            {
                parts.Add(char.ToLowerInvariant(ch).ToString());
                continue;
            }

            if (CommonCharPinyin.TryGetValue(ch, out var py))
            {
                parts.Add(py);
            }
        }

        return string.Concat(parts);
    }

    private static string BuildMappedInitials(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var initials = new List<char>();
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch))
            {
                initials.Add(char.ToLowerInvariant(ch));
                continue;
            }

            if (CommonCharPinyin.TryGetValue(ch, out var py) && !string.IsNullOrWhiteSpace(py))
            {
                initials.Add(py[0]);
            }
        }

        return new string(initials.ToArray());
    }
}
