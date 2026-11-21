using System.Globalization;
using System.Text;

namespace UnshackledWord.Domain.Models.Vocab;

/// <summary>Text helpers of the vocab trainer shared by the lexicon generator, search and (later) typing.</summary>
public static class VocabText
{
    /// <summary>
    /// The search and typing key of a lemma: trimmed, decomposed (NFD), combining marks dropped (Greek accents and breathings, Hebrew
    /// niqqud and cantillation), final sigma folded to σ, lower-case. ἀρχή becomes αρχη, Ἰησοῦς becomes ιησουσ and
    /// בְּרֵאשִׁית becomes בראשית.
    /// </summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var decomposed = text.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(c == 'ς' ? 'σ' : char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }
}
