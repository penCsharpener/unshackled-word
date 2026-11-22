using System.Globalization;
using System.Text;

namespace UnshackledWord.Domain.Models.Vocab;

/// <summary>One piece of a segmented lemma form: a group of letters (with their combining marks) the learner types, or a fixed separator.</summary>
public sealed record VocabTextSegment(bool IsGroup, string Text)
{
    /// <summary>Number of base letters of a group after <see cref="VocabText.Normalize"/> (what the UI draws boxes for); 0 for a separator.</summary>
    public int Length => IsGroup ? VocabText.Normalize(Text).Length : 0;
}

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

    /// <summary>
    /// Splits a lemma form into letter groups (runs of Unicode letters with their combining marks) and fixed separators (everything else:
    /// comma, hyphen, space). μέσος, -η, -ον becomes [μέσος] ", -" [η] ", -" [ον].
    /// </summary>
    public static List<VocabTextSegment> Segment(string? text)
    {
        var result = new List<VocabTextSegment>();

        if (string.IsNullOrEmpty(text))
        {
            return result;
        }

        var current = new StringBuilder();
        var inGroup = false;

        void Flush()
        {
            if (current.Length > 0)
            {
                result.Add(new VocabTextSegment(inGroup, current.ToString()));
                current.Clear();
            }
        }

        foreach (var c in text.Normalize(NormalizationForm.FormC))
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            var isMark = category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;
            var belongsToGroup = char.IsLetter(c) || (isMark && inGroup);

            if (belongsToGroup != inGroup)
            {
                Flush();
                inGroup = belongsToGroup;
            }

            current.Append(c);
        }

        Flush();

        return result;
    }
}
