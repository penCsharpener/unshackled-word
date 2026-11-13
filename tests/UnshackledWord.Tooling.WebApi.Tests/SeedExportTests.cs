using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Seed;
using UnshackledWord.Infrastructure.Services;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.ExportSeed;
using UnshackledWord.Tooling.WebApi.Models;

namespace UnshackledWord.Tooling.WebApi.Tests;

public class SeedExportCoordinatorTests
{
    [Fact]
    public void SecondEntry_IsRefused_UntilTheFirstExits()
    {
        var coordinator = new SeedExportCoordinator();

        coordinator.TryEnter().ShouldBeTrue();
        coordinator.IsRunning.ShouldBeTrue();
        coordinator.TryEnter().ShouldBeFalse();

        coordinator.Exit();

        coordinator.IsRunning.ShouldBeFalse();
        coordinator.TryEnter().ShouldBeTrue();
    }
}

public class ExportSeedValidatorTests
{
    [Theory]
    [InlineData("all")]
    [InlineData("Morphology")]
    [InlineData("mappings")]
    public void KnownKinds_AreValid(string kind)
    {
        new ExportSeedValidator().Validate(new ExportSeedRequest { Kind = kind }).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("anchors")]
    public void UnknownKinds_AreRejected(string kind)
    {
        new ExportSeedValidator().Validate(new ExportSeedRequest { Kind = kind }).IsValid.ShouldBeFalse();
    }
}

public class SeedZipBuilderAvailabilityTests : IDisposable
{
    private readonly string _assets = Path.Combine(Path.GetTempPath(), $"seed-zip-test-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_assets))
        {
            Directory.Delete(_assets, recursive: true);
        }
    }

    private SeedZipBuilder Builder() => new(
        null!,
        new FileService(),
        Options.Create(new Tooling.WebApi.Models.AppSettings { DatabaseSeeding = new DatabaseSeedSettings { SolutionAssetsPath = _assets, Elberfelder1871TextFile = "base.txt" } }),
        NullLogger<SeedZipBuilder>.Instance);

    [Fact]
    public void Mappings_NeedNoBaseText()
    {
        Builder().CheckAvailable(SeedExportKind.Mappings).ShouldBeNull();
    }

    [Theory]
    [InlineData(SeedExportKind.All)]
    [InlineData(SeedExportKind.Morphology)]
    public void BaseTextKinds_AreRefused_WhenTheBaseTextIsMissing(SeedExportKind kind)
    {
        Builder().CheckAvailable(kind).ShouldNotBeNull();
    }

    [Fact]
    public void BaseTextKinds_AreAvailable_WhenTheBaseTextExists()
    {
        Directory.CreateDirectory(_assets);
        File.WriteAllText(Path.Combine(_assets, "base.txt"), "x");

        Builder().CheckAvailable(SeedExportKind.All).ShouldBeNull();
    }
}
