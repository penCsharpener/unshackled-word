using UnshackledWord.Application.Features.Seed.Anchoring;
using static UnshackledWord.Application.Tests.Features.Seed.Anchoring.AnchorTestData;

namespace UnshackledWord.Application.Tests.Features.Seed.Anchoring;

public class ChapterAlignerTests
{
    private static ChapterAlignment Align(List<AnchorToken> oldTokens, List<AnchorToken> newTokens)
    {
        return ChapterAligner.Align(AnchorTestData.Chapter, oldTokens, newTokens);
    }

    private static AnchorClass[] Classes(ChapterAlignment alignment) => alignment.Matches.Select(m => m.Class).ToArray();

    [Fact]
    public void Align_ShouldMarkEverythingExact_WhenChapterIsIdentical()
    {
        var tokens = Chapter1("Im Anfang schuf Gott", "Und die Erde war wüst");

        var alignment = Align(tokens, [.. tokens]);

        alignment.Exact.ShouldBe(9);
        alignment.Shifted.ShouldBe(0);
        alignment.Modified.ShouldBe(0);
        alignment.Orphaned.ShouldBe(0);
        alignment.UnmatchedNew.ShouldBeEmpty();
        alignment.Matches.All(m => m.New == m.Old).ShouldBeTrue();
    }

    [Fact]
    public void Align_ShouldShiftFollowingWords_WhenAWordIsInsertedInTheVerse()
    {
        var oldTokens = Chapter1("Im Anfang schuf Gott", "Und die Erde");
        var newTokens = Chapter1("Im Anfang schuf dann Gott", "Und die Erde");

        var alignment = Align(oldTokens, newTokens);

        // Im Anfang schuf unchanged; Gott moved from position 4 to 5; verse 2 unchanged
        Classes(alignment).ShouldBe([
            AnchorClass.Exact, AnchorClass.Exact, AnchorClass.Exact, AnchorClass.Shifted,
            AnchorClass.Exact, AnchorClass.Exact, AnchorClass.Exact]);
        alignment.Matches[3].New.ShouldBe(new AnchorToken(1001001, 5, "Gott"));
        alignment.UnmatchedNew.ShouldBe([new AnchorToken(1001001, 4, "dann")]);
        alignment.Orphaned.ShouldBe(0);
    }

    [Fact]
    public void Align_ShouldOrphanTheDeletedWord_WhenAWordIsRemoved()
    {
        var oldTokens = Chapter1("Im Anfang schuf dann Gott");
        var newTokens = Chapter1("Im Anfang schuf Gott");

        var alignment = Align(oldTokens, newTokens);

        Classes(alignment).ShouldBe([AnchorClass.Exact, AnchorClass.Exact, AnchorClass.Exact, AnchorClass.Orphaned, AnchorClass.Shifted]);
        alignment.OrphanedTokens.ShouldBe([new AnchorToken(1001001, 4, "dann")]);
        alignment.Matches[3].New.ShouldBeNull();
        alignment.Matches[4].New.ShouldBe(new AnchorToken(1001001, 4, "Gott"));
    }

    [Fact]
    public void Align_ShouldShiftIntoTheNextVerse_WhenAWordMovesAcrossTheVerseBoundary()
    {
        var oldTokens = Chapter1("Im Anfang schuf Gott", "die Himmel und die Erde");
        var newTokens = Chapter1("Im Anfang schuf", "Gott die Himmel und die Erde");

        var alignment = Align(oldTokens, newTokens);

        alignment.Orphaned.ShouldBe(0);
        alignment.Matches[3].Class.ShouldBe(AnchorClass.Shifted);
        alignment.Matches[3].New.ShouldBe(new AnchorToken(1001002, 1, "Gott"));
        alignment.Matches[4].New.ShouldBe(new AnchorToken(1001002, 2, "die"));
        alignment.Exact.ShouldBe(3);
        alignment.Shifted.ShouldBe(6);
    }

