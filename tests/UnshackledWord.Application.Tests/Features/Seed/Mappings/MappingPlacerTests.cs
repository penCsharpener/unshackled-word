using UnshackledWord.Application.Features.Seed.Anchoring;
using UnshackledWord.Application.Features.Seed.Mappings;
using UnshackledWord.Application.Tests.Features.Seed.Anchoring;

namespace UnshackledWord.Application.Tests.Features.Seed.Mappings;

public class MappingPlacerTests
{
    private const int Verse = 1001001;

    private static MappingSeedLine Line(int pos, string form, int? srcPos = null, string? srcWord = null, bool added = false, int? parentPos = null, string? parentForm = null)
    {
        return new MappingSeedLine
        {
            Ref = Verse,
            Pos = pos,
            Form = form,
            SrcRef = srcPos.HasValue ? Verse : null,
            SrcPos = srcPos,
            SrcHash = srcWord is null ? string.Empty : SourceKey.Hash(srcWord),
            Added = added,
            ParentPos = parentPos,
            ParentForm = parentForm ?? string.Empty
        };
    }

    private static MappingPlacement Place(List<MappingSeedLine> lines, string oldGerman, string newGerman, string[] oldStep, string[] newStep)
    {
        var german = GermanWordResolver.Create(AnchorTestData.Chapter1(oldGerman), AnchorTestData.Chapter1(newGerman));
        var seedKeys = lines.Where(l => l.HasSource).Select(l => l.Source!.Value);
        var newStepTokens = newStep.Select((w, i) => SourceKey.From(Verse, i + 1, w).ToToken());
        var source = SourceWordResolver.Create(seedKeys, newStepTokens);

        return MappingPlacer.Place(lines, german, source);
    }

    [Fact]
    public void Place_ShouldLandOnTheRightIds_WhenAGermanAndAStepWordShifted()
    {
        List<MappingSeedLine> lines = [Line(2, "Anfang", 1, "alpha"), Line(3, "schuf", 2, "beta")];

        var placement = Place(lines, "Im Anfang schuf Gott", "Im allerersten Anfang schuf Gott", ["alpha", "beta"], ["neu", "alpha", "beta"]);

        var schuf = placement.Lines[1];
        schuf.Class.ShouldBe(AnchorClass.Shifted);
        schuf.German!.Value.Pos.ShouldBe(4);
        schuf.Source!.Value.Pos.ShouldBe(3);
        placement.Orphaned.ShouldBe(0);
    }

    [Fact]
    public void Place_ShouldReportAnExactMatchAsExact()
    {
        var placement = Place([Line(2, "Anfang", 1, "alpha")], "Im Anfang schuf Gott", "Im Anfang schuf Gott", ["alpha"], ["alpha"]);

        placement.Exact.ShouldBe(1);
        placement.Reportable.ShouldBeEmpty();
    }

    [Fact]
    public void Place_ShouldOrphanTheLine_WhenTheGermanWordIsGone()
    {
        var placement = Place([Line(4, "Gott", 3, "gamma")], "Im Anfang schuf Gott", "Im Anfang schuf", ["gamma"], ["gamma"]);

        placement.Lines[0].Class.ShouldBe(AnchorClass.Orphaned);
        placement.Lines[0].Reason.ShouldBe("German");
        placement.Placed.ShouldBeEmpty();
    }

    [Fact]
    public void Place_ShouldOrphanTheLine_WhenTheStepWordIsGone()
    {
        var placement = Place([Line(2, "Anfang", 1, "alpha")], "Im Anfang schuf Gott", "Im Anfang schuf Gott", ["alpha"], ["zzz"]);

        placement.Lines[0].Reason.ShouldBe("Source");
    }

    [Fact]
    public void Place_ShouldMoveTheParentWithItsWord_WhenAnAddedWordPointsAtIt()
    {
        List<MappingSeedLine> lines = [Line(1, "Im", added: true, parentPos: 2, parentForm: "Anfang"), Line(2, "Anfang", 1, "alpha")];

        var placement = Place(lines, "Im Anfang schuf", "Ganz Im Anfang schuf", ["alpha"], ["alpha"]);

        placement.Lines[0].German!.Value.Pos.ShouldBe(2);
        placement.Lines[0].Parent!.Value.Pos.ShouldBe(3);
        placement.Lines[0].Class.ShouldBe(AnchorClass.Shifted);
    }

    [Fact]
    public void Place_ShouldOrphanTheLine_WhenTheParentIsGone()
    {
        var placement = Place([Line(1, "Im", added: true, parentPos: 2, parentForm: "Anfang")], "Im Anfang schuf Gott", "Im schuf Gott", [], []);

        placement.Lines[0].Reason.ShouldBe("Parent");
    }

    [Fact]
    public void Place_ShouldKeepTheFirstLine_WhenTwoLinesLandOnTheSameWords()
    {
        List<MappingSeedLine> lines = [Line(2, "Anfang", 1, "alpha"), Line(2, "Anfang", 1, "alpha")];

        var placement = Place(lines, "Im Anfang schuf", "Im Anfang schuf", ["alpha"], ["alpha"]);

        placement.Lines.Select(l => l.Class).ShouldBe([AnchorClass.Exact, AnchorClass.Orphaned]);
        placement.Lines[1].Reason.ShouldBe("Duplicate");
    }

    [Fact]
    public void Place_ShouldKeepALineWithoutSourceOrParent()
    {
        var placement = Place([Line(3, "schuf")], "Im Anfang schuf", "Im Anfang schuf", [], []);

        placement.Exact.ShouldBe(1);
        placement.Lines[0].Source.ShouldBeNull();
    }
}
