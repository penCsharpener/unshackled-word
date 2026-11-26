using UnshackledWord.Domain.Models.Grammar;

namespace UnshackledWord.Application.Features.GrammarSearch;

/// <summary>A distinct morphology code of the word tables with its decoded keys (<see cref="Aramaic"/> only for Hebrew codes).</summary>
public sealed record MorphologyCodeEntry(string Code, MorphologyKeys Keys, bool Aramaic = false);

/// <summary>All distinct morphology codes, decoded once and cached (roadmap 0029.1: Greek; the Hebrew codes join in 0029.6).</summary>
public interface IMorphologyCodeIndex
{
    Task<IReadOnlyList<MorphologyCodeEntry>> GetGreekAsync(CancellationToken token = default);
}

/// <summary>Turns the filters of one slot into the set of codes that satisfy every one of them (an empty set means no hits).</summary>
public sealed class GrammarFilterResolver
{
    private readonly IMorphologyCodeIndex _index;

    public GrammarFilterResolver(IMorphologyCodeIndex index)
    {
        _index = index;
    }

    public async Task<IReadOnlyList<string>> ResolveGreekAsync(IReadOnlyList<GrammarFilter> filters, CancellationToken token = default) =>
        Resolve(filters, await _index.GetGreekAsync(token), GrammarVocabulary.LanguageGreek);

    public Task<IReadOnlyList<MorphologyCodeEntry>> GreekCodesAsync(CancellationToken token = default) => _index.GetGreekAsync(token);

    /// <summary>Pure core: the codes of <paramref name="codes"/> (of language <paramref name="language"/>) that satisfy all filters.</summary>
    public static IReadOnlyList<string> Resolve(IReadOnlyList<GrammarFilter> filters, IEnumerable<MorphologyCodeEntry> codes, string language) =>
        [.. codes.Where(entry => filters.All(f => Holds(f, entry, language))).Select(e => e.Code)];

    private static bool Holds(GrammarFilter filter, MorphologyCodeEntry entry, string language)
    {
        var inSet = filter.Key.Kind switch
        {
            GrammarKeyKind.Morphology => Selected(filter, entry) is { } id && filter.Values.Any(v => v.Id == id),
            GrammarKeyKind.Language => filter.Values.Any(v => v.Id == language || (language == GrammarVocabulary.LanguageHebrew && entry.Aramaic && v.Id == GrammarVocabulary.LanguageAramaic)),
            GrammarKeyKind.Code => filter.CodePatterns.Any(p => Wildcard(entry.Code, p)),
            _ => false,
        };
        return filter.Negated ? !inSet : inSet;
    }

    private static string? Selected(GrammarFilter filter, MorphologyCodeEntry entry) => filter.Key.Selector?.Invoke(entry.Keys);

    /// <summary>Case-insensitive match of <paramref name="code"/> against <paramref name="pattern"/>, where <c>*</c> stands for any run of characters.</summary>
    public static bool Wildcard(string code, string pattern)
    {
        var parts = pattern.Split('*');
        if (parts.Length == 1) return string.Equals(code, pattern, StringComparison.OrdinalIgnoreCase);

        if (!code.StartsWith(parts[0], StringComparison.OrdinalIgnoreCase)) return false;
        var position = parts[0].Length;
        for (var i = 1; i < parts.Length - 1; i++)
        {
            var found = code.IndexOf(parts[i], position, StringComparison.OrdinalIgnoreCase);
            if (found < 0) return false;
            position = found + parts[i].Length;
        }

        var last = parts[^1];
        return code.Length - position >= last.Length && code.EndsWith(last, StringComparison.OrdinalIgnoreCase);
    }
}
