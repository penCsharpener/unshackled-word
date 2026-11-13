using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace UnshackledWord.Application.Features.Seed.Anchoring;

/// <summary>
/// Key of a STEP (Greek/Hebrew) word that survives a re-import: the STEP verse (<c>LxxRefId</c>), the position in it and a short hash
/// of the word. STEP text is never redistributed, so the seed carries only the hash, which verifies and aligns but cannot be turned
/// back into the word.
/// </summary>
public readonly record struct SourceKey(int LxxRefId, int Position, string SrcHash)
{
    public const int HashHexLength = 8;

    public static SourceKey From(int lxxRefId, int position, string? stepWord)
    {
        return new SourceKey(lxxRefId, position, Hash(stepWord));
    }

    /// <summary>The key as a token, so the same <see cref="ChapterAligner"/> can align STEP words by their hashes.</summary>
    public AnchorToken ToToken() => new(LxxRefId, Position, SrcHash);

    /// <summary>
    /// 8 hex digits (lower case) of the SHA-256 over the word after NFC normalisation, removal of everything that is not a letter or
    /// digit (vowel points, accents, cantillation, maqaf, sof pasuq, punctuation) and lower-casing, so a changed accent in a STEP
    /// release does not change the hash but a changed letter does.
    /// </summary>
    public static string Hash(string? stepWord)
    {
        var bytes = Encoding.UTF8.GetBytes(Normalize(stepWord));

        return Convert.ToHexStringLower(SHA256.HashData(bytes)).Substring(0, HashHexLength);
    }

    private static string Normalize(string? stepWord)
    {
        if (string.IsNullOrEmpty(stepWord))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(stepWord.Length);

        foreach (var c in stepWord.Normalize(NormalizationForm.FormD))
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);

            if (category is UnicodeCategory.UppercaseLetter
                or UnicodeCategory.LowercaseLetter
                or UnicodeCategory.TitlecaseLetter
                or UnicodeCategory.ModifierLetter
                or UnicodeCategory.OtherLetter
                or UnicodeCategory.DecimalDigitNumber)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }
}
