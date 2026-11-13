using System.Text;
using UnshackledWord.Domain.Extensions;

namespace UnshackledWord.Application.Features.Seed.Anchoring;

/// <summary>Normalisation of a German word to the text the anchoring compares.</summary>
public static class TokenKey
{
    /// <summary>
    /// Unicode NFC, case kept, punctuation stripped exactly like <c>Elb1871Words."PlainWord"</c> is (<c>RemovePunctuation</c>), so the
    /// seed's <c>Form</c> column, the database column and a freshly parsed word all give the same key.
    /// </summary>
    public static string Normalize(string? word)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            return string.Empty;
        }

        return word.RemovePunctuation().Normalize(NormalizationForm.FormC);
    }
}
