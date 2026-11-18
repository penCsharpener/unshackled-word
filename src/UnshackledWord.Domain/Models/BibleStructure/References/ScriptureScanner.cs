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

    [GeneratedRegex(@"(?<![\p{L}\d])[1-3]?\p{L}{2,6}\.(?=\d)")]
    private static partial Regex CandidateRegex();

    /// <summary>All references in <paramref name="text"/>, in order and without overlap. Positions are indexes into <paramref name="text"/>.</summary>
    public static IReadOnlyList<ScriptureReference> Scan(string? text, ScriptureParseOptions? options = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        options ??= OsisOptions;
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
}
