using System.Diagnostics;
using System.Text.RegularExpressions;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.Dbo.Step;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public static partial class StrongsRegexParser
{
    /// <summary>
    /// Parses the strongs numbers from DisambiguatedStrongs or UnifiedStrongs columns.
    /// </summary>
    /// <param name="strongsText"></param>
    /// <param name="langOverride">Since there is no Strongs number with a leading 'A', Aramaic must be added via external parsing and override.</param>
    /// <returns></returns>
    /// <exception cref="UnreachableException"></exception>
    public static IEnumerable<StrongsNumberInternal> Parse(string strongsText, StrongsLanguage? langOverride = null)
    {
        if (strongsText.IsNullOrWhiteSpace())
        {
            yield break;
        }

        var matches = ExtractStrongs().Matches(strongsText);
        if (matches.Count == 0)
        {
            yield break;
        }

        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var strongsNumber = new StrongsNumberInternal();
            strongsNumber.Number = int.Parse(match.Groups["number"].Value);
            var lang = match.Groups["lang"].Value switch
            {
                "G" => StrongsLanguage.Greek,
                "H" => StrongsLanguage.Hebrew,
                "A" => StrongsLanguage.Aramaic,
                _ => throw new UnreachableException("this strongs number should be impossible")
            };
            strongsNumber.LanguageId = langOverride ?? lang;
            strongsNumber.Extra = match.Groups["extra"].Value.SetNullOrValue();
            strongsNumber.IsRoot = match.Groups["isRoot1"].Value.IsNotNullOrEmpty();
            strongsNumber.CoversNextWord = match.Groups["nextWord"].Value.IsNotNullOrEmpty();
            strongsNumber.DisambiguatedExtra = match.Groups["disamExtra"].Value?.Replace(" = ", "").SetNullOrValue();
            strongsNumber.UnderscoredExtra = match.Groups["underscoredExtra"].Value?.Replace("_", "").SetNullOrValue();
            strongsNumber.Order = i + 1;

            yield return strongsNumber;
        }
    }

    [GeneratedRegex(@"(?<isRoot1>{)?(?<lang>[HG])(?<number>\d\d\d\d\d?)(?<extra>\w)?(?<underscoredExtra>_\w)?(?<isRoot2>})?(?<nextWord>\+)?(?<disamExtra>\s=\s.*)?")]
    private static partial Regex ExtractStrongs();
}

public class StrongsNumberInternal : IEquatable<StrongsNumberInternal>
{
    public int Id { get; set; }
    public StrongsLanguage LanguageId { get; set; }
    public int Number { get; set; }
    /// <summary>
    /// when the strongs number is followed by a letter or underscore letter
    /// </summary>
    public string? Extra { get; set; }
    public string? DisambiguatedExtra { get; set; }
    public string? UnderscoredExtra { get; set; }
    /// <summary>
    /// when the strongs number is surrounded by braces
    /// </summary>
    public bool IsRoot { get; set; }
    /// <summary>
    /// when the strongs number is followed by a +
    /// </summary>
    public bool CoversNextWord { get; set; }
    public int Order { get; set; }

    public override bool Equals(object? obj)
    {
        return Equals(obj as StrongsNumberInternal);
    }

    public bool Equals(StrongsNumberInternal? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return LanguageId == other.LanguageId &&
               Number == other.Number &&
               string.Equals(Extra, other.Extra, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(LanguageId, Number, Extra?.ToLowerInvariant());
    }

    public static bool operator ==(StrongsNumberInternal? left, StrongsNumberInternal? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(StrongsNumberInternal? left, StrongsNumberInternal? right)
    {
        return !Equals(left, right);
    }
}
