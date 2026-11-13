using UnshackledWord.Application.Features.Seed.Anchoring;

namespace UnshackledWord.Application.Tests.Features.Seed.Anchoring;

public class AnchorRunTests
{
    private static List<AnchorToken> Book1(int chapter, params string[] verses)
    {
        return AnchorTestData.Chapter1(verses)
            .Select(t => t with { Ref = chapter * 1000 + (t.Ref % 1000) })
            .ToList();
    }

    [Fact]
    public void Align_ShouldReportOnlyChaptersWithOrphans_WhenOnlyOneChapterLosesAWord()
    {
        var oldTokens = Book1(1001, "Im Anfang schuf Gott").Concat(Book1(1002, "Und die Erde")).ToList();
        var newTokens = Book1(1001, "Im Anfang schuf Gott").Concat(Book1(1002, "Und Erde")).ToList();

        var run = AnchorRun.Align(oldTokens, newTokens);

        run.Chapters.Select(c => c.Chapter).ShouldBe([1001, 1002]);
        run.Exact.ShouldBe(5);
        run.Shifted.ShouldBe(1);
        run.Orphaned.ShouldBe(1);
        run.ChaptersWithOrphans.Select(c => c.Chapter).ShouldBe([1002]);
        run.OrphanedTokens.ShouldBe([new AnchorToken(1002001, 2, "die")]);
    }

    [Fact]
    public void Align_ShouldOrphanTheWholeChapter_WhenItExistsOnTheOldSideOnly()
    {
        var run = AnchorRun.Align(Book1(1001, "Im Anfang").Concat(Book1(1002, "Und die Erde")).ToList(), Book1(1001, "Im Anfang"));

        run.Orphaned.ShouldBe(3);
        run.ChaptersWithOrphans.Single().Chapter.ShouldBe(1002);
    }

    [Fact]
    public void Align_ShouldCountInsertedWords_WhenAChapterExistsOnTheNewSideOnly()
    {
        var run = AnchorRun.Align(Book1(1001, "Im Anfang"), Book1(1001, "Im Anfang").Concat(Book1(1002, "Und die Erde")).ToList());

        run.InsertedNew.ShouldBe(3);
        run.Orphaned.ShouldBe(0);
        run.ChaptersWithOrphans.ShouldBeEmpty();
    }

    [Fact]
    public void Align_ShouldFlagTheChapter_WhenTheAlignmentGaveUp()
    {
        var run = AnchorRun.Align(Book1(1001, "a b c d e f g h"), Book1(1001, "p q r s t u v w"), maxEdits: 4);

        run.ChaptersWithOrphans.Single().Complete.ShouldBeFalse();
    }
}
