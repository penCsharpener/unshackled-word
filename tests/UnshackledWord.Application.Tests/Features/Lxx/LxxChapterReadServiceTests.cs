using UnshackledWord.Application.Features.Lxx;

namespace UnshackledWord.Application.Tests.Features.Lxx;

/// <summary>Roadmap 0028.4: grouping of LXX units under the Elb1871 verses of a chapter.</summary>
public class LxxChapterReadServiceTests
{
    private static LxxUnitRow Unit(int id, int sort, int book, int chapter, int verse, int? heb, int? after = null, string kind = "one", string form = "", int sub = 0) =>
        new() { Id = id, SortOrder = sort, BibleBookId = book, Chapter = chapter, Verse = verse, Subverse = sub, HebRefId = heb, AfterHebRefId = after, MapKind = kind, TextForm = form };

    private static LxxWordRow Word(int id, int unit, int pos, string greek) => new() { Id = id, LxxVerseId = unit, Position = pos, Greek = greek };

    [Fact]
    public void Build_Groups_Units_Per_Elb_Verse_And_Keeps_Empty_Verses()
    {
        var rows = new LxxChapterRows
        {
            ElbHebRefIds = [24025014, 24025015, 24025016],
            Units = [Unit(2, 20, 24, 32, 1, 24025015)],
            Words = [Word(11, 2, 2, "b"), Word(10, 2, 1, "a")],
        };

        var response = LxxChapterReadService.Build(24, 25, rows);

        response.Verses.Select(v => v.Verse).ShouldBe([14, 15, 16]);
        response.Verses[0].Units.ShouldBeEmpty();
        response.Verses[2].Units.ShouldBeEmpty();
        var unit = response.Verses[1].Units.ShouldHaveSingleItem();
        unit.Ref.ShouldBe("Jer.32:1");
        unit.DiffersFromHebrew.ShouldBeTrue();
        unit.Words.Select(w => w.Greek).ShouldBe(["a", "b"]);
    }

    [Fact]
    public void Build_Orders_Merged_And_Split_Units_By_SortOrder_And_Flags_Differences()
    {
        var rows = new LxxChapterRows
        {
            ElbHebRefIds = [11002035],
            Units =
            [
                Unit(3, 30, 11, 2, 35, 11002035, kind: "split", sub: 1),
                Unit(1, 10, 11, 2, 35, 11002035, kind: "split"),
                Unit(2, 20, 11, 2, 35, 11002035, kind: "split", sub: 0),
            ],
        };

        var units = LxxChapterReadService.Build(11, 2, rows).Verses.Single().Units;

        units.Select(u => u.LxxVerseId).ShouldBe([1, 2, 3]);
        units[0].DiffersFromHebrew.ShouldBeFalse();
        units[2].Ref.ShouldBe("1Ki.2:35.1");
        units[2].DiffersFromHebrew.ShouldBeTrue();
    }

    [Fact]
    public void Build_Places_Additions_By_SortOrder_Around_The_Main_Unit()
    {
        // Esther: addition A (sort 5) precedes 1:1 (sort 10); an addition after the verse (sort 15) follows it.
        var rows = new LxxChapterRows
        {
            ElbHebRefIds = [17001001],
            Units =
            [
                Unit(3, 15, 17, 1, 1, null, after: 17001001, kind: "addition", sub: 1),
                Unit(2, 10, 17, 1, 1, 17001001),
                Unit(1, 5, 17, 1, 1, null, after: 17001001, kind: "addition", sub: 2),
            ],
        };

        var units = LxxChapterReadService.Build(17, 1, rows).Verses.Single().Units;

        units.Select(u => u.LxxVerseId).ShouldBe([1, 2, 3]);
        units.Select(u => u.IsAddition).ShouldBe([true, false, true]);
        units[0].DiffersFromHebrew.ShouldBeTrue();
        units[1].DiffersFromHebrew.ShouldBeFalse();
    }

    [Fact]
    public void Build_Returns_Both_Text_Forms_For_Daniel()
    {
        var rows = new LxxChapterRows
        {
            ElbHebRefIds = [27003001],
            Units = [Unit(1, 1, 27, 3, 1, 27003001, form: "og"), Unit(2, 2, 27, 3, 1, 27003001, form: "th")],
        };

        LxxChapterReadService.Build(27, 3, rows).Verses.Single().Units.Select(u => u.TextForm).ShouldBe(["og", "th"]);
    }
}
