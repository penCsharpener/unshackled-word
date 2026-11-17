using UnshackledWord.Infrastructure.Seed;
using UnshackledWord.Infrastructure.Seed.Mappings;
using System.Text;
using UnshackledWord.Application.Features.Seed.Mappings;
using UnshackledWord.Application.Features.Seed.Revision;
using UnshackledWord.Infrastructure.Seed.Revision;

namespace UnshackledWord.Infrastructure.Tests.Seed;

public class MappingSeedFormatTests
{
    [Theory]
    [InlineData("HebRefId,PositionInVerse,GermanWordPart,ElbWord,StepWord,StepPositionInVerse,IsAddedWord,ParentPositionInVerse,ParentWord", MappingSeedFormat.Legacy)]
    [InlineData("Ref,Pos,Form,Part,SrcRef,SrcPos,SrcHash,Added,ParentPos,ParentForm,ParentRef", MappingSeedFormat.Anchored)]
    [InlineData("Ref,Pos,Form,Part,SrcRef,SrcPos,SrcHash,Added,ParentPos,ParentForm,ParentRef,Role,Strongs,PartLemma,MultiHead", MappingSeedFormat.Roles)]
    [InlineData("", MappingSeedFormat.Legacy)]
    [InlineData(null, MappingSeedFormat.Legacy)]
    public void DetectFromHeader_ShouldTellTheFormatsApart(string? header, int expected)
    {
        MappingSeedFormat.DetectFromHeader(header).ShouldBe(expected);
    }

    [Fact]
    public void Serialize_ShouldWriteTheFormat3Header_AndReadBackTheSameLines()
    {
        var lines = new List<MappingSeedLine>
        {
            new() { Ref = 1001001, Pos = 1, Form = "Im", Added = true, ParentPos = 2, ParentForm = "Anfang" },
            new() { Ref = 1001001, Pos = 2, Form = "Anfang", Part = "An", SrcRef = 1001001, SrcPos = 1, SrcHash = "26358cad" },
            new() { Ref = 1001002, Pos = 1, Form = "Und, dann", SrcRef = 1001001, SrcPos = 7, SrcHash = "0b44bf95", ParentPos = 3, ParentForm = "x", ParentRef = 1001003 }
        };

        var (bytes, _) = SeedCsv.Serialize<MappingSeedLine, MappingSeedLineMap>(lines);
        var text = Encoding.UTF8.GetString(bytes);
        var back = SeedCsv.Read<MappingSeedLine, MappingSeedLineMap>(bytes);

        text.ShouldStartWith("Ref,Pos,Form,Part,SrcRef,SrcPos,SrcHash,Added,ParentPos,ParentForm,ParentRef,Role,Strongs,PartLemma,MultiHead\n");
        text.ShouldContain("1001001,1,Im,,,,,True,2,Anfang,,,,,\n"); // defaults (head, none, none, false) are empty cells
        MappingSeedFormat.DetectFromHeader(text.Split('\n')[0]).ShouldBe(MappingSeedFormat.Roles);
        back.ShouldBeEquivalentTo(lines);
    }

    [Fact]
    public void Serialize_ShouldRoundTripRoleStrongsPartLemmaAndMultiHead()
    {
        var lines = new List<MappingSeedLine>
        {
            new() { Ref = 1001001, Pos = 1, Form = "Und", SrcRef = 1001001, SrcPos = 1, SrcHash = "26358cad", Strongs = "H9001" },
            new() { Ref = 1001001, Pos = 2, Form = "wurde", Role = "unit", SrcRef = 1001001, SrcPos = 2, SrcHash = "0b44bf95", MultiHead = true },
            new() { Ref = 1001001, Pos = 3, Form = "Gerstenernte", Part = "Gersten", PartLemma = "Gerste", SrcRef = 1001001, SrcPos = 3, SrcHash = "0b44bf96" }
        };

        var (bytes, _) = SeedCsv.Serialize<MappingSeedLine, MappingSeedLineMap>(lines);
        var text = Encoding.UTF8.GetString(bytes);

        text.ShouldContain("1001001,2,wurde,,1001001,2,0b44bf95,False,,,,unit,,,true\n");
        text.ShouldContain("1001001,3,Gerstenernte,Gersten,1001001,3,0b44bf96,False,,,,,,Gerste,\n");
        SeedCsv.Read<MappingSeedLine, MappingSeedLineMap>(bytes).ShouldBeEquivalentTo(lines);
    }

