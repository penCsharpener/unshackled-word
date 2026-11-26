using System.Text;

namespace UnshackledWord.Application.Features.Lxx;

/// <summary>Why a Swete token is not a word of <c>LxxWords</c> (counted by <see cref="LxxWordFilter"/>).</summary>
public enum LxxDroppedToken
{
    /// <summary>A subverse marker (<c>[3]</c>, <c>⸂[1]</c>, <see cref="LxxVersification.IsMarkerToken"/>).</summary>
    Marker,

    /// <summary>Only editorial marks (<c>⸂⸆⸃</c>, <c>⸆</c>) or nothing at all.</summary>
    EditorialMark,

    /// <summary>A Hebrew letter name of Lamentations' acrostic (<c>Ἄλεφ.</c>, <c>Βήθ.</c> ...): Swete's section heading, no Greek word.</summary>
    AcrosticLetter,

    /// <summary>Punctuation printed as a token of its own (<c>·</c>, <c>,</c>): appended to the previous word, dropped when there is none.</summary>
    Punctuation,
}

/// <summary>
/// Turns the tokens of a Swete unit into the words of <c>LxxWords</c> (roadmap 0028.3):
/// <list type="bullet">
/// <item>marker tokens (<see cref="LxxVersification.IsMarkerToken"/>) are dropped;</item>
/// <item>the editorial marks <c>⸂ ⸆ ⸃</c> are removed from every token; a token left without letters or digits is dropped, except punctuation,
/// which is appended to the previous word;</item>
/// <item>in Lamentations the Hebrew letter names of the acrostic (<see cref="AcrosticLetters"/>, 88 tokens) are dropped.</item>
/// </list>
/// Swete has no other headings in the word file: the capitals at the start of a book (<c>ΕΝ ΑΡΧΗ</c>, <c>ΚΑΙ</c>, <c>ΛΟΓΟΣ</c>) are the first
/// words of the text printed in capitals, so they are kept as they are.
/// </summary>
public static class LxxWordFilter
{
    /// <summary>Swete's editorial marks: ⸂ … ⸃ brackets a variant, ⸆ an omission.</summary>
    public static readonly IReadOnlySet<char> EditorialMarks = new HashSet<char> { '⸂', '⸃', '⸆' };

    /// <summary>The letter names of the acrostic in Lam 1-4 as Swete spells them (NFC, with the trailing full stop).</summary>
    public static readonly IReadOnlySet<string> AcrosticLetters = new HashSet<string>(
        new[]
        {
            "Ἄλεφ.", "Βήθ.", "Βή.", "Γίμελ.", "Δάλεθ.", "Ἥ.", "Οὐαύ.", "Ζάιν.", "Ζάι.", "Ἥθ.", "Τήθ.", "Ἰώδ.", "Ἰώθ.", "Χάφ.", "Λάβδ.", "Λάμεδ.",
            "Μήμ.", "Νούν.", "Σάμχ.", "Σάμεχ.", "Ἄιν.", "Φή.", "Τιαδή.", "Κώφ.", "Ῥήχς.", "Χσέν.", "Θαύ.",
        }.Select(l => l.Normalize(NormalizationForm.FormC)),
        StringComparer.Ordinal);

    /// <summary>The TVTMS code of Lamentations, the only book with acrostic letter names in the word stream.</summary>
    public const string Lamentations = "Lam";

    /// <summary>The words of one unit in order; <paramref name="dropped"/> counts what was removed.</summary>
    public static List<string> Filter(IEnumerable<string> tokens, string rahlfsBook, IDictionary<LxxDroppedToken, int>? dropped = null)
    {
        var words = new List<string>();
        foreach (var token in tokens)
        {
            if (LxxVersification.IsMarkerToken(token))
            {
                Count(dropped, LxxDroppedToken.Marker);
                continue;
            }

            var text = StripMarks(token).Trim();
            if (rahlfsBook == Lamentations && AcrosticLetters.Contains(text.Normalize(NormalizationForm.FormC)))
            {
                Count(dropped, LxxDroppedToken.AcrosticLetter);
                continue;
            }

            if (!text.Any(char.IsLetterOrDigit))
            {
                if (text.Length > 0)
                {
                    Count(dropped, LxxDroppedToken.Punctuation);
                    if (words.Count > 0)
                    {
                        words[^1] += text;
                    }
                }
                else
                {
                    Count(dropped, LxxDroppedToken.EditorialMark);
                }

                continue;
            }

            words.Add(text);
        }

        return words;
    }

    private static string StripMarks(string token)
    {
        if (!token.Any(EditorialMarks.Contains))
        {
            return token;
        }

        var builder = new StringBuilder(token.Length);
        foreach (var c in token.Where(c => !EditorialMarks.Contains(c)))
        {
            builder.Append(c);
        }

        return builder.ToString();
    }

    private static void Count(IDictionary<LxxDroppedToken, int>? dropped, LxxDroppedToken kind)
    {
        if (dropped is not null)
        {
            dropped[kind] = dropped.TryGetValue(kind, out var n) ? n + 1 : 1;
        }
    }
}
