using System.Text.Json;
using UnshackledWord.Application.Features.Revision;

namespace UnshackledWord.Application.Tests.Features.Revision;

/// <summary>
/// Regression fixture exported read-only from the maintainer DB: <c>Elb1871Verses."VerseText"</c> with the <c>Elb1871Words</c> rows
/// (<c>WordInContext</c>, <c>PlainWord</c>) the parser produced. Tokenizing the text must reproduce those rows.
/// </summary>
public class RevisionTokenizerParserParityTests
{
    private sealed record FixtureVerse(int HebRefId, string Text, string[][] Words);

    private static List<FixtureVerse> Load() =>
        JsonSerializer.Deserialize<List<FixtureVerse>>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Features", "Revision", "Fixtures", "elb1871-verses.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    [Fact]
    public void Tokenize_ReproducesParserWords()
    {
        var verses = Load();
        verses.ShouldNotBeEmpty();

        var mismatches = new List<int>();
        foreach (var verse in verses)
        {
            var expected = verse.Words.Select((w, i) => new RevisionToken(i + 1, w[0], w[1])).ToList();
            if (!RevisionTokenizer.Tokenize(verse.Text).SequenceEqual(expected))
            {
                mismatches.Add(verse.HebRefId);
            }
        }

        mismatches.ShouldBeEmpty();
    }
}
