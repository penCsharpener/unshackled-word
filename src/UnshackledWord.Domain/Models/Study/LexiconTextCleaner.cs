using System.Net;
using System.Text.RegularExpressions;

namespace UnshackledWord.Domain.Models.Study;

/// <summary>Strips markup from STEP lexicon text and shortens it for display. Formatting only, the wording is untouched.</summary>
public static partial class LexiconTextCleaner
{
    [GeneratedRegex("<[^>]*>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    public static (string Text, bool Truncated) Clean(string? raw, int maxLength = 600)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return (string.Empty, false);
        }

        // Tags become spaces so "x</b><br/>y" does not glue words together; spaces before punctuation are repaired below.
        var text = TagRegex().Replace(raw, " ");
        text = WebUtility.HtmlDecode(text);
        text = WhitespaceRegex().Replace(text, " ").Trim();
        text = Regex.Replace(text, @" +([,.;:!?)])", "$1");

        if (text.Length <= maxLength)
        {
            return (text, false);
        }

        var cut = text.LastIndexOf(' ', maxLength - 1);
        var head = cut > 0 ? text[..cut] : text[..maxLength];
        return (head.TrimEnd(',', ';', ':', ' ') + "…", true);
    }
}
