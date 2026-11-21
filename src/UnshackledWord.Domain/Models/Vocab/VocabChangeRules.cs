namespace UnshackledWord.Domain.Models.Vocab;

/// <summary>Kinds of <c>VocabEntryChanges</c> rows (docs/roadmaps/0022-vocab-trainer/04-study-engine.md "Lexicon changes under active cards").</summary>
public static class VocabChangeKinds
{
    public const string Meanings = "meanings";
    public const string Form = "form";
    public const string Lemma = "lemma";
    public const string Keys = "keys";
    public const string Merge = "merge";
    public const string Split = "split";
    public const string Retire = "retire";
    public const string Delete = "delete";
}

/// <summary>Where a dictionary form came from (<c>VocabEntries.DictionaryFormOrigin</c>).</summary>
public static class VocabFormOrigins
{
    public const string Generated = "generated";
    public const string Agent = "agent";
    public const string Manual = "manual";
}

/// <summary>Result of <see cref="VocabChangeRules.PlanForm"/>.</summary>
public sealed record VocabFormPlan(string? DictionaryForm, string? Origin, bool Changed);

/// <summary>
/// Pure rules of the vocab editor (roadmap 0022.4): which edits are learning-relevant and therefore bump <c>ContentVersion</c> and write a
/// change row. Bumping: the accepted meaning set (case-sensitive German texts, order does not count), the dictionary form, the lemma, keys
/// added or removed, merge, split, retire and delete. Not bumping: ranks and order of meanings, pinning, <c>ReviewedAt</c>, transliteration
/// and audio.
/// </summary>
public static class VocabChangeRules
{
    /// <summary>The set of accepted German texts differs (ordinal, case-sensitive). Order and duplicates are ignored.</summary>
    public static bool AcceptedSetChanged(IEnumerable<string> before, IEnumerable<string> after) =>
        !before.ToHashSet(StringComparer.Ordinal).SetEquals(after);

    /// <summary>The dictionary form text differs (ordinal). A change of the origin alone is not learning-relevant.</summary>
    public static bool FormChanged(string? before, string? after) => !string.Equals(before, after, StringComparison.Ordinal);

    /// <summary>The set of Strong's keys differs (ordinal).</summary>
    public static bool KeysChanged(IEnumerable<string> before, IEnumerable<string> after) =>
        !before.ToHashSet(StringComparer.Ordinal).SetEquals(after);

    /// <summary>
    /// A requested dictionary form against the stored one. Blank means no form. An unchanged text keeps its origin (a generated form stays
    /// generated); a new text is <c>manual</c>; no form has no origin (the table requires both or neither).
    /// </summary>
    public static VocabFormPlan PlanForm(string? currentForm, string? currentOrigin, string? requestedForm)
    {
        var requested = string.IsNullOrWhiteSpace(requestedForm) ? null : requestedForm.Trim();

        if (!FormChanged(currentForm, requested))
        {
            return new VocabFormPlan(currentForm, currentOrigin, false);
        }

        return new VocabFormPlan(requested, requested is null ? null : VocabFormOrigins.Manual, true);
    }

    /// <summary>
    /// The new rank of every meaning for a requested order: 1..n in the requested order. Null when the requested ids are not exactly the
    /// current ids (missing, extra or duplicated).
    /// </summary>
    public static Dictionary<int, int>? PlanMeaningOrder(IReadOnlyCollection<int> currentIds, IReadOnlyList<int> requestedIds)
    {
        if (requestedIds.Count != currentIds.Count || !requestedIds.ToHashSet().SetEquals(currentIds) || requestedIds.Distinct().Count() != requestedIds.Count)
        {
            return null;
        }

        var ranks = new Dictionary<int, int>(requestedIds.Count);

        for (var i = 0; i < requestedIds.Count; i++)
        {
            ranks[requestedIds[i]] = i + 1;
        }

        return ranks;
    }

    /// <summary>Strong's keys look like <c>G3056</c> or <c>H1254B</c>.</summary>
    public static bool TryParseKey(string key, out char lang, out int number)
    {
        lang = '\0';
        number = 0;

        if (key.Length < 2 || (key[0] != 'G' && key[0] != 'H'))
        {
            return false;
        }

        var end = 1;
        while (end < key.Length && char.IsAsciiDigit(key[end]))
        {
            end++;
        }

        if (end == 1 || end < key.Length - 1 || (end == key.Length - 1 && !char.IsAsciiLetterUpper(key[end])) || !int.TryParse(key.AsSpan(1, end - 1), out number))
        {
            return false;
        }

        lang = key[0];

        return true;
    }

    /// <summary>Hebrew prefix and suffix morphemes H9001-H9049 are listed but never ranked.</summary>
    public static bool IsMorphemeKey(string key) => TryParseKey(key, out var lang, out var number) && lang == 'H' && number is >= 9001 and <= 9049;
}
