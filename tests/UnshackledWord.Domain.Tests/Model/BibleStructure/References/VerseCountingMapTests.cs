using UnshackledWord.Domain.Models.BibleStructure.References;

namespace UnshackledWord.Domain.Tests.Model.BibleStructure.References;

public class VerseCountingMapTests
{
    private const int Mal = 39;
    private const int Ps = 19;
    private const int Joel = 29;
    private const int Joh = 43;

    // Rows copied from BibleVerseCountingMapping (LxxRefId, HebRefId), differing rows only.
    private static readonly VerseCountingMap Map = new(
    [
        new(39004001, 39003019), new(39004002, 39003020), new(39004003, 39003021), new(39004004, 39003022), new(39004005, 39003023), new(39004006, 39003024),
        new(19003001, 19003002),
        new(19051000, 19051001), new(19051000, 19051002), new(19051001, 19051003), new(19051002, 19051004),
        new(29002028, 29003001), new(29002029, 29003002), new(29002030, 29003003), new(29002031, 29003004), new(29002032, 29003005),
        new(29003001, 29004001),
    ]);

    [Fact]
    public void MapStart_And_MapEnd_Mal4_5() => (Map.MapStart(39004005), Map.MapEnd(39004005)).ShouldBe((39003023, 39003023));

    [Fact]
    public void MapSpan_Mal4_5_BecomesMal3_23() => Map.MapSpan(Mal, new VerseSpan(4, 5, 4, 5)).ShouldBe(new VerseSpan(3, 23, 3, 23));

    [Fact]
    public void MapSpan_WholeChapterMal4_UsesFirstAndLastEnglishVerse() =>
        Map.MapSpan(Mal, new VerseSpan(4, 0, 4, 999)).ShouldBe(new VerseSpan(3, 19, 3, 24));

    [Fact]
    public void MapSpan_Ps3_1_BecomesPs3_2() => Map.MapSpan(Ps, new VerseSpan(3, 1, 3, 1)).ShouldBe(new VerseSpan(3, 2, 3, 2));

    [Fact]
    public void MapSpan_Ps51_1To2_BecomesPs51_3To4() => Map.MapSpan(Ps, new VerseSpan(51, 1, 51, 2)).ShouldBe(new VerseSpan(51, 3, 51, 4));

    [Fact]
    public void MapSpan_Ps51_0_OneToMany_StartTakesSmallest_EndTakesLargest()
    {
        Map.MapStart(19051000).ShouldBe(19051001);
        Map.MapEnd(19051000).ShouldBe(19051002);
        Map.MapSpan(Ps, new VerseSpan(51, 0, 51, 0)).ShouldBe(new VerseSpan(51, 1, 51, 2));
    }

    [Fact]
    public void MapSpan_Joel2_28To32_BecomesJoel3_1To5() => Map.MapSpan(Joel, new VerseSpan(2, 28, 2, 32)).ShouldBe(new VerseSpan(3, 1, 3, 5));

    [Fact]
    public void MapSpan_Joel3_1_BecomesJoel4_1() => Map.MapSpan(Joel, new VerseSpan(3, 1, 3, 1)).ShouldBe(new VerseSpan(4, 1, 4, 1));

    [Fact]
    public void MapSpan_Joh3_16_IsUnchanged() => Map.MapSpan(Joh, new VerseSpan(3, 16, 3, 16)).ShouldBe(new VerseSpan(3, 16, 3, 16));

    [Fact]
    public void UnknownId_IsUnchanged()
    {
        Map.MapStart(43003016).ShouldBe(43003016);
        Map.MapEnd(99999999).ShouldBe(99999999);
        new VerseCountingMap([]).MapSpan(Joh, new VerseSpan(3, 16, 3, 18)).ShouldBe(new VerseSpan(3, 16, 3, 18));
    }

    [Fact]
    public void MapSpan_WholeChapterWithoutMapping_StaysWholeChapterSized()
    {
        var span = new VerseCountingMap([]).MapSpan(Joh, new VerseSpan(3, 0, 3, 999));
        span.ShouldBe(new VerseSpan(3, 1, 3, 36));
    }
}
