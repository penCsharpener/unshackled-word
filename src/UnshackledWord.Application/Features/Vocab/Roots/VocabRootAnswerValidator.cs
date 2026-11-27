using System.Text.RegularExpressions;
using UnshackledWord.Domain.Models.Vocab;

namespace UnshackledWord.Application.Features.Vocab.Roots;

public enum VocabRootVerdictKind
{
    /// <summary>The answer may be written (<see cref="VocabRootVerdict.Plan"/> is set).</summary>
    Accept,

    /// <summary>The key has a manual row, or a prefill row while the check option is off: the answer is ignored.</summary>
    Dropped,

    /// <summary>A rule is broken (invented key, wrong letters ...): not written, asked once more with the problem, then listed in the run report.</summary>
    Rejected,

    /// <summary>The answer is well-formed but unsure or does not concatenate: not written, listed in the run report for the maintainer.</summary>
    Flagged,
}

/// <summary>What an accepted answer writes: the root (text and key), the stripped prepositions and whether the word is its own root.</summary>
public sealed record VocabRootPlan(string RootText, string? RootKey, string[] PrefixKeys, string? Prefixes, bool IsSimple);

public sealed record VocabRootVerdict(VocabRootVerdictKind Kind, string? Reason = null, VocabRootPlan? Plan = null);

/// <summary>The lookups the validator needs, built once per run: the Greek lexicon lemmas by key and the Hebrew verb keys with their consonants.</summary>
public sealed class VocabRootValidationContext
{
    private readonly Dictionary<string, List<VocabRootLexiconLemma>> _greek;
    private readonly Dictionary<string, string> _hebrewVerbs;

    public VocabRootValidationContext(IEnumerable<VocabRootLexiconLemma> greekLexicon, IReadOnlyDictionary<string, string>? hebrewVerbConsonants = null)
    {
        _greek = greekLexicon.GroupBy(l => l.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        _hebrewVerbs = hebrewVerbConsonants?.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal) ?? [];
    }

    public bool HasGreekKey(string key) => _greek.ContainsKey(key);

    /// <summary>Normalized written forms of the Greek word with this key (lexicon form and vocab lemma).</summary>
    public IReadOnlyList<string> GreekForms(string key) => _greek.TryGetValue(key, out var list) ? list.Select(l => l.Normalized).Distinct().ToList() : [];

    /// <summary>Display lemma of the key, the vocab lemma when the word is in the vocab.</summary>
    public string? GreekDisplay(string key) =>
        _greek.TryGetValue(key, out var list) ? (list.FirstOrDefault(l => l.InVocab) ?? list[0]).Display : null;

    /// <summary>Consonants of the verb with this key; null when the key is not a Hebrew/Aramaic verb.</summary>
    public string? HebrewVerbConsonants(string key) => _hebrewVerbs.TryGetValue(key, out var consonants) ? consonants : null;
}

/// <summary>
/// The rules of run mode <c>vocab-root</c> (roadmap 0030.4, docs/roadmaps/0030-vocab-roots/04-prefill-and-ai.md). Pure: the caller loads the
/// lookups and writes the accepted plans. Greek: every prefix key is in the preposition table of <see cref="VocabRootPrefill"/>; the root key exists
/// in the lexicon; with prefixes the root is not the word itself; prefix and root plausibly concatenate to the word (normalized, elision allowed),
/// otherwise the entry is flagged. Hebrew: 2 to 4 root consonants; an optional root key is a verb whose consonants equal the letters. A manual row
/// is never touched, a prefill row only with <c>checkPrefill</c>.
/// </summary>
public static partial class VocabRootAnswerValidator
{
    public const int MinHebrewRootLetters = 2;
    public const int MaxHebrewRootLetters = 4;

    public static VocabRootVerdict Check(char track, VocabRootInput entry, VocabRootAnswerRow answer, VocabRootValidationContext context, bool checkPrefill)
    {
        if (entry.Existing?.Origin == "manual" || (entry.Existing?.Origin == "prefill" && !checkPrefill))
        {
            return new VocabRootVerdict(VocabRootVerdictKind.Dropped, $"Row is {entry.Existing?.Origin}.");
        }

        return track == 'G' ? CheckGreek(entry, answer, context) : CheckHebrew(entry, answer, context);
    }

    private static VocabRootVerdict CheckGreek(VocabRootInput entry, VocabRootAnswerRow answer, VocabRootValidationContext context)
    {
        var prefixKeys = answer.Prefixes.Select(p => p.Trim().ToUpperInvariant()).ToArray();

        foreach (var key in prefixKeys)
        {
            if (!VocabRootPrefill.IsPrepositionKey(key))
            {
                return Rejected($"Prefix key {key} is not in the preposition table.");
            }
        }

        if (answer.Simple)
        {
            return prefixKeys.Length > 0
                ? Rejected("A simple word has no prefixes.")
                : Accept(new VocabRootPlan(entry.Lemma, entry.MainKey, [], null, true), answer);
        }

        if (answer.RootKey is null)
        {
            return Rejected("rootKey is missing.");
        }

        var rootKey = answer.RootKey.ToUpperInvariant();

        if (!context.HasGreekKey(rootKey))
        {
            return Rejected($"rootKey {rootKey} does not exist in the Greek lexicon.");
        }

        if (prefixKeys.Length > 0 && rootKey == entry.MainKey)
        {
            return Rejected("The root is the word itself although prefixes are listed.");
        }

        if (prefixKeys.Length == 0 && rootKey == entry.MainKey)
        {
            return Accept(new VocabRootPlan(entry.Lemma, entry.MainKey, [], null, true), answer);
        }

        var word = VocabText.Normalize(entry.Lemma);

        if (!Concatenates(word, prefixKeys, 0, context.GreekForms(rootKey)))
        {
            return Flag($"{(prefixKeys.Length == 0 ? "The root" : "Prefixes and root")} do not plausibly form {entry.Lemma}.", answer);
        }

        var prefixes = prefixKeys.Length == 0 ? null : string.Join(" + ", prefixKeys.Select(k => VocabRootPrefill.PrepositionDictionaryForm(k)));

        return Accept(new VocabRootPlan(context.GreekDisplay(rootKey)!, rootKey, prefixKeys, prefixes, false), answer);
    }

