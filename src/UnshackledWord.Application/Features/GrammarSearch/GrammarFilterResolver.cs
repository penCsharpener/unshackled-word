using UnshackledWord.Domain.Models.Grammar;

namespace UnshackledWord.Application.Features.GrammarSearch;

/// <summary>A distinct morphology code of the word tables with its decoded keys (<see cref="Aramaic"/> only for Hebrew codes).</summary>
public sealed record MorphologyCodeEntry(string Code, MorphologyKeys Keys, bool Aramaic = false);

/// <summary>All distinct morphology codes of the Greek and the Hebrew/Aramaic words, decoded once and cached (roadmap 0029.1, 0029.6).</summary>
public interface IMorphologyCodeIndex
{
    Task<IReadOnlyList<MorphologyCodeEntry>> GetGreekAsync(CancellationToken token = default);

    /// <summary>Hebrew and Aramaic codes (<c>"Grammar"</c>), decoded to the keys of the root segment, with the Aramaic flag.</summary>
    Task<IReadOnlyList<MorphologyCodeEntry>> GetHebrewAsync(CancellationToken token = default);
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

    public async Task<IReadOnlyList<string>> ResolveHebrewAsync(IReadOnlyList<GrammarFilter> filters, CancellationToken token = default) =>
        Resolve(filters, await _index.GetHebrewAsync(token), GrammarVocabulary.LanguageHebrew);

    public Task<IReadOnlyList<MorphologyCodeEntry>> GreekCodesAsync(CancellationToken token = default) => _index.GetGreekAsync(token);

    public Task<IReadOnlyList<MorphologyCodeEntry>> HebrewCodesAsync(CancellationToken token = default) => _index.GetHebrewAsync(token);

    /// <summary>Pure core: the codes of <paramref name="codes"/> (of language <paramref name="language"/>) that satisfy all filters.</summary>
    public static IReadOnlyList<string> Resolve(IReadOnlyList<GrammarFilter> filters, IEnumerable<MorphologyCodeEntry> codes, string language) =>
        [.. codes.Where(entry => filters.All(f => Holds(f, entry, language))).Select(e => e.Code)];

    /// <summary>Does the key exist for words of this code's language? Hebrew codes marked Aramaic need the Aramaic flag of the key.</summary>
    private static bool Applies(GrammarKeyDef key, MorphologyCodeEntry entry, string language)
    {
        var flag = language == GrammarVocabulary.LanguageGreek ? GrammarLanguages.Greek : entry.Aramaic ? GrammarLanguages.Aramaic : GrammarLanguages.Hebrew;
        return key.Languages.HasFlag(flag);
    }

    private static bool Holds(GrammarFilter filter, MorphologyCodeEntry entry, string language)
    {
        // A key that does not exist in the language matches nothing there, negated or not.
        if (!Applies(filter.Key, entry, language)) return false;

        var inSet = filter.Key.Kind switch
        {
            GrammarKeyKind.Morphology => Selected(filter, entry) is { } id && filter.Values.Any(v => v.Id == id),
            GrammarKeyKind.Language => filter.Values.Any(v => v.Id == (language == GrammarVocabulary.LanguageHebrew && entry.Aramaic ? GrammarVocabulary.LanguageAramaic : language)),
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
