using UnshackledWord.Application.Features.Lxx;

namespace UnshackledWord.Application.Tests.Features.Lxx;

/// <summary>Roadmap 0028.1: the Greek column of the TVTMS condensed table, on snippets copied from the real file.</summary>
public class TvtmsGreekParserTests
{
    private static string Tabs(string text) => text.Replace(" | ", "\t").Replace("|", "\t");

    private static TvtmsGreekTable Parse(string body) =>
        TvtmsGreekParser.Parse(("#DataStart(Condensed)\n" + Tabs(body) + "\n#DataEnd(Condensed)\nOneToOne | Gen.1:1 | Gen.1:1 | Gen.1:1 | Gen.1:1").Split('\n'));

    [Fact]
    public void Ignores_Lines_Outside_The_Condensed_Table()
    {
        var table = TvtmsGreekParser.Parse(["$Gen.1:1 | English KJV | Hebrew | Latin | Greek".Replace(" | ", "\t"), "OneToOne | Gen.1:1 | Gen.1:1 | Gen.1:1 | Gen.1:1".Replace(" | ", "\t")]);

        table.Rows.ShouldBeEmpty();
    }

    [Fact]
    public void Reads_Columns_Per_Record_And_Renumbered_Ranges()
    {
        // Gen 31/32: Hebrew is shifted, Greek follows English.
        var table = Parse("""
            $Gen.31:55--32:32 | English KJV | Hebrew | Latin | Greek
            TEST: If... | Gen.32:32=Last | Gen.32:33=Last | Gen.32:32=Last | Gen.32:32=Last
            OneToOne | Gen.31:55 | Gen.32:1 | Gen.31:55 | Gen.31:55
            OneToOne | Gen.32:1-32 | Gen.32:2-33 | Gen.32:1-32 | Gen.32:1-32
            """);

        table.Rows.Count.ShouldBe(2);
        var entries = table.Entries().ToList();
        entries.Count.ShouldBe(33);
        entries[0].Greek.ShouldBe(new TvtmsRef("Gen", "31", 55, 0));
        entries[0].Hebrew.ShouldBe(new TvtmsRef("Gen", "32", 1, 0));
        entries[^1].Greek.ShouldBe(new TvtmsRef("Gen", "32", 32, 0));
        entries[^1].English.ShouldBe(new TvtmsRef("Gen", "32", 32, 0));
        entries[^1].Hebrew.ShouldBe(new TvtmsRef("Gen", "32", 33, 0));
        entries.ShouldAllBe(e => e.Paired);
    }

    [Fact]
    public void Psalm_Record_Takes_Its_Columns_From_The_Bibles_Row_And_Skips_Test_Rows()
    {
        var table = Parse("""
            $Psa.9:19--10:18
            English KJV | & Psa.10:18=Last
            Hebrew + Greek + Latin | & Psa.10:18=Last
            BIBLES | English KJV | Hebrew | Latin | Greek | EngTitleMerged
            OneToOne | Psa.10:1-18 | Psa.10:1-18 | Psa.9:22-39 | Psa.9:22-39 | Psa.10:1-18
            """);

        var entries = table.Entries().ToList();
        entries.Count.ShouldBe(18);
        entries[0].Greek.ShouldBe(new TvtmsRef("Psa", "9", 22, 0));
        entries[0].English.ShouldBe(new TvtmsRef("Psa", "10", 1, 0));
        entries[0].Hebrew.ShouldBe(new TvtmsRef("Psa", "10", 1, 0));
        entries[17].Greek.ShouldBe(new TvtmsRef("Psa", "9", 39, 0));
    }

    [Fact]
    public void Psalm_Title_Is_Verse_Zero_And_Greek_Title_Has_No_Separate_Verse()
    {
        var table = Parse("""
            $Psa.3:1-3:8
            BIBLES | English KJV | Hebrew | Latin | Greek | EngTitleMerged
            OneToOne | Psa.3:Title | Psa.3:1 | Psa.3:1 | Psa.3:1 | Absent [=Psa.3:1]
            OneToOne | Psa.3:1 | Psa.3:2 | Psa.3:2 | Psa.3:2 | Psa.3:1
            OneToOne | Psa.3:2-8 | Psa.3:3-9 | Psa.3:3-9 | Psa.3:3-9 | Psa.3:2-8
            """);

        var entries = table.Entries().ToList();
        entries.Count.ShouldBe(9);
        entries[0].Greek.ShouldBe(new TvtmsRef("Psa", "3", 1, 0));
        entries[0].English.ShouldBe(new TvtmsRef("Psa", "3", 0, 0));
        entries[0].Hebrew.ShouldBe(new TvtmsRef("Psa", "3", 1, 0));
    }

