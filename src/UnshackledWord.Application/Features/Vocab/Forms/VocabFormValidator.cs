using UnshackledWord.Domain.Models.Vocab;

namespace UnshackledWord.Application.Features.Vocab.Forms;

/// <summary>
/// Checks one agent-written dictionary form (roadmap 0022.13) before it reaches the editor: a single line of at most
/// <see cref="MaxLength"/> characters whose first group (up to the first comma or opening bracket) is the lemma, compared after
/// <see cref="VocabText.Normalize"/> so accents, niqqud and final sigma do not matter. Examples: <c>μέσος, -η, -ον</c>, <c>λόγος, -ου, ὁ</c>.
/// </summary>
public static class VocabFormValidator
{
    public const int MaxLength = 120;

    /// <summary>The problem text for the model and the run log, or null when the form is acceptable.</summary>
    public static string? Check(string lemma, string? form)
    {
        if (string.IsNullOrWhiteSpace(form))
        {
            return "The form is empty.";
        }

        var text = form.Trim();

        if (text.Contains('\n') || text.Contains('\r'))
        {
            return "The form must be one line.";
        }

        if (text.Length > MaxLength)
        {
            return $"The form has {text.Length} characters; at most {MaxLength}.";
        }

        var end = text.IndexOfAny([',', '(']);
        var first = end < 0 ? text : text[..end];

        return VocabText.Normalize(first.Trim()) == VocabText.Normalize(lemma)
            ? null
            : $"The form must start with the lemma '{lemma}'.";
    }
}
