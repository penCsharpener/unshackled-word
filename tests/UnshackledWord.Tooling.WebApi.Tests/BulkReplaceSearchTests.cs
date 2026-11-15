using UnshackledWord.Domain.WebApi.Revision;
using UnshackledWord.Tooling.WebApi.Endpoints.Revision.BulkReplaceSearch;

namespace UnshackledWord.Tooling.WebApi.Tests;

public class BulkReplaceSearchTests
{
    private static BulkTokenRow Token(int pos, string form, string? lemma = null, string? stts = null, int verse = 1002022) =>
        new() { HebRefId = verse, PositionInVerse = pos, WordId = verse * 100 + pos, PlainWord = form.Trim(',', '.').ToLowerInvariant(), WordInContext = form, Lemma = lemma, Stts = stts };

    [Fact]
    public void BuildRows_MatchesWholeWordsCaseInsensitively_AndNumbersOccurrencesPerVerse()
    {
        var tokens = new[] { Token(1, "Weib", "Weib"), Token(2, "und", "und"), Token(3, "weib,", "Weib"), Token(4, "Weiber", "Weib") };

        var rows = BulkReplaceSearchService.BuildRows(tokens, "Weib", []);

        rows.Select(r => r.PositionInVerse).ShouldBe([1, 3]);
        rows.Select(r => r.Occurrence).ShouldBe([1, 2]);
        rows[1].Found.ShouldBe("weib,");
    }

    [Fact]
    public void BuildRows_WithLemmas_AlsoMatchesOtherForms()
    {
        var tokens = new[] { Token(1, "des", "der", "ART"), Token(2, "Weibes", "Weib"), Token(3, "Weiber", "Weib") };

        var rows = BulkReplaceSearchService.BuildRows(tokens, "Weib", ["Weib"]);

        rows.Select(r => r.Found).ShouldBe(["Weibes", "Weiber"]);
    }

    [Fact]
    public void BuildRows_FlagsAnArticleOrDeterminerBeforeTheHit_AndKeepsSixWordsOfContext()
    {
        var tokens = Enumerable.Range(1, 8).Select(i => Token(i, $"w{i}", "x")).ToList();
        tokens.Add(Token(9, "ein", "ein", "ART"));
        tokens.Add(Token(10, "Weib", "Weib"));
        tokens.Add(Token(11, "ging", "gehen"));

        var row = BulkReplaceSearchService.BuildRows(tokens, "Weib", []).Single();

        row.ArticleBefore.ShouldBe("ein");
        row.ContextBefore.ShouldBe("w4 w5 w6 w7 w8 ein");
        row.ContextAfter.ShouldBe("ging");
    }

    [Fact]
    public void BuildRows_NoArticleFlag_WhenThePreviousWordIsNotADeterminer()
    {
        var tokens = new[] { Token(1, "schönes", "schön", "ADJA"), Token(2, "Weib", "Weib") };

        BulkReplaceSearchService.BuildRows(tokens, "Weib", []).Single().ArticleBefore.ShouldBeNull();
    }

    [Fact]
    public void BuildFacets_CountsPerStrongsMostFrequentFirst_AndAppendsTheUnlinkedHits()
    {
        var rows = new List<BulkReplaceRow>
        {
            new() { Strongs = ["H802"] },
            new() { Strongs = ["H802", "H2181"] },
            new() { Strongs = [] },
            new() { Strongs = ["G1135"] },
        };

        var facets = BulkReplaceSearchService.BuildFacets(rows);

        facets.Select(f => (f.Strongs, f.Count)).ShouldBe([("H802", 2), ("G1135", 1), ("H2181", 1), (null, 1)]);
    }

    [Theory]
    [InlineData("H0802", "H802")]
    [InlineData("H0802a", "H802")]
    [InlineData("G1135", "G1135")]
    [InlineData("xyz", "xyz")]
    public void NormalizeStrongs_StripsLeadingZerosAndSuffixes(string raw, string expected)
    {
        BulkReplaceSearchService.NormalizeStrongs(raw).ShouldBe(expected);
    }

    [Theory]
    [InlineData("Weib", true)]
    [InlineData("Weib-es", true)]
    [InlineData("W", false)]
    [InlineData("zwei Worte", false)]
    [InlineData("Weib1", false)]
    public void Validator_AcceptsOneTokenOfLetters(string word, bool valid)
    {
        var request = new BulkReplaceSearchRequest { Word = word, SourceRevision = "elb2027", TargetRevision = "elb2027" };

        new Validator().Validate(request).IsValid.ShouldBe(valid);
    }
}
