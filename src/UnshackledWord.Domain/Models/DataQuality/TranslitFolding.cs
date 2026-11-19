using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace UnshackledWord.Domain.Models.DataQuality;

/// <summary>
/// Search folding of the original-word filter: lower case, no diacritics, no separators, plus Latin equivalences
/// (ph=f, kh=ch=x, ts=tz=ṣ, sh=š, w=v) and a Hebrew consonant skeleton (vowels removed).
/// </summary>
public static partial class TranslitFolding
{
    private static readonly char[] Dropped = ['.', '-', 'ʼ', 'ʽ', '\'', '’', '‘', '`'];

    /// <summary>Lower case → Normalize(FormD) → drop non-spacing marks → drop separators → ς→σ and the Hebrew final forms to their regular letters.</summary>
    public static string Fold(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var lowered = input.Trim().ToLowerInvariant()
            .Replace("ṣ", "ts", StringComparison.Ordinal)
            .Replace("š", "sh", StringComparison.Ordinal)
            .Replace("ś", "s", StringComparison.Ordinal);

        var sb = new StringBuilder(lowered.Length);
        foreach (var c in lowered.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (Array.IndexOf(Dropped, c) >= 0) continue;
            sb.Append(c switch
            {
                'ς' => 'σ',
                'ך' => 'כ',
                'ם' => 'מ',
                'ן' => 'נ',
                'ף' => 'פ',
                'ץ' => 'צ',
                _ => c,
            });
        }

        return sb.ToString();
    }

    /// <summary>
    /// <see cref="Fold"/> for Greek / Hebrew words as typed or printed: also drops everything that is no letter or digit
    /// (maqaf, sof pasuq, paseq, Greek ano teleia and question mark, punctuation) except spaces. Hebrew final forms and ς are normalised by <see cref="Fold"/>.
    /// </summary>
    public static string FoldScript(string? input)
    {
        var folded = Fold(input);
        return folded.Length == 0 ? folded : string.Concat(folded.Where(c => char.IsLetterOrDigit(c) || c == ' '));
    }

    /// <summary>Folded text with the Latin equivalences applied, so that "Phaneros" and "faneros", "wayyiqra" and "vayyiqra" meet.</summary>
    public static string Latin(string? input)
    {
        return Fold(input)
            .Replace("tz", "ts", StringComparison.Ordinal)
            .Replace("kh", "ch", StringComparison.Ordinal)
            .Replace("x", "ch", StringComparison.Ordinal)
            .Replace("ph", "f", StringComparison.Ordinal)
            .Replace("w", "v", StringComparison.Ordinal);
    }

    /// <summary>
    /// Consonant skeleton of a transliteration: <see cref="Latin"/> without vowels, with the Hebrew spirantisation pairs
    /// merged (b=v, p=f, k=ch=q, th=t) and doubled consonants collapsed. "dabar", "da.var" and "dbr" all give "dvr".
    /// </summary>
    public static string Skeleton(string? input)
    {
        var consonants = VowelRegex().Replace(Latin(input), string.Empty)
            .Replace("ch", "k", StringComparison.Ordinal)
            .Replace("q", "k", StringComparison.Ordinal)
            .Replace("th", "t", StringComparison.Ordinal)
            .Replace("b", "v", StringComparison.Ordinal)
            .Replace("p", "f", StringComparison.Ordinal);
        return RepeatRegex().Replace(consonants, "$1");
    }

    public static bool IsLatinInput(string folded) => folded.Length > 0 && folded.All(c => c is >= 'a' and <= 'z');

    [GeneratedRegex("(.)\\1+")]
    private static partial Regex RepeatRegex();

    [GeneratedRegex("[aeiou]")]
    private static partial Regex VowelRegex();

    /// <summary>
    /// Parses a Strong's query: G1577, g1577, H430, h0430, 2424 (language open). Returns the zero-padded base numbers
    /// (G1577, H0430); a bare number yields both languages. An Extra letter (G2424a) is ignored.
    /// </summary>
    public static IReadOnlyList<string> ParseStrongs(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return [];
        var m = StrongsRegex().Match(input.Trim());
        if (!m.Success) return [];

        var number = int.Parse(m.Groups["n"].Value, CultureInfo.InvariantCulture);
        if (number is 0 or > 9999) return [];
        var digits = number.ToString("D4", CultureInfo.InvariantCulture);
        var lang = m.Groups["l"].Value.ToUpperInvariant();
        return lang.Length == 0 ? ["G" + digits, "H" + digits] : [lang + digits];
    }

    /// <summary>Normalises a base Strong's key from a route value: G2424, g2424, H430 → G2424 / H0430; null when invalid.</summary>
    public static string? NormalizeStrongsKey(string? input)
    {
        var parsed = ParseStrongs(input);
        return parsed.Count == 1 ? parsed[0] : null;
    }

    [GeneratedRegex(@"^(?<l>[GgHh])?0*(?<n>\d{1,4})[A-Za-z]?$")]
    private static partial Regex StrongsRegex();
}
