using System.Text.RegularExpressions;
using UnshackledWord.Domain.Models.Dbo.Step;

namespace UnshackledWord.Domain.Models.Study;

public sealed record StrongsParseResult(StrongsNumber? Value, string? Error);

/// <summary>A Strong's number in STEP notation, e.g. G0026 or H1254A. Aramaic uses H numbers, so only Hebrew or Greek is parsed.</summary>
public readonly partial record struct StrongsNumber(StrongsLanguage Language, int Number, string? Extra)
{
    [GeneratedRegex(@"^(?<lang>[GH])\s*(?<num>\d{1,5})(?<extra>[A-Za-z]{0,3})$", RegexOptions.IgnoreCase)]
    private static partial Regex StrongsRegex();

    /// <summary>
    /// Parses "G26", "g0026", "H1254A", "{H7225G}" and "H9003/{H7225G}" (the braced root part wins).
    /// Never throws.
    /// </summary>
    public static StrongsParseResult Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new(null, "Strong's number is empty. Example: 'G26' or 'H1254A'.");
        }

        var text = input.Trim();
        var open = text.IndexOf('{');
        var close = text.IndexOf('}');
        if (open >= 0 && close > open)
        {
            text = text[(open + 1)..close];
        }

        var match = StrongsRegex().Match(text.Trim());
        if (!match.Success)
        {
            return new(null, text.Length > 0 && char.IsDigit(text[0])
                ? "Prefix G (Greek) or H (Hebrew) is required."
                : $"Could not parse Strong's number '{input}'. Examples: 'G26', 'H1254A'.");
        }

        var language = char.ToUpperInvariant(match.Groups["lang"].Value[0]) == 'G' ? StrongsLanguage.Greek : StrongsLanguage.Hebrew;
        var extra = match.Groups["extra"].Value.ToUpperInvariant();
        return new(new StrongsNumber(language, int.Parse(match.Groups["num"].Value), extra.Length == 0 ? null : extra), null);
    }

    /// <summary>
    /// Normalizes user input to the form stored in the STEP tables ("g26" becomes "G0026", "H1254A_B" becomes "H1254A").
    /// An instance suffix after "_" is dropped, like the occurrence count of the word detail does. Never throws.
    /// </summary>
    public static bool TryNormalize(string? input, out string normalized, out bool hebrew)
    {
        normalized = string.Empty;
        hebrew = false;
        var text = input?.Trim();
        var underscore = text?.IndexOf('_') ?? -1;
        if (underscore >= 0)
        {
            text = text![..underscore];
        }

        var value = Parse(text).Value;
        if (value is null || value.Value.Number < 1)
        {
            return false;
        }

        normalized = value.Value.ToString();
        hebrew = value.Value.Language == StrongsLanguage.Hebrew;
        return true;
    }

    public override string ToString()
    {
        return $"{(Language == StrongsLanguage.Greek ? 'G' : 'H')}{Number:D4}{Extra}";
    }
}