    private static VocabRootVerdict CheckHebrew(VocabRootInput entry, VocabRootAnswerRow answer, VocabRootValidationContext context)
    {
        if (answer.Prefixes.Count > 0)
        {
            return Rejected("Hebrew roots have no prefixes.");
        }

        var letters = answer.Simple
            ? VocabRootPrefill.Consonants(entry.Lemma)
            : answer.RootLetters is null ? string.Empty : VocabRootPrefill.Consonants(answer.RootLetters);

        if (!answer.Simple && answer.RootLetters is null)
        {
            return Rejected("rootLetters is missing.");
        }

        if (letters.Length is < MinHebrewRootLetters or > MaxHebrewRootLetters)
        {
            return Rejected($"rootLetters must be {MinHebrewRootLetters} to {MaxHebrewRootLetters} consonants, got {letters.Length}.");
        }

        string? rootKey = null;

        if (!answer.Simple && answer.RootKey is not null)
        {
            rootKey = answer.RootKey.ToUpperInvariant();

            if (context.HebrewVerbConsonants(rootKey) is not { } verbLetters)
            {
                return Rejected($"rootKey {rootKey} is not a Hebrew or Aramaic verb.");
            }

            if (verbLetters != letters)
            {
                return Rejected($"rootKey {rootKey} has the consonants {verbLetters}, not {letters}.");
            }
        }

        return Accept(new VocabRootPlan(letters, rootKey, [], null, answer.Simple), answer);
    }

    /// <summary>Low confidence is never written: the maintainer decides from the run report.</summary>
    private static VocabRootVerdict Accept(VocabRootPlan plan, VocabRootAnswerRow answer) =>
        answer.Confidence.Equals("low", StringComparison.OrdinalIgnoreCase)
            ? Flag("The model marked the answer as low confidence." + (answer.Note is null ? string.Empty : " " + answer.Note), answer)
            : new VocabRootVerdict(VocabRootVerdictKind.Accept, null, plan);

    private static VocabRootVerdict Rejected(string reason) => new(VocabRootVerdictKind.Rejected, reason);

    private static VocabRootVerdict Flag(string reason, VocabRootAnswerRow answer) =>
        new(VocabRootVerdictKind.Flagged, answer.Note is null || reason.Contains(answer.Note, StringComparison.Ordinal) ? reason : $"{reason} Note: {answer.Note}");

    /// <summary>The word (normalized) starts with the written forms of the prefixes in order, and what is left is the root (elision, augment and contraction allowed).</summary>
    private static bool Concatenates(string word, string[] prefixKeys, int index, IReadOnlyList<string> rootForms)
    {
        if (index == prefixKeys.Length)
        {
            return rootForms.Any(root => Similar(word, root));
        }

        foreach (var form in VocabRootPrefill.PrepositionForms(prefixKeys[index]))
        {
            if (word.StartsWith(form, StringComparison.Ordinal) && Concatenates(word[form.Length..], prefixKeys, index + 1, rootForms))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Similar(string rest, string root)
    {
        if (rest.Length == 0 || root.Length == 0)
        {
            return false;
        }

        return Close(rest, root) || Close(StripEnding(rest), StripEnding(root))
            // augment and contraction at the seam: ἐξ + ἔρχομαι, but also εἰσ + ἄγω with a changed vowel
            || (IsVowel(rest[0]) && IsVowel(root[0]) && rest.Length > 1 && root.Length > 1 && (Close(rest[1..], root[1..]) || Close(StripEnding(rest[1..]), StripEnding(root[1..]))));
    }

    // one is the start of the other and at least two thirds of it (so ἀγω does not pass for ἀγαπάω)
    private static bool Close(string a, string b) =>
        a.Length >= 2 && b.Length >= 2 && Math.Min(a.Length, b.Length) * 3 >= Math.Max(a.Length, b.Length) * 2
        && (a.StartsWith(b, StringComparison.Ordinal) || b.StartsWith(a, StringComparison.Ordinal));

    private static string StripEnding(string text) => VerbEndingRegex().Replace(text, string.Empty);

    private static bool IsVowel(char c) => c is 'α' or 'ε' or 'η' or 'ι' or 'ο' or 'υ' or 'ω';

    [GeneratedRegex("(ουμαι|ωμαι|ομαι|αω|εω|οω|ω|μι|ος|ον|η|α)$")]
    private static partial Regex VerbEndingRegex();
}
