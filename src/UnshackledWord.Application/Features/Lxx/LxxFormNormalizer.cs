using System.Globalization;
using System.Text;

namespace UnshackledWord.Application.Features.Lxx;

/// <summary>
/// Normal form for matching an LXX running-text word (Swete) against an NT running-text word (<c>StepGreekWords.Greek</c>), roadmap 0028.6.
/// Both sides use running text (grave accents on non-final oxytones, capitals at sentence starts, punctuation attached), so both go through the
/// same function and are compared like with like: NFD, keep only letters and combining marks, grave to acute, lower case, NFC. Dropped are
/// punctuation, <c>¶</c>, digits, Swete's editorial marks (<c>[ ]</c>, <c>⸂ ⸃ ⸆</c>) and the elision mark (<c>᾽ ’ '</c>; an elided form stays its own
/// form, <c>ἀπ</c> is not <c>ἀπό</c>). Breathings, iota subscript and diaeresis are kept. Pure, thread-safe.
/// </summary>
public static class LxxFormNormalizer
{
    private const char CombiningGrave = '̀';
    private const char CombiningAcute = '́';

    /// <summary>The normal form, or an empty string when the word has no letters (a mark or number on its own).</summary>
    public static string Normalize(string? word)
    {
        if (string.IsNullOrEmpty(word))
        {
            return string.Empty;
        }

        var decomposed = word.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var hasLetter = false;
        foreach (var c in decomposed)
        {
            var category = char.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                // A mark counts only on a letter; a stray one at the start is dropped.
                if (hasLetter)
                {
                    builder.Append(c == CombiningGrave ? CombiningAcute : c);
                }
            }
            else if (char.IsLetter(c))
            {
                hasLetter = true;
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return hasLetter ? builder.ToString().Normalize(NormalizationForm.FormC) : string.Empty;
    }

    /// <summary>
    /// True for Swete's book openings and headings: unaccented capitals (<c>ΕΝ ΑΡΧΗ</c>). The accented form cannot be known, so pass 1 skips them.
    /// A capital with breathing or accent (<c>Ἐν</c>) is ordinary running text and returns false.
    /// </summary>
    public static bool IsUnaccentedCapitals(string? word)
    {
        if (string.IsNullOrEmpty(word))
        {
            return false;
        }

        var letters = 0;
        foreach (var c in word.Normalize(NormalizationForm.FormD))
        {
            if (char.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                return false;
            }

            if (char.IsLetter(c))
            {
                if (!char.IsUpper(c))
                {
                    return false;
                }

                letters++;
            }
        }

        return letters > 0;
    }
}