    [Fact]
    public void Read_ShouldImportAFormat2File_WithTheDefaultRoleStrongsPartLemmaAndMultiHead()
    {
        const string old = "Ref,Pos,Form,Part,SrcRef,SrcPos,SrcHash,Added,ParentPos,ParentForm,ParentRef\n"
            + "1001001,1,Im,,,,,True,2,Anfang,\n"
            + "1001001,2,Anfang,An,1001001,1,26358cad,False,,,\n";
        var bytes = Encoding.UTF8.GetBytes(old);

        MappingSeedFormat.DetectFromHeader(old.Split('\n')[0]).ShouldBe(MappingSeedFormat.Anchored);
        MappingSeedFormat.IsAnchored(MappingSeedFormat.Anchored).ShouldBeTrue();

        var lines = SeedCsv.Read<MappingSeedLine, MappingSeedLineMap>(bytes);

        lines.Count.ShouldBe(2);
        lines.ShouldAllBe(l => l.Role == "head" && l.Strongs == "" && l.PartLemma == "" && !l.MultiHead);
        lines[1].Part.ShouldBe("An");
        lines[1].SrcHash.ShouldBe("26358cad");
    }

    [Fact]
    public void RevisionMapping_ShouldRoundTripTheNewFields_AndReadAnOldFileWithDefaults()
    {
        var lines = new List<RevisionMappingSeedLine>
        {
            new() { Ref = 1001001, Level = 100, Pos = 1, Form = "Und", SrcRef = 1001001, SrcPos = 1, SrcHash = "26358cad", Origin = "seed", Strongs = "H9001" },
            new() { Ref = 1001001, Level = 100, Pos = 2, Form = "wurde", Role = "unit", MultiHead = true, Origin = "manual" },
            new() { Ref = 1001001, Level = 100, Pos = 3, Form = "Gerstenernte", Part = "Gersten", PartLemma = "Gerste", Origin = "seed" }
        };

        var (bytes, _) = SeedCsv.Serialize<RevisionMappingSeedLine, RevisionMappingSeedLineMap>(lines);
        var text = Encoding.UTF8.GetString(bytes);

        text.ShouldStartWith("Ref,Level,Pos,Form,Part,SrcRef,SrcPos,SrcHash,Added,ParentPos,ParentForm,Origin,Role,Strongs,PartLemma,MultiHead\n");
        text.ShouldContain("1001001,100,2,wurde,,,,,False,,,manual,unit,,,true\n");
        SeedCsv.Read<RevisionMappingSeedLine, RevisionMappingSeedLineMap>(bytes).ShouldBeEquivalentTo(lines);

        const string old = "Ref,Level,Pos,Form,Part,SrcRef,SrcPos,SrcHash,Added,ParentPos,ParentForm,Origin\n1001001,100,1,Im,,,,,True,2,Anfang,seed\n";
        var oldLines = SeedCsv.Read<RevisionMappingSeedLine, RevisionMappingSeedLineMap>(Encoding.UTF8.GetBytes(old));

        oldLines.Single().ShouldBeEquivalentTo(new RevisionMappingSeedLine { Ref = 1001001, Level = 100, Pos = 1, Form = "Im", Added = true, ParentPos = 2, ParentForm = "Anfang", Origin = "seed" });
    }

    [Fact]
    public void ComputeHash_ShouldStayTheSame_ForDefaultsAndChange_WhenRoleStrongsPartLemmaOrMultiHeadDiffer()
    {
        MappingRowPlan Row() => new(1001001, 1, 1, 1, 1001001, 1, "Gersten", false, null, 0, 0);

        var plain = MappingSeedImporter.ComputeHash([Row()]);

        MappingSeedImporter.ComputeHash([Row() with { Role = "head", Strongs = null, PartLemma = null, MultiHead = false }]).ShouldBe(plain);
        MappingSeedImporter.ComputeHash([Row() with { Role = "unit" }]).ShouldNotBe(plain);
        MappingSeedImporter.ComputeHash([Row() with { Strongs = "H9001" }]).ShouldNotBe(plain);
        MappingSeedImporter.ComputeHash([Row() with { PartLemma = "Gerste" }]).ShouldNotBe(plain);
        MappingSeedImporter.ComputeHash([Row() with { MultiHead = true }]).ShouldNotBe(plain);
    }

    [Fact]
    public void Read_ShouldAcceptCrlf_WhenTheFilesWereCheckedOutOnWindows()
    {
        var (bytes, _) = SeedCsv.Serialize<MappingSeedLine, MappingSeedLineMap>([new MappingSeedLine { Ref = 1001001, Pos = 1, Form = "Im" }]);
        var crlf = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace("\n", "\r\n"));

