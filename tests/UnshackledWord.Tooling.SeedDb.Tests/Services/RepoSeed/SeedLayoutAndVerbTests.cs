using UnshackledWord.Infrastructure.Seed;
using UnshackledWord.Tooling.SeedDb.Services.RepoSeed;
using UnshackledWord.Tooling.SeedDb.Services.RepoSeed.Verbs;

namespace UnshackledWord.Tooling.SeedDb.Tests.Services.RepoSeed;

public class SeedLayoutAndVerbTests
{
    [Theory]
    [InlineData(1, "01-Genesis.csv")]
    [InlineData(22, "22-Song of Solomon.csv")]
    [InlineData(46, "46-1 Corinthians.csv")]
    [InlineData(66, "66-Revelation.csv")]
    public void BookFileName_ShouldMatchTheMappingFileNames_WhenGivenABookId(int bookId, string expected)
    {
        SeedLayout.BookFileName(bookId).ShouldBe(expected);
        SeedLayout.RelativePath(SeedLayout.MorphologyFolder, bookId).ShouldBe($"morphology/{expected}");
    }

    [Fact]
    public void Compute_ShouldChange_WhenAFileHashChangesAndStayWhenNot()
    {
        var files = new List<SeedFileEntry> { new() { Path = "morphology/01-Genesis.csv", Rows = 1, Sha256 = "aa" } };
        var same = new List<SeedFileEntry> { new() { Path = "morphology/01-Genesis.csv", Rows = 1, Sha256 = "aa" } };
        var changed = new List<SeedFileEntry> { new() { Path = "morphology/01-Genesis.csv", Rows = 1, Sha256 = "ab" } };

        SeedVersion.Compute(files).ShouldBe(SeedVersion.Compute(same));
        SeedVersion.Compute(files).ShouldNotBe(SeedVersion.Compute(changed));
        SeedVersion.Compute(files).Length.ShouldBe(64);
    }

    [Theory]
    [InlineData(new[] { "export-seed", "--only", "morphology" }, "export-seed", "morphology")]
    [InlineData(new[] { "anchor-check" }, "anchor-check", null)]
    [InlineData(new[] { "--environment", "Development" }, null, null)]
    [InlineData(new string[0], null, null)]
    public void VerbArguments_ShouldTakeTheFirstBareArgumentAsVerb_WhenCommandLineIsParsed(string[] args, string? verb, string? only)
    {
        var arguments = new VerbArguments(args);

        arguments.Verb.ShouldBe(verb);
        arguments.Get("--only").ShouldBe(only);
    }

    [Fact]
    public void Get_ShouldReturnNull_WhenOptionHasNoValue()
    {
        var arguments = new VerbArguments(["export-seed", "--only", "--out", "x"]);

        arguments.Get("--only").ShouldBeNull();
        arguments.Get("--out").ShouldBe("x");
        arguments.Has("--only").ShouldBeTrue();
    }
}