    [Fact]
    public void Absent_Greek_Cell_Gives_No_Greek_Verse()
    {
        var table = Parse("""
            $Gen.2:24--3:1 | English KJV | Hebrew | Latin | Greek | Greek2 (eg NETS)
            TEST: If... | Gen.2:25=Last | Gen.2:25=Last | Gen.2:25=Last | Gen.2:25=Last | Gen.2:24=Last
            MergedPrevVerse | Gen.2:25 | Gen.2:25 | Gen.2:25 | Gen.2:25 | Absent [=Gen.3:1]
            OneToOne | Gen.3:1 | Gen.3:1 | Gen.3:1 | Gen.3:1 | Gen.3:1
            """);

        table.Entries().Select(e => e.Greek.ToString()).ShouldBe(["Gen.2:25", "Gen.3:1"]);

        var absent = Parse("""
            $Psa.3:1-3:8 | English KJV | Hebrew | Latin | Greek
            OneToOne | Psa.3:Title | Psa.3:1 | Psa.3:1 | Absent
            """);
        absent.Rows.Single().GreekAbsent.ShouldBeTrue();
        absent.Entries().ShouldBeEmpty();
    }

    [Fact]
    public void Greek_Star_Column_And_Other_Greek_Columns_Are_Told_Apart()
    {
        var table = Parse("""
            $Exo.36:8-36:38 | English KJV | Hebrew | Latin | Greek* | GreekUndivided*
            OneToOne | Exo.36:8-9 | Exo.36:8-9 | Exo.36:8-9 | Exo.39:1-2 | Exo.36:8-9
            """);

        table.Entries().Select(e => e.Greek.ToString()).ShouldBe(["Exo.39:1", "Exo.39:2"]);
    }

    [Fact]
    public void Subverses_Ranges_And_Lists_Are_Expanded()
    {
        var table = Parse("""
            $1Ki.2:35 | English KJV | Latin | Greek
            LongVerse | 1Ki.2:35.0 | 1Ki.2:35 | 1Ki.2:35.0
            LongVerse | 1Ki.2:35.1-3 | NoVerse | 1Ki.2:35.1-3
            $Est.1:1-22 | English KJV | Hebrew | Latin* | Greek | GreekUndivided
            LongVerse/LVExtra | Est.1:1.1-17 | NoVerse | Est.A:1-17 | Est.1:1.1-17 | NoVerse
            $Exo.37:1-37:29 | English KJV | Hebrew | Latin | Greek*
            OneToOne | Exo.37:1-2 | Exo.37:1-2 | Exo.37:1-2 | Exo.38:1, 2.1; 3
            """);

        var greek = table.Entries().Select(e => e.Greek.ToString()).ToList();
        greek.ShouldContain("1Ki.2:35");
        greek.ShouldContain("1Ki.2:35.3");
        greek.Where(g => g.StartsWith("Est.1:1.")).Count().ShouldBe(17);
        greek.TakeLast(3).ShouldBe(["Exo.38:1", "Exo.38:2.1", "Exo.38:3"]);
        table.UnparsedCells.ShouldBeEmpty();
    }

    [Fact]
    public void GreekVerses_Adds_The_Verses_No_Row_Mentions_As_Identical()
    {
        var table = Parse("""
            $Psa.3:1-3:8
            BIBLES | English KJV | Hebrew | Latin | Greek | EngTitleMerged
            OneToOne | Psa.3:Title | Psa.3:1 | Psa.3:1 | Psa.3:1 | Absent [=Psa.3:1]
            OneToOne | Psa.3:1 | Psa.3:2 | Psa.3:2 | Psa.3:2 | Psa.3:1
            OneToOne | Psa.3:2-8 | Psa.3:3-9 | Psa.3:3-9 | Psa.3:3-9 | Psa.3:2-8
            """);

        var verses = table.GreekVerses(book => book switch { "Psa" => [8, 8, 8, 8], "Gen" => [31], _ => null });

        verses[("Psa", "3")].Verses.ShouldBe([1, 2, 3, 4, 5, 6, 7, 8, 9]);
        verses[("Psa", "4")].Verses.Count.ShouldBe(8); // not in any row: English counting is Greek counting
        verses[("Gen", "1")].Verses.Count.ShouldBe(31);
    }
}
