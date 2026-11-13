using UnshackledWord.Application.Features.Seed.Anchoring;

namespace UnshackledWord.Application.Tests.Features.Seed.Anchoring;

public static class AnchorTestData
{
    public const int Chapter = 1001;

    /// <summary>Builds the tokens of one chapter from verse texts: <c>("Im Anfang", "Und die Erde")</c> are verses 1 and 2 (positions restart per verse).</summary>
    public static List<AnchorToken> Chapter1(params string[] verses)
    {
        var tokens = new List<AnchorToken>();

        for (var verse = 0; verse < verses.Length; verse++)
        {
            var words = verses[verse].Split(' ', StringSplitOptions.RemoveEmptyEntries);

            for (var position = 0; position < words.Length; position++)
            {
                tokens.Add(AnchorToken.ForWord(Chapter * 1000 + verse + 1, position + 1, words[position]));
            }
        }

        return tokens;
    }
}
