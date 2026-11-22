using System.Text.RegularExpressions;

namespace UnshackledWord.Domain.Models.BibleStructure.References;

/// <summary>
/// Finds references inside running text (the untagged lexicon notes). A candidate is a book code directly followed by a dot and a digit
/// (<c>1Ch.8.39</c>), which keeps false positives in English prose near zero; <see cref="ScriptureReferenceParser.ParseAt"/> decides
/// whether the candidate really is a reference and how much of the following text belongs to it.
/// </summary>
public static partial class ScriptureScanner
{
    /// <summary>Options of the lexicon scan: STEP/OSIS notation, English verse counting, no prefix matching.</summary>
    public static readonly ScriptureParseOptions OsisOptions = new(AliasProfile.Osis, AllowPrefix: false, WholeInput: false, Counting: VerseCounting.English);

    /// <summary>Options of the German scan (notes): German citation style, Hebrew (reader) counting, exact aliases only.</summary>
    public static readonly ScriptureParseOptions GermanTextOptions = new(AliasProfile.German, AllowPrefix: false, WholeInput: false, Counting: VerseCounting.Hebrew);

    [GeneratedRegex(@"(?<![\p{L}\d])[1-3]?\p{L}{2,6}\.(?=\d)")]
    private static partial Regex CandidateRegex();

    [GeneratedRegex(@"\d\s*[,:]\s*\d")]
    private static partial Regex ChapterVerseRegex();

    [GeneratedRegex(@"\G\.[ \t]*\p{L}")]
    private static partial Regex DotThenLetterRegex();

    [GeneratedRegex(@"^([1-3])\.?\s*(.+)$")]
    private static partial Regex NumberedAliasRegex();

    // German candidate: a book alias as written (case-sensitive), optional dot and space, then a digit.
    private static readonly Regex GermanCandidate = BuildGermanCandidate();

    private static Regex BuildGermanCandidate()
    {
        static string Escape(string text) => Regex.Escape(text).Replace("\\ ", @"\s?").Replace(" ", @"\s?");

        var patterns = BookAliasCatalog.GermanSurfaceAliases()
            .OrderByDescending(a => a.Length)
            .Select(alias =>
            {
                var numbered = NumberedAliasRegex().Match(alias);
                return numbered.Success
                    ? numbered.Groups[1].Value + @"\.?\s?" + Escape(numbered.Groups[2].Value)
                    : Escape(alias);
            });
        return new Regex(@"(?<![\p{L}\d])(?:" + string.Join('|', patterns) + @")\.?\s?(?=\d)", RegexOptions.CultureInvariant);
    }

    /// <summary>All references in <paramref name="text"/>, in order and without overlap. Positions are indexes into <paramref name="text"/>.</summary>
    public static IReadOnlyList<ScriptureReference> Scan(string? text, ScriptureParseOptions? options = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        options ??= OsisOptions;
        if (options.Profile == AliasProfile.German)
        {
            return ScanGerman(text, options);
        }

        var found = new List<ScriptureReference>();
        var position = 0;
        while (position < text.Length)
        {
            var candidate = CandidateRegex().Match(text, position);
            if (!candidate.Success)
            {
                break;
            }

            var reference = ScriptureReferenceParser.ParseAt(text, candidate.Index, options);
            if (reference is null || reference.Start != candidate.Index || reference.Length <= 0)
            {
                position = candidate.Index + candidate.Length;
                continue;
            }

            found.Add(reference);
            position = reference.Start + reference.Length;
        }

        return found;
    }

    private static List<ScriptureReference> ScanGerman(string text, ScriptureParseOptions options)
    {
        var found = new List<ScriptureReference>();
        var position = 0;
        while (position < text.Length)
        {
            var candidate = GermanCandidate.Match(text, position);
            if (!candidate.Success)
            {
                break;
            }

            var reference = ScriptureReferenceParser.ParseAt(text, candidate.Index, options);
            if (reference is null || reference.Start != candidate.Index || reference.Length <= 0 || reference.BookOnly || !IsProseReference(text, reference))
            {
                position = candidate.Index + candidate.Length;
                continue;
            }

            found.Add(reference);
            position = reference.Start + reference.Length;
        }

        return found;
    }

    // The false-positive guard of 04-references.md: common-word aliases need chapter,verse; "3. Tag" is an ordinal, not a chapter.
    private static bool IsProseReference(string text, ScriptureReference reference)
    {
        var bookText = text.Substring(reference.Start, reference.BookLength);
        var referenceText = text.Substring(reference.Start + reference.BookLength, reference.Length - reference.BookLength);
        var hasVerse = ChapterVerseRegex().IsMatch(referenceText);
        if (BookAliasCatalog.RequiresVerseInProse(bookText))
        {
            return hasVerse;
        }

        return hasVerse || !DotThenLetterRegex().IsMatch(text, reference.Start + reference.Length);
    }
}