        SeedCsv.Read<MappingSeedLine, MappingSeedLineMap>(crlf).Single().Form.ShouldBe("Im");
    }

    [Theory]
    [InlineData("Schaf", false)]
    [InlineData("hirten", false)]
    [InlineData("טֻמְאָת", true)]
    [InlineData("λόγος", true)]
    [InlineData("ἀγάπη", true)]
    public void ContainsOriginalLanguageLetters_ShouldFlagHebrewAndGreekText(string part, bool expected)
    {
        MappingExportRepository.ContainsOriginalLanguageLetters(part).ShouldBe(expected);
    }

    [Fact]
    public void Decide_ShouldReplaceKeepAndAdoptPerVerse()
    {
        MappingRowPlan Row(int verse, int pos) => new(verse, pos, 1, 1, verse, pos, null, false, null, 0, 0);
        VerseRows Rows(int verse, int pos) => new(false, [Row(verse, pos)]);
        var hash = (int verse, int pos) => MappingSeedImporter.ComputeHash([Row(verse, pos)]);

        var desired = new Dictionary<int, VerseRows>
        {
            [1] = Rows(1, 1),   // seed changed, untouched locally -> Replaced
            [2] = Rows(2, 1),   // seed changed, edited locally -> KeptLocal
            [3] = Rows(3, 1),   // seed equals the database already -> Unchanged
            [4] = Rows(4, 1),   // new in the seed, nothing in the database -> New
            [6] = Rows(6, 1)    // new in the seed, but the database has own rows -> KeptLocal
        };
        var state = new Dictionary<int, string> { [1] = hash(1, 9), [2] = hash(2, 9), [3] = hash(3, 9), [5] = hash(5, 9) };
        var database = new Dictionary<int, string> { [1] = hash(1, 9), [2] = hash(2, 7), [3] = hash(3, 1), [5] = hash(5, 9), [6] = hash(6, 5) };

        var outcomes = MappingSeedImporter.Decide(desired, state, database);

        outcomes[1].ShouldBe(VerseOutcome.Replaced);
        outcomes[2].ShouldBe(VerseOutcome.KeptLocal);
        outcomes[3].ShouldBe(VerseOutcome.Unchanged);
        outcomes[4].ShouldBe(VerseOutcome.New);
        outcomes[5].ShouldBe(VerseOutcome.Removed);
        outcomes[6].ShouldBe(VerseOutcome.KeptLocal);
    }

    [Fact]
    public void MappingsRoot_ShouldStayBelowTheOutFolder_WhenTheExportIsRedirected()
    {
        var fileService = Substitute.For<UnshackledWord.Application.Abstractions.IFileService>();
        fileService.Combine(Arg.Any<string[]>()).Returns(call => string.Join('/', call.Arg<string[]>()));
        var settings = new UnshackledWord.Domain.Models.Settings.DatabaseSeedSettings { SolutionAssetsPath = "assets", SolutionTempPath = "temp" };

        new SeedLayout(fileService, settings).MappingsRoot.ShouldBe("assets/Elb1871Mappings");
        new SeedLayout(fileService, settings, "out").MappingsRoot.ShouldBe("out/Elb1871Mappings");
    }

    [Theory]
    [InlineData("Gersten", "Gerstenernte", "Gersten")]
    [InlineData("Gerstenernte", "Gerstenernte", "")]   // the whole word is no part
    [InlineData("1", "Gerstenernte", "")]
    [InlineData("null", "Gerstenernte", "")]
    [InlineData("und", "Gerstenernte", "")]
    [InlineData("\u03BB\u03CC\u03B3\u03BF\u03C2", "Gerstenernte", "")]   // Greek text in the part column
    public void CleanPart_ShouldExportOnlyRealCompoundParts(string part, string form, string expected)
    {
        MappingExportRepository.CleanPart(part, form).ShouldBe(expected);
    }

    [Fact]
    public void ApplyParts_ShouldDropJunkParts_AndDeriveTheOrderFromThePositionInTheWord()
    {
        MappingRowPlan Row(int wordId, int stepId, string? part) => new(8001022, 28, wordId, stepId, 8001022, stepId, part, false, null, 0, 0);
        var words = new List<ElbWordRow>
        {
            new() { Id = 1, Ref = 8001022, Pos = 28, Form = "Gerstenernte" },
            new() { Id = 2, Ref = 8001022, Pos = 27, Form = "der" },
            new() { Id = 3, Ref = 8001022, Pos = 26, Form = "Beginn" },
        };
        var rows = new List<MappingRowPlan> { Row(1, 10, "ernte"), Row(1, 11, "Gersten"), Row(2, 12, "der"), Row(3, 13, "1"), Row(3, 14, null) };

        var result = MappingSeedImporter.ApplyParts(rows, words);

        result.Select(r => (r.StepWordId, r.Part, r.PartOrder)).ToArray().ShouldBe(
        [
            ((int?)10, "ernte", (int?)2),
            ((int?)11, "Gersten", (int?)1),
            ((int?)12, (string?)null, (int?)null),
            ((int?)13, (string?)null, (int?)null),
            ((int?)14, (string?)null, (int?)null),
        ]);
    }
}
