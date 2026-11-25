using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Abstractions.Step;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Tooling.SeedDb.Services.StepBible;

namespace UnshackledWord.Tooling.SeedDb.Tests.Services.StepBible;

/// <summary>Roadmap 0025 review: the word editions are rebuilt only after a Greek import or when they are missing, not on every SeedDb run.</summary>
public class StepDataBibleTextImporterTests
{
    private readonly IStepGreekWordsRepository _greek = Substitute.For<IStepGreekWordsRepository>();

    private StepDataBibleTextImporter NewImporter()
    {
        var fileService = Substitute.For<IFileService>();
        var reader = Substitute.For<IDbReader>();
        return new StepDataBibleTextImporter(
            new StepGithubDownloader(new HttpClient(), fileService, Options.Create(new AppSettings())),
            new StepGreekFileStrategy(reader, fileService, _greek, NullLogger<StepGreekFileStrategy>.Instance),
            new StepHebrewFileStrategy(reader, fileService, Substitute.For<IStepHebrewWordsRepository>(), NullLogger<StepHebrewFileStrategy>.Instance),
            _greek,
            Substitute.For<IStepHebrewWordsRepository>(),
            NullLogger<StepDataBibleTextImporter>.Instance);
    }

    [Fact]
    public async Task SkippedImport_WithEditions_DoesNotRefresh()
    {
        _greek.EditionsMissingAsync(Arg.Any<CancellationToken>()).Returns(false);

        await NewImporter().RefreshGreekEditionsIfNeededAsync(0, TestContext.Current.CancellationToken);

        await _greek.DidNotReceive().RefreshEditionsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InsertedWords_Refresh()
    {
        await NewImporter().RefreshGreekEditionsIfNeededAsync(142_095, TestContext.Current.CancellationToken);

        await _greek.Received(1).RefreshEditionsAsync(Arg.Any<CancellationToken>());
        await _greek.DidNotReceive().EditionsMissingAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SkippedImport_WithoutEditions_RefreshesToHeal()
    {
        _greek.EditionsMissingAsync(Arg.Any<CancellationToken>()).Returns(true);

        await NewImporter().RefreshGreekEditionsIfNeededAsync(0, TestContext.Current.CancellationToken);

        await _greek.Received(1).RefreshEditionsAsync(Arg.Any<CancellationToken>());
    }
}
