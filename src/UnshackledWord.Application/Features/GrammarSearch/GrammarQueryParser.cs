namespace UnshackledWord.Application.Features.GrammarSearch;

public static class GrammarErrorCodes
{
    public const string UnknownKey = "unknownKey";
    public const string UnknownValue = "unknownValue";
    public const string AmbiguousValue = "ambiguousValue";
    public const string TooManySlots = "tooManySlots";
    public const string TooManyFilters = "tooManyFilters";
    public const string EmptyQuery = "emptyQuery";
    public const string UnknownWord = "unknownWord";
    public const string UnknownSlot = "unknownSlot";
}

/// <summary>A query that cannot be searched: machine-readable <see cref="Code"/>, the offending <see cref="Token"/> and the valid <see cref="Candidates"/> (when there are any).</summary>
public sealed record GrammarQueryError(string Code, string? Token = null, IReadOnlyList<string>? Candidates = null);

/// <summary>How a word token is read: by the order of roadmap 0029 (<see cref="Auto"/>) or forced by <c>Strong:</c> / <c>Lemma:</c>.</summary>
public enum GrammarWordMode
{
    Auto,
    Strongs,
    Lemma,
}

/// <summary>One parsed filter token. <see cref="Values"/> are the matched value definitions (OR); <see cref="CodePatterns"/> the upper-cased patterns of a <c>Code</c> filter (OR).</summary>
public sealed record GrammarFilter(
    string Text,
    GrammarKeyDef Key,
    IReadOnlyList<GrammarValueDef> Values,
    IReadOnlyList<string> CodePatterns,
    bool Negated,
    bool German);

/// <summary>A word position of the verse: an optional word token plus the filters that bind to it. Without <see cref="Word"/> it is the "any word" slot.</summary>
public sealed record GrammarSlotSpec(string Text, string? Word, GrammarWordMode Mode, IReadOnlyList<GrammarFilter> Filters);

public sealed record GrammarParseResult(IReadOnlyList<GrammarSlotSpec> Slots, GrammarQueryError? Error)
{
    public bool Ok => Error is null;
}

/// <summary>
/// Pure parser of the grammar query language (roadmap 0029, 03-query-language): whitespace tokens, a word token opens a slot, a filter binds to the nearest
/// preceding word (filters before the first word form an any-word slot), comma = OR, leading <c>-</c> = exclude. Keys and aliases match exactly after
/// folding, values by unique prefix where an exact label wins.
/// </summary>
public static class GrammarQueryParser
{
    public const int MaxSlots = 4;
    public const int MaxFilters = 12;
    public const int MaxQueryLength = 200;

    private sealed class SlotBuilder
    {
        public SlotBuilder(string? word, GrammarWordMode mode, string firstToken)
        {
            Word = word;
            Mode = mode;
            Tokens.Add(firstToken);
        }

        public string? Word { get; }
        public GrammarWordMode Mode { get; }
        public List<string> Tokens { get; } = [];
        public List<GrammarFilter> Filters { get; } = [];

        public GrammarSlotSpec Build() => new(string.Join(' ', Tokens), Word, Mode, Filters);
    }

