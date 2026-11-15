namespace UnshackledWord.Domain.Extensions;

/// <summary>
/// The one rule that produces <c>Elb1871Words."PlainWord"</c> from <c>WordInContext</c>. Used by the Elberfelder parser (seed) and by
/// the revision tokenizer, so both always agree. An empty result is possible (e.g. a token that is only punctuation).
/// </summary>
public static class Elb1871PlainWord
{
    private const string Punctuation = ",;:.!?\"'{}[]()’¶";

    public static string From(string wordInContext)
    {
        var result = wordInContext.Trim();

        foreach (var character in Punctuation)
        {
            result = result.Replace(character.ToString(), string.Empty);
        }

        return result.Trim('-');
    }
}
