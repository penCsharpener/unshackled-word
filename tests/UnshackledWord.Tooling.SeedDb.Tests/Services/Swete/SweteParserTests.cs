using UnshackledWord.Seeding.Swete;

namespace UnshackledWord.Tooling.SeedDb.Tests.Services.Swete;

/// <summary>Roadmap 0028.1: Swete's versification file maps verses to the first word id of the word file.</summary>
public class SweteParserTests
{
    private static readonly string[] Versification = ["1\tGen.1:1", "11\tGen.1:2", "30\tGen.1:3"];

    private static readonly string[] Words =
    [
        "1\tΕΝ", "2\tΑΡΧΗ", "3\tἐποίησεν", "4\tὁ", "5\tθεὸς", "6\tτὸν", "7\tοὐρανὸν", "8\tκαὶ", "9\tτὴν", "10\tγῆν.",
        "11\tἡ", "12\tδὲ", "13\tγῆ", "29\tἀβύσσου,", "30\tκαὶ", "31\tεἶπεν",
    ];

    [Fact]
    public void A_Verse_Owns_The_Words_From_Its_Id_To_The_Next_Verse()
    {
        var verses = SweteParser.Parse(Versification, Words);

        verses.Count.ShouldBe(3);
        verses[0].Reference.ShouldBe("Gen.1:1");
        verses[0].Words.Count.ShouldBe(10);
        verses[0].Words[^1].Text.ShouldBe("γῆν.");
        verses[0].Words[^1].Position.ShouldBe(10);
        verses[1].Words.Select(w => w.Id).ShouldBe([11, 12, 13, 29]);
        verses[1].Words.Select(w => w.Position).ShouldBe([1, 2, 3, 4]);
        verses[2].Words.Select(w => w.Text).ShouldBe(["καὶ", "εἶπεν"]);
    }

    [Fact]
    public void Reads_Book_Codes_Starting_With_A_Digit_And_Tolerates_Cr_And_Blank_Lines()
    {
        var verses = SweteParser.Parse(["1\t1Ki.2:35\r", ""], ["1\tκαὶ\r", "2\tἔδωκεν\r"]);

        verses.Single().BookCode.ShouldBe("1Ki");
        verses.Single().Chapter.ShouldBe(2);
        verses.Single().Verse.ShouldBe(35);
        verses.Single().Words.Count.ShouldBe(2);
    }

    [Fact]
    public void Rejects_Malformed_Lines_And_Descending_Ids()
    {
        Should.Throw<FormatException>(() => SweteParser.Parse(["1\tGen.1"], Words));
        Should.Throw<FormatException>(() => SweteParser.Parse(["5\tGen.1:1", "3\tGen.1:2"], Words));
        Should.Throw<FormatException>(() => SweteParser.Parse(Versification, ["1\ta", "x"]));
    }
}