    [Fact]
    public void Align_ShouldMarkModified_WhenOneWordIsReplacedBetweenEqualNeighbours()
    {
        var oldTokens = Chapter1("Im Anfang schuf Gott die Himmel");
        var newTokens = Chapter1("Im Anfang schuff Gott die Himmel");

        var alignment = Align(oldTokens, newTokens);

        Classes(alignment).ShouldBe([
            AnchorClass.Exact, AnchorClass.Exact, AnchorClass.Modified, AnchorClass.Exact, AnchorClass.Exact, AnchorClass.Exact]);
        alignment.Matches[2].New.ShouldBe(new AnchorToken(1001001, 3, "schuff"));
        alignment.Orphaned.ShouldBe(0);
        alignment.UnmatchedNew.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Gott schuf die Himmel", "Ein schuf die Himmel")]
    [InlineData("Im Anfang schuf Gott", "Im Anfang schuf Er")]
    public void Align_ShouldMarkModified_WhenTheFirstOrLastWordOfTheChapterIsReplaced(string oldText, string newText)
    {
        var alignment = Align(Chapter1(oldText), Chapter1(newText));

        alignment.Modified.ShouldBe(1);
        alignment.Orphaned.ShouldBe(0);
    }

    [Theory]
    [InlineData("Im Anfang schuf Gott", "Im Anfang schuf Gott Er")]
    [InlineData("Im Anfang schuf Gott", "Im Anfang Er sie Gott")]
    [InlineData("Im Anfang schuf Gott", "Im Er sie Gott")]
    public void Align_ShouldNotGuess_WhenReplacementIsNotOneToOne(string oldText, string newText)
    {
        var alignment = Align(Chapter1(oldText), Chapter1(newText));

        alignment.Modified.ShouldBe(0);
    }

    [Fact]
    public void Align_ShouldOrphanTheWord_WhenAOneWordChapterIsReplaced()
    {
        var alignment = Align(Chapter1("Amen"), Chapter1("Selah"));

        alignment.Classes().ShouldBe([AnchorClass.Orphaned]);
        alignment.UnmatchedNew.Count.ShouldBe(1);
    }

    [Fact]
    public void Align_ShouldSplitAVerse_WhenTheTailMovesToANewVerse()
    {
        var oldTokens = Chapter1("Im Anfang schuf Gott die Himmel und die Erde");
        var newTokens = Chapter1("Im Anfang schuf Gott", "die Himmel und die Erde");

        var alignment = Align(oldTokens, newTokens);

        alignment.Orphaned.ShouldBe(0);
        alignment.Exact.ShouldBe(4);
        alignment.Shifted.ShouldBe(5);
        alignment.Matches[4].New.ShouldBe(new AnchorToken(1001002, 1, "die"));
        alignment.Matches[8].New.ShouldBe(new AnchorToken(1001002, 5, "Erde"));
    }

    [Fact]
    public void Align_ShouldKeepRepeatedWordsInOrder_WhenAWordIsInsertedBeforeThem()
    {
        var oldTokens = Chapter1("und Gott und die Erde und Licht");
        var newTokens = Chapter1("Dann und Gott und die Erde und Licht");

        var alignment = Align(oldTokens, newTokens);

        alignment.Orphaned.ShouldBe(0);
        alignment.Matches.Select(m => m.New!.Value.Pos).ShouldBe([2, 3, 4, 5, 6, 7, 8]);
    }

    [Fact]
    public void Align_ShouldNotCrossMatches_WhenTwoDifferentWordsAreSwapped()
    {
        var oldTokens = Chapter1("Im Himmel Erde und Licht");
        var newTokens = Chapter1("Im Erde Himmel und Licht");

        var alignment = Align(oldTokens, newTokens);

        // Only one of the two swapped words can keep its identity; the other one is not guessed onto the wrong place.
        alignment.Orphaned.ShouldBe(1);
        alignment.Matches.Where(m => m.Class != AnchorClass.Orphaned).Select(m => m.New!.Value.Pos)
            .ShouldBe(alignment.Matches.Where(m => m.Class != AnchorClass.Orphaned).Select(m => m.New!.Value.Pos).Order());
        alignment.Matches.Where(m => m.New is not null).All(m => m.New!.Value.Text == m.Old.Text).ShouldBeTrue();
    }

    [Fact]
    public void Align_ShouldStayExact_WhenTwoEqualWordsSwapPlaces()
    {
        // Swapping two identical words does not change the token list at all.
        var oldTokens = Chapter1("Gott sah Gott");
        var newTokens = Chapter1("Gott sah Gott");

        Align(oldTokens, newTokens).Exact.ShouldBe(3);
    }

    [Fact]
    public void Align_ShouldTreatAnEmptyVerseAsNothing_WhenAVerseHasNoWords()
    {
        // verse 2 has no words on either side: only verses 1 and 3 hold tokens
        var oldTokens = Chapter1("Im Anfang", "", "Und die Erde");
        var newTokens = Chapter1("Im Anfang", "", "Und die Erde");

        var alignment = Align(oldTokens, newTokens);

        alignment.Exact.ShouldBe(5);
        alignment.Matches.Select(m => m.Old.Ref).Distinct().ShouldBe([1001001, 1001003]);
    }

    [Fact]
    public void Align_ShouldFillAnEmptyVerse_WhenTheNewSideHasWordsThereOldHadNone()
    {
        var alignment = Align(Chapter1("Im Anfang", "", "Und die Erde"), Chapter1("Im Anfang", "Gott sprach", "Und die Erde"));

        alignment.Orphaned.ShouldBe(0);
        alignment.Exact.ShouldBe(5);
        alignment.UnmatchedNew.Count.ShouldBe(2);
    }

    [Fact]
    public void Align_ShouldOrphanEverything_WhenTheNewSideChapterIsEmpty()
    {
        var alignment = Align(Chapter1("Im Anfang"), []);

        alignment.Orphaned.ShouldBe(2);
        alignment.Matches.All(m => m.New is null).ShouldBeTrue();
    }

    [Fact]
    public void Align_ShouldReturnNoMatches_WhenTheOldSideChapterIsEmpty()
    {
        var alignment = Align([], Chapter1("Im Anfang"));

        alignment.Matches.ShouldBeEmpty();
        alignment.UnmatchedNew.Count.ShouldBe(2);
    }

    [Fact]
    public void Align_ShouldReportIncomplete_WhenChapterDiffersBeyondTheEditBudget()
    {
        var alignment = ChapterAligner.Align(Chapter, Chapter1("a b c d e f g h"), Chapter1("p q r s t u v w"), maxEdits: 4);

        alignment.Complete.ShouldBeFalse();
        alignment.Orphaned.ShouldBe(8);
    }

    [Fact]
    public void Align_ShouldAlignSourceWordsByHash_WhenGivenSourceKeyTokens()
    {
        var oldWords = new[] { "בְּרֵאשִׁ֖ית", "בָּרָ֣א", "אֱלֹהִ֑ים" };
        var newWords = new[] { "בראשית", "בָּרָ֣א", "הוא", "אֱלֹהִ֑ים" };
        var oldTokens = oldWords.Select((w, i) => SourceKey.From(1001001, i + 1, w).ToToken()).ToList();
        var newTokens = newWords.Select((w, i) => SourceKey.From(1001001, i + 1, w).ToToken()).ToList();

        var alignment = Align(oldTokens, newTokens);

        Classes(alignment).ShouldBe([AnchorClass.Exact, AnchorClass.Exact, AnchorClass.Shifted]);
        alignment.Matches[2].New!.Value.Pos.ShouldBe(4);
    }
}

internal static class ChapterAlignmentExtensions
{
    public static AnchorClass[] Classes(this ChapterAlignment alignment) => alignment.Matches.Select(m => m.Class).ToArray();
}
