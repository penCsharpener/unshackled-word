using UnshackledWord.Infrastructure.Seed;
using System.Text;
using UnshackledWord.Application.Features.Seed.Morphology;
using UnshackledWord.Tooling.SeedDb.Services.RepoSeed;

namespace UnshackledWord.Tooling.SeedDb.Tests.Services.RepoSeed;

public class SeedCsvTests
{
    private static List<MorphologySeedLine> Sample() =>
    [
        new() { Ref = 1001001, Pos = 1, Form = "Im", Lemma = "in", Upos = "ADP", Stts = "APPRART", Feats = "Case=Dat|Number=S" },
        new() { Ref = 1001001, Pos = 2, Form = "Anfang", Lemma = "Anfang, der", Upos = "NOUN", Stts = "NN", Feats = "Gender=M" },
        new() { Ref = 1001001, Pos = 3, Form = "„Gott“", Lemma = "Gott \"El\"", Upos = "NOUN" }
    ];

    [Fact]
    public void Serialize_ShouldWriteHeaderLfLinesAndNoBom_WhenGivenMorphologyLines()
    {
        var (bytes, _) = SeedCsv.Serialize<MorphologySeedLine, MorphologySeedLineMap>(Sample());
        var text = Encoding.UTF8.GetString(bytes);

        bytes.Take(3).ShouldNotBe([0xEF, 0xBB, 0xBF]);
        text.ShouldStartWith("Ref,Pos,Form,Lemma,Upos,Stts,Feats\n");
        text.ShouldNotContain("\r");
        text.ShouldEndWith("\n");
        text.Split('\n').Length.ShouldBe(5); // header + 3 lines + empty after the last LF
    }

    [Fact]
    public void Serialize_ShouldBeByteIdentical_WhenCalledTwiceWithTheSameLines()
    {
        var first = SeedCsv.Serialize<MorphologySeedLine, MorphologySeedLineMap>(Sample());
        var second = SeedCsv.Serialize<MorphologySeedLine, MorphologySeedLineMap>(Sample());

        second.Bytes.ShouldBe(first.Bytes);
        second.Sha256.ShouldBe(first.Sha256);
    }

    [Fact]
    public void Read_ShouldReturnTheSameLines_WhenReadingWhatSerializeWrote()
    {
        var (bytes, _) = SeedCsv.Serialize<MorphologySeedLine, MorphologySeedLineMap>(Sample());

        var lines = SeedCsv.Read<MorphologySeedLine, MorphologySeedLineMap>(bytes);

        lines.Count.ShouldBe(3);
        lines.Select(l => (l.Ref, l.Pos, l.Form, l.Lemma, l.Upos, l.Stts, l.Feats))
            .ShouldBe(Sample().Select(l => (l.Ref, l.Pos, l.Form, l.Lemma, l.Upos, l.Stts, l.Feats)));
        lines[2].HasMorphology.ShouldBeTrue();
    }

    [Fact]
    public void Serialize_ShouldWriteEmptyFields_WhenWordHasNoMorphology()
    {
        var line = new MorphologySeedLine { Ref = 1001001, Pos = 9, Form = "-" };

        var text = Encoding.UTF8.GetString(SeedCsv.Serialize<MorphologySeedLine, MorphologySeedLineMap>([line]).Bytes);

        text.ShouldBe("Ref,Pos,Form,Lemma,Upos,Stts,Feats\n1001001,9,-,,,,\n");
        line.HasMorphology.ShouldBeFalse();
    }

    [Fact]
    public void Serialize_ShouldWriteChapterWordsFingerprint_WhenGivenAnchorRows()
    {
        var rows = new List<AnchorRow> { new() { Chapter = 1001, Words = 743, Fingerprint = "6e60f735226c" } };

        Encoding.UTF8.GetString(SeedCsv.Serialize<AnchorRow, AnchorRowMap>(rows).Bytes).ShouldBe("Chapter,Words,Fingerprint\n1001,743,6e60f735226c\n");
    }
}
