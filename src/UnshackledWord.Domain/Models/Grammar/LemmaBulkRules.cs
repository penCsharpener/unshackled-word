namespace UnshackledWord.Domain.Models.Grammar;

/// <summary>Pure rules of the bulk lemma edit (limits, text hygiene, scope resolution).</summary>
public static class LemmaBulkRules
{
    public const int MaxLemmaLength = 255;
    public const int MaxFromLemmas = 20;
    public const int MaxApplyWords = 20_000;
    public const int MaxNoteLength = 500;
    public const int DefaultPageSize = 100;
    public const int MaxPageSize = 500;
    public const int ContextWords = 5;
    public const int MaxAutocompleteLimit = 50;
    public const int DefaultAutocompleteLimit = 20;

    public const string TestamentOld = "ot";
    public const string TestamentNew = "nt";

    /// <summary>Lemmas are compared exact and case-sensitive, so the only normalisation is trimming.</summary>
    public static string Normalize(string? lemma) => (lemma ?? string.Empty).Trim();

    /// <summary>1-255 characters after trimming and no control characters.</summary>
    public static bool IsValidLemma(string? lemma)
    {
        var l = Normalize(lemma);
        return l.Length is >= 1 and <= MaxLemmaLength && !l.Any(char.IsControl);
    }

    /// <summary>Trimmed, de-duplicated (ordinal) source lemmas in the order given.</summary>
    public static List<string> NormalizeFrom(IEnumerable<string>? from) =>
        (from ?? []).Select(Normalize).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>Inclusive BibleBookId range for the scope. Testament wins over nothing; bookId wins over testament.</summary>
    public static (int From, int To) BookRange(string? testament, int? bookId)
    {
        if (bookId is { } b) return (b, b);
        return testament switch
        {
            TestamentOld => (1, 39),
            TestamentNew => (40, 66),
            _ => (1, 66),
        };
    }

    /// <summary>Escapes LIKE wildcards (backslash is the default escape character) and appends the prefix wildcard.</summary>
    public static string LikePrefix(string q) =>
        q.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal) + "%";

    /// <summary>Ids of the rows whose current lemma is not one of the source lemmas (or that have no lemma at all).</summary>
    public static List<int> FindStale(IEnumerable<int> requested, IReadOnlyDictionary<int, string> currentLemmaByWordId, IReadOnlyCollection<string> from)
    {
        var fromSet = from.ToHashSet(StringComparer.Ordinal);
        return requested.Where(id => !currentLemmaByWordId.TryGetValue(id, out var lemma) || !fromSet.Contains(lemma)).ToList();
    }
}
