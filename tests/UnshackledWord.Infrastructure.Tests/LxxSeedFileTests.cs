using System.Text;
using System.Text.RegularExpressions;
using UnshackledWord.Infrastructure.Seed.Lxx;

namespace UnshackledWord.Infrastructure.Tests;

/// <summary>Roadmap 0028.8: the format of <c>lxx/tags.csv</c> and <c>links.csv</c> without a database (header, LF, round trip, no Greek or Hebrew script).</summary>
public partial class LxxSeedFileTests
{
    [GeneratedRegex("[Ͱ-Ͽἀ-῿֐-׿]")]
    private static partial Regex GreekOrHebrew();

    private static readonly LxxTagSeedLine[] Tags =
    [
        new() { RahlfsRef = 800_100_100, TextForm = "", Position = 1, SrcHash = "0a1b2c3d", Strongs = "G2532", Morphology = "CONJ", Source = "ai" },
        new() { RahlfsRef = 2_700_100_100, TextForm = "og", Position = 2, SrcHash = "deadbeef", Strongs = "G20969", Morphology = "V-AAI-3S", Source = "human" },
    ];

    private static readonly LxxLinkSeedLine[] Links =
    [
        new() { RahlfsRef = 800_100_100, TextForm = "", Position = 1, SrcHash = "0a1b2c3d", HebLxxRefId = 8_001_001, HebPosition = 3, HebSrcHash = "12345678" },
    ];

    [Fact]
    public void Tags_HaveAHeaderLfLineEndingsAndRoundTrip()
    {
        var (bytes, hash) = LxxSeed.SerializeTags(Tags);
        var text = Encoding.UTF8.GetString(bytes);

        text.ShouldBe("""
            RahlfsRef,TextForm,Position,SrcHash,Strongs,Morphology,Source
            800100100,,1,0a1b2c3d,G2532,CONJ,ai
            2700100100,og,2,deadbeef,G20969,V-AAI-3S,human

            """.ReplaceLineEndings("\n"));
        hash.Length.ShouldBe(64);
        LxxSeed.ParseTags(bytes).Select(l => (l.RahlfsRef, l.TextForm, l.Position, l.SrcHash, l.Strongs, l.Morphology, l.Source))
            .ShouldBe(Tags.Select(l => (l.RahlfsRef, l.TextForm, l.Position, l.SrcHash, l.Strongs, l.Morphology, l.Source)));
        LxxSeed.SerializeTags(Tags).Sha256.ShouldBe(hash);
    }

    [Fact]
    public void Links_HaveAHeaderAndRoundTrip()
    {
        var (bytes, _) = LxxSeed.SerializeLinks(Links);
        var text = Encoding.UTF8.GetString(bytes);

        text.ShouldBe("RahlfsRef,TextForm,Position,SrcHash,HebLxxRefId,HebPosition,HebSrcHash\n800100100,,1,0a1b2c3d,8001001,3,12345678\n");
        var back = LxxSeed.ParseLinks(bytes).Single();
        (back.RahlfsRef, back.TextForm, back.Position, back.SrcHash, back.HebLxxRefId, back.HebPosition, back.HebSrcHash)
            .ShouldBe((800_100_100, "", 1, "0a1b2c3d", 8_001_001, 3, "12345678"));
    }

    [Fact]
    public void TheFilesCarryNoGreekOrHebrewScript()
    {
        GreekOrHebrew().IsMatch(Encoding.UTF8.GetString(LxxSeed.SerializeTags(Tags).Bytes)).ShouldBeFalse();
        GreekOrHebrew().IsMatch(Encoding.UTF8.GetString(LxxSeed.SerializeLinks(Links).Bytes)).ShouldBeFalse();
    }

    [Fact]
    public void ReadingAcceptsCrLf_AndAnEmptySourceBecomesNullOrEmpty()
    {
        var bytes = Encoding.UTF8.GetBytes("RahlfsRef,TextForm,Position,SrcHash,Strongs,Morphology,Source\r\n800100100,,1,0a1b2c3d,G2532,CONJ,\r\n");

        var line = LxxSeed.ParseTags(bytes).Single();

        string.IsNullOrEmpty(line.Source).ShouldBeTrue();
        line.TextForm.ShouldBe(string.Empty);
    }

    [Fact]
    public void TheCommittedEmptySeedIsHeaderOnly()
    {
        var root = AppContext.BaseDirectory;
        while (root is not null && !Directory.Exists(Path.Combine(root, "assets", "seed")))
        {
            root = Path.GetDirectoryName(root);
        }

        Assert.SkipWhen(root is null, "assets/seed not found");
        File.ReadAllText(Path.Combine(root!, "assets", "seed", "lxx", "tags.csv")).ShouldBe("RahlfsRef,TextForm,Position,SrcHash,Strongs,Morphology,Source\n");
        File.ReadAllText(Path.Combine(root!, "assets", "seed", "lxx", "links.csv")).ShouldBe("RahlfsRef,TextForm,Position,SrcHash,HebLxxRefId,HebPosition,HebSrcHash\n");
    }
}
