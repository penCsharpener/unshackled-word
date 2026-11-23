using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Seed;
using UnshackledWord.Infrastructure.Services;

namespace UnshackledWord.Infrastructure.Tests.Seed;

public class SeedFileExportServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"uw-seedfiles-{Guid.NewGuid():N}");
    private readonly string _assets;

    public SeedFileExportServiceTests()
    {
        _assets = Path.Combine(_root, "assets");
        Directory.CreateDirectory(_assets);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private SeedFileExportService Create() => new(
        null!,
        new FileService(),
        Options.Create(new AppSettings { DatabaseSeeding = new DatabaseSeedSettings { SolutionAssetsPath = _assets } }),
        NullLogger<SeedFileExportService>.Instance);

    private void Write(string relative, string content)
    {
        var path = Path.Combine(_assets, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    [Fact]
    public void Compare_ShouldSeparateChangedCreatedAndRemovedFiles()
    {
        var older = new Dictionary<string, string> { ["a.csv"] = "1", ["b.csv"] = "2", ["gone.csv"] = "3" };
        var newer = new Dictionary<string, string> { ["a.csv"] = "1", ["b.csv"] = "X", ["new.csv"] = "4" };

        var diff = SeedFileExportService.Compare(newer, older);

        diff.Identical.ShouldBe(1);
        diff.Compared.ShouldBe(4);
        diff.Changed.Items.ShouldBe(["b.csv"]);
        diff.OnlyInNew.Items.ShouldBe(["new.csv"]);
        diff.OnlyInOld.Items.ShouldBe(["gone.csv"]);
        diff.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void Compare_ShouldBeEmpty_WhenBothTreesHaveTheSameBytes()
    {
        var files = new Dictionary<string, string> { ["revision/levels.csv"] = "h" };

        SeedFileExportService.Compare(files, new Dictionary<string, string>(files)).IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public async Task Snapshot_ShouldOnlyListTheFilesOfTheKind()
    {
        Write("seed/manifest.json", "{}");
        Write("seed/revision/levels.csv", "Level\n100\n");
        Write("seed/revision/verses/43-John.csv", "Ref\n");
        Write("seed/morphology/01-Genesis.csv", "m");
        Write("seed/anchors/01-Genesis.csv", "a");
        Write("seed/tsk/curations.csv", "source\n");
        Write("Elb1871Mappings/01-Genesis.csv", "g");
        Write("Elb1871Mappings/notes.txt", "not a seed file");
        var service = Create();
        var layout = service.CreateRepoLayout();

        var revision = await service.SnapshotAsync(layout, SeedExportKind.Revision, CancellationToken.None);
        var mappings = await service.SnapshotAsync(layout, SeedExportKind.Mappings, CancellationToken.None);
        var all = await service.SnapshotAsync(layout, SeedExportKind.All, CancellationToken.None);

        revision.Keys.Order().ShouldBe(["manifest.json", "revision/levels.csv", "revision/verses/43-John.csv"]);
        mappings.Keys.Order().ShouldBe(["Elb1871Mappings/01-Genesis.csv", "manifest.json"]);
        all.Keys.Count.ShouldBe(7);
        all.Keys.ShouldContain("tsk/curations.csv");
    }

    [Fact]
    public async Task Snapshot_ShouldDetectAOneByteChange()
    {
        Write("seed/revision/levels.csv", "Level\n100\n");
        var service = Create();
        var before = await service.SnapshotAsync(service.CreateRepoLayout(), SeedExportKind.Revision, CancellationToken.None);

        Write("seed/revision/levels.csv", "Level\n101\n");
        var after = await service.SnapshotAsync(service.CreateRepoLayout(), SeedExportKind.Revision, CancellationToken.None);

        SeedFileExportService.Compare(after, before).Changed.Items.ShouldBe(["revision/levels.csv"]);
    }

    [Fact]
    public void CheckAssetsAvailable_ShouldReportAMissingFolder()
    {
        Create().CheckAssetsAvailable().ShouldBeNull();

        var missing = new SeedFileExportService(
            null!,
            new FileService(),
            Options.Create(new AppSettings { DatabaseSeeding = new DatabaseSeedSettings { SolutionAssetsPath = Path.Combine(_root, "nope") } }),
            NullLogger<SeedFileExportService>.Instance);

        missing.CheckAssetsAvailable().ShouldNotBeNull();
    }

    [Fact]
    public void SeedStepReport_ShouldCapLongListsButKeepTheTotal()
    {
        var report = new SeedStepReport().AddList("kept", Enumerable.Range(0, 500).ToList(), i => $"v{i}");

        report.Lists["kept"].Total.ShouldBe(500);
        report.Lists["kept"].Items.Count.ShouldBe(SeedStepReport.MaxListItems);
    }

    [Theory]
    [InlineData(43003016, null, "John 3:16")]
    [InlineData(43003016, 200, "John 3:16 · 200")]
    public void Verse_ShouldFormatTheReference(int hebRefId, int? level, string expected)
    {
        SeedVerseFormat.Verse(hebRefId, level).ShouldBe(expected);
    }
}
