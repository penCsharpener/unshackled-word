using UnshackledWord.Application.Features.WordDetail;
using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Application.Tests.Features.WordDetail;

public class ReadingEditionsBuilderTests
{
    private static readonly string[] MainCodes = ["NA28", "NA27", "Tyn", "SBL", "WH", "Treg", "TR", "Byz"];

    private static readonly List<GreekEditionRow> Main = MainCodes
        .Select((c, i) => new GreekEditionRow { Id = (short)(i + 1), Code = c, Name = c, Kind = "edition", IsMain = true, Sort = (short)(i + 1) })
        .ToList();

    private static GreekWordEditionRow Row(string code, short? shift = null, string? raw = null)
    {
        var main = Main.FirstOrDefault(m => m.Code == code);
        return new GreekWordEditionRow
        {
            StepWordId = 1, Code = code, Kind = main?.Kind ?? "manuscript", IsMain = main is not null, Sort = main?.Sort ?? 20, Shift = shift, ShiftRaw = raw,
        };
    }

    private static List<GreekWordEditionRow> All() => MainCodes.Select(c => Row(c)).ToList();

    [Fact]
    public void AllEight_ReturnsNull() => ReadingEditionsBuilder.Build(All(), Main).ShouldBeNull();

    [Fact]
    public void MissingWhAndTreg_ListsThemAsMissing()
    {
        var result = ReadingEditionsBuilder.Build(All().Where(r => r.Code is not ("WH" or "Treg")).ToList(), Main).ShouldNotBeNull();

        result.Missing.Select(m => m.Code).ShouldBe(["WH", "Treg"]);
        result.Present.Select(p => p.Code).ShouldBe(["NA28", "NA27", "Tyn", "SBL", "TR", "Byz"]);
    }

    [Fact]
    public void TrOnly_OnePresentSevenMissing()
    {
        var result = ReadingEditionsBuilder.Build([Row("TR")], Main).ShouldNotBeNull();

        result.Present.Select(p => p.Code).ShouldBe(["TR"]);
        result.Missing.Count.ShouldBe(7);
    }

    [Fact]
    public void AllEightButTrShifted_IsSetWithShiftOnTr()
    {
        var rows = All().Select(r => r.Code == "TR" ? Row("TR", 1) : r).ToList();

        var result = ReadingEditionsBuilder.Build(rows, Main).ShouldNotBeNull();

        result.Missing.ShouldBeEmpty();
        result.Present.Single(p => p.Code == "TR").Shift.ShouldBe(1);
        result.Present.Where(p => p.Code != "TR").ShouldAllBe(p => p.Shift == null);
    }

    [Fact]
    public void ShiftZero_DoesNotCountAndIsOmitted() => ReadingEditionsBuilder.Build(All().Select(r => r.Code == "TR" ? Row("TR", 0) : r).ToList(), Main).ShouldBeNull();

    [Fact]
    public void RawShift_SetsEditionsAndKeepsRaw()
    {
        var rows = All().Select(r => r.Code == "Byz" ? Row("Byz", null, "«14.24") : r).ToList();

        var result = ReadingEditionsBuilder.Build(rows, Main).ShouldNotBeNull();

        result.Present.Single(p => p.Code == "Byz").ShiftRaw.ShouldBe("«14.24");
    }

    [Fact]
    public void ManuscriptAlongsideAllEight_DoesNotSetEditions() => ReadingEditionsBuilder.Build([.. All(), Row("01")], Main).ShouldBeNull();

    [Fact]
    public void ManuscriptWhenSectionShown_IsListedInPresentInCatalogOrder()
    {
        var result = ReadingEditionsBuilder.Build([Row("TR"), Row("01")], Main).ShouldNotBeNull();

        result.Present.Select(p => p.Code).ShouldBe(["TR", "01"]);
        result.Missing.ShouldNotContain(m => m.Code == "01");
    }
}
