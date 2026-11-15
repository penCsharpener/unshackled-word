using UnshackledWord.Application.Features.Revision;
using UnshackledWord.Application.Features.Seed.Anchoring;
using UnshackledWord.Application.Features.Seed.Revision;

namespace UnshackledWord.Application.Tests.Features.Seed.Revision;

public class RevisionMappingPlacerTests
{
    private static readonly List<RevisionToken> Words = RevisionTokenizer.Tokenize("Denn also hat Gott die Welt geliebt");

    private static SourceWordResolver Resolver(params (int Ref, int Pos, string Hash)[] installed) =>
        SourceWordResolver.Create(
            installed.Select(i => new SourceKey(i.Ref, i.Pos, i.Hash)),
            installed.Select(i => new AnchorToken(i.Ref, i.Pos, i.Hash)));

    private static RevisionMappingSeedLine Line(int pos, string form, int? srcPos = null, string hash = "", bool added = false, int? parentPos = null, string parentForm = "") => new()
    {
        Ref = 43003016, Level = 200, Pos = pos, Form = form, SrcRef = srcPos.HasValue ? 43003016 : null, SrcPos = srcPos, SrcHash = hash,
        Added = added, ParentPos = parentPos, ParentForm = parentForm, Origin = "manual"
    };

    [Fact]
    public void Place_ShouldImportEverything_WhenAllWordsAndSourcesAreFound()
    {
        var lines = new[] { Line(3, "hat", 3, "aaaa1111"), Line(4, "Gott", 4, "bbbb2222"), Line(1, "Denn", added: true, parentPos: 2, parentForm: "also") };

        var placement = RevisionMappingPlacer.Place(Words, lines, Resolver((43003016, 3, "aaaa1111"), (43003016, 4, "bbbb2222")));

        placement.Orphaned.ShouldBe(0);
        placement.Exact.ShouldBe(3);
        placement.Placed.Count().ShouldBe(3);
    }

    [Fact]
    public void Place_ShouldReportOrphans_WhenFormSourceOrParentDoNotMatch()
    {
        var lines = new[]
        {
            Line(4, "Gott", 4, "bbbb2222"),
            Line(5, "Götter", 5, "cccc3333"),                           // German form not in the text
            Line(6, "Welt", 6, "dddd4444"),                              // source word gone on this installation
            Line(1, "Denn", added: true, parentPos: 2, parentForm: "nie") // parent form not in the text
        };

        var placement = RevisionMappingPlacer.Place(Words, lines, Resolver((43003016, 4, "bbbb2222")));

        placement.Orphaned.ShouldBe(3);
        placement.Lines.Select(l => l.Reason).Where(r => r != string.Empty).ShouldBe(["German", "Source", "Parent"]);
    }

    [Fact]
    public void Place_ShouldFollowAShiftedGermanWordAndAShiftedSourceWord()
    {
        var resolver = SourceWordResolver.Create([new SourceKey(43003016, 9, "bbbb2222")], [new AnchorToken(43003016, 4, "bbbb2222")]);
        var placement = RevisionMappingPlacer.Place(Words, [Line(9, "Gott", 9, "bbbb2222")], resolver);

        var placed = placement.Placed.Single();
        placed.Pos.ShouldBe(4);
        placed.Source!.Value.Pos.ShouldBe(4);
        placed.Class.ShouldBe(AnchorClass.Shifted);
    }

    [Fact]
    public void Place_ShouldDropTheSecondLineOnTheSameWordPair_AndAnEdgeWithoutAnySource()
    {
        var lines = new[] { Line(4, "Gott", 4, "bbbb2222"), Line(4, "Gott", 4, "bbbb2222"), Line(5, "die") };

        var placement = RevisionMappingPlacer.Place(Words, lines, Resolver((43003016, 4, "bbbb2222")));

        placement.Lines.Select(l => l.Reason).ShouldBe(["", "Duplicate", "Source"]);
    }
}