    /// <param name="query">The query text.</param>
    /// <param name="allowEmptyValue">Facet requests: a filter without value (<c>Kasus:</c>) is kept in its slot's text but adds no filter, instead of failing with <c>unknownValue</c>.</param>
    public static GrammarParseResult Parse(string? query, bool allowEmptyValue = false)
    {
        var tokens = (query ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return Fail(new GrammarQueryError(GrammarErrorCodes.EmptyQuery));

        var slots = new List<SlotBuilder>();
        var filterCount = 0;
        foreach (var token in tokens)
        {
            var negated = token.Length > 1 && token[0] == '-';
            var body = negated ? token[1..] : token;
            var german = body.StartsWith(GrammarVocabulary.GermanPrefix, StringComparison.OrdinalIgnoreCase);
            if (german) body = body[GrammarVocabulary.GermanPrefix.Length..];

            var colon = body.IndexOf(':', StringComparison.Ordinal);
            if (colon < 0)
            {
                // A plain word; a negated or dt. word is not part of the language.
                if (negated || german) return Fail(new GrammarQueryError(GrammarErrorCodes.UnknownWord, token));
                if (slots.Count >= MaxSlots) return Fail(new GrammarQueryError(GrammarErrorCodes.TooManySlots, token));
                slots.Add(new SlotBuilder(body, GrammarWordMode.Auto, token));
                continue;
            }

            var keyName = body[..colon];
            var valueText = body[(colon + 1)..];
            var key = GrammarVocabulary.FindKey(keyName, german);
            if (key is null) return Fail(new GrammarQueryError(GrammarErrorCodes.UnknownKey, token, GrammarVocabulary.KeyNames(german)));

            if (key.Kind == GrammarKeyKind.Word)
            {
                if (negated) return Fail(new GrammarQueryError(GrammarErrorCodes.UnknownKey, token, GrammarVocabulary.KeyNames(german)));
                if (valueText.Length == 0) return Fail(new GrammarQueryError(GrammarErrorCodes.UnknownValue, token));
                if (slots.Count >= MaxSlots) return Fail(new GrammarQueryError(GrammarErrorCodes.TooManySlots, token));
                slots.Add(new SlotBuilder(valueText, key.Name == "Strong" ? GrammarWordMode.Strongs : GrammarWordMode.Lemma, token));
                continue;
            }

            if (++filterCount > MaxFilters) return Fail(new GrammarQueryError(GrammarErrorCodes.TooManyFilters, token));
            if (allowEmptyValue && valueText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Length == 0)
            {
                if (slots.Count == 0) slots.Add(new SlotBuilder(null, GrammarWordMode.Auto, token));
                else slots[^1].Tokens.Add(token);
                continue;
            }

            var parsed = ParseFilter(token, key, valueText, negated, german, out var error);
            if (parsed is null) return Fail(error!);

            if (slots.Count == 0) slots.Add(new SlotBuilder(null, GrammarWordMode.Auto, token));
            else slots[^1].Tokens.Add(token);
            slots[^1].Filters.Add(parsed);
        }

        return new GrammarParseResult([.. slots.Select(s => s.Build())], null);
    }

    private static GrammarFilter? ParseFilter(string token, GrammarKeyDef key, string valueText, bool negated, bool german, out GrammarQueryError? error)
    {
        error = null;
        var parts = valueText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            error = new GrammarQueryError(GrammarErrorCodes.UnknownValue, token, key.Values.Select(v => v.De).ToList());
            return null;
        }

        if (key.Kind == GrammarKeyKind.Code)
        {
            return new GrammarFilter(token, key, [], [.. parts.Select(p => p.ToUpperInvariant()).Distinct()], negated, german);
        }

        var values = new List<GrammarValueDef>();
        foreach (var part in parts)
        {
            var value = MatchValue(key, part, token, out error);
            if (value is null) return null;
            if (!values.Contains(value)) values.Add(value);
        }

        return new GrammarFilter(token, key, values, [], negated, german);
    }

    private static GrammarValueDef? MatchValue(GrammarKeyDef key, string typed, string token, out GrammarQueryError? error)
    {
        error = null;
        var folded = GrammarVocabulary.Fold(typed);
        var exact = key.Values.Where(v => Labels(v).Any(l => l == folded)).ToList();
        if (exact.Count == 1) return exact[0];

        var prefix = key.Values.Where(v => Labels(v).Any(l => l.StartsWith(folded, StringComparison.Ordinal))).ToList();
        if (prefix.Count == 1) return prefix[0];

        error = prefix.Count == 0
            ? new GrammarQueryError(GrammarErrorCodes.UnknownValue, token, key.Values.Select(v => v.De).ToList())
            : new GrammarQueryError(GrammarErrorCodes.AmbiguousValue, token, prefix.Select(v => v.De).ToList());
        return null;
    }

    private static IEnumerable<string> Labels(GrammarValueDef value) =>
        new[] { value.De }.Concat(value.Aliases ?? []).Select(GrammarVocabulary.Fold);

    private static GrammarParseResult Fail(GrammarQueryError error) => new([], error);
}
