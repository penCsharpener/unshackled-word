using UnshackledWord.Infrastructure.Seed;
using UnshackledWord.Infrastructure.Seed.Mappings;
using System.Text;
using UnshackledWord.Application.Features.Seed.Mappings;
using UnshackledWord.Tooling.SeedDb.Services.RepoSeed;
using UnshackledWord.Tooling.SeedDb.Services.RepoSeed.Mappings;

namespace UnshackledWord.Tooling.SeedDb.Tests.Services.RepoSeed.Mappings;

public class MappingSeedFormatTests
{
    [Theory]
    [InlineData("HebRefId,PositionInVerse,GermanWordPart,ElbWord,StepWord,StepPositionInVerse,IsAddedWord,ParentPositionInVerse,ParentWord", MappingSeedFormat.Legacy)]
    [InlineData("Ref,Pos,Form,Part,SrcRef,SrcPos,SrcHash,Added,ParentPos,ParentForm,ParentRef", MappingSeedFormat.Anchored)]
    [InlineData("", MappingSeedFormat.Legacy)]
    [InlineData(null, MappingSeedFormat.Legacy)]
    public void DetectFromHeader_ShouldTellTheFormatsApart(string? header, int expected)
    {
        MappingSeedFormat.DetectFromHeader(header).ShouldBe(expected);
    }

    [Fact]
    public void Serialize_ShouldWriteTheFormat2Header_AndReadBackTheSameLines()
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

        text.ShouldStartWith("Ref,Pos,Form,Part,SrcRef,SrcPos,SrcHash,Added,ParentPos,ParentForm,ParentRef\n");
        text.ShouldContain("1001001,1,Im,,,,,True,2,Anfang,\n");
        MappingSeedFormat.DetectFromHeader(text.Split('\n')[0]).ShouldBe(MappingSeedFormat.Anchored);
        back.ShouldBeEquivalentTo(lines);
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
}
