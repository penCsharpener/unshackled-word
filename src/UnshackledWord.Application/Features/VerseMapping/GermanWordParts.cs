using System.Text;

namespace UnshackledWord.Application.Features.VerseMapping;

/// <summary>
/// Rules for compound parts of German words (<c>Gerstenernte</c> = <c>Gersten</c> + <c>ernte</c>, roadmap 0016.10), shared by the save
/// validator, the read path and the mapping seed import/export. The numbered migration <c>0027_CleanGermanWordParts.sql</c> applies
/// <see cref="IsRealPart"/> in SQL; keep the two in step.
/// </summary>
public static class GermanWordParts
{
    /// <summary>Length of the <c>GermanWordPart</c> column.</summary>
    public const int MaxPartLength = 30;

    /// <summary>
    /// Lower-cased letters and digits only: hyphens, punctuation and spaces are dropped, so <c>Gerstenernte.</c> equals <c>Gersten</c> +
    /// <c>ernte</c> and <c>Migdal-Heder</c> equals <c>Migdal</c> + <c>Heder</c>.
    /// </summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        foreach (var c in text.Where(char.IsLetterOrDigit))
        {
            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }

    /// <summary>
    /// A real compound part: contains a letter, is a case-insensitive substring of the word and is not the whole word (ignoring hyphens).
    /// <c>und</c> of <c>Gerstenernte</c>, <c>1</c>, <c>null</c>, <c>*</c>, <c>--</c> or the word itself are not.
    /// </summary>
    public static bool IsRealPart(string? word, string? part)
    {
        if (string.IsNullOrWhiteSpace(word) || string.IsNullOrWhiteSpace(part) || !part.Any(char.IsLetter))
        {
            return false;
        }

        return word.Contains(part, StringComparison.OrdinalIgnoreCase)
               && !string.Equals(word.Replace("-", string.Empty), part.Replace("-", string.Empty), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The part when it is a real part of the word, otherwise null.</summary>
    public static string? CleanPart(string? word, string? part)
    {
        return IsRealPart(word, part) ? part : null;
    }

    /// <summary>
    /// 1-based order of each part inside the word, derived from where the part sits in the word; equal part texts (one part linked to several
    /// original words) share the order. For parts stored without an order and for the seed, which does not carry it.
    /// </summary>
    public static int[] DeriveOrders(string word, IReadOnlyList<string> parts)
    {
        var keys = parts
            .Select(p => (Position: word.IndexOf(p, StringComparison.OrdinalIgnoreCase), Text: p.ToLowerInvariant()))
            .ToList();
        var ranked = keys.Distinct().OrderBy(k => k.Position).ThenBy(k => k.Text, StringComparer.Ordinal).ToList();

        return keys.Select(k => ranked.IndexOf(k) + 1).ToArray();
    }
}
