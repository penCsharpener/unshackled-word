using UnshackledWord.Application.Abstractions.Step;
using UnshackledWord.Domain.Models.Dbo.Step;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

/// <summary>
/// Roadmap 0022.1b: adds the disambiguated Hebrew lexicon rows (<c>H5921A</c>, <c>H0834A</c>, ...) that older imports skipped
/// (the eStrong column of those TBESH lines ends in a lower-case letter, which the old line filter did not accept).
/// Additive and idempotent: existing rows and ids stay, only missing (language, number, extra) keys are inserted.
/// The STEP file is downloaded on the fly (cached in temp) and never redistributed.
/// </summary>
public sealed class StepLexiconRepairService
{
    private readonly StepGithubDownloader _downloader;
    private readonly StepHebrewStrongsStrategy _hebrewStrategy;
    private readonly IStepStrongsRepository _repository;

    public StepLexiconRepairService(StepGithubDownloader downloader, StepHebrewStrongsStrategy hebrewStrategy, IStepStrongsRepository repository)
    {
        _downloader = downloader;
        _hebrewStrategy = hebrewStrategy;
        _repository = repository;
    }

    public async Task<IReadOnlyList<string>> RepairAsync(CancellationToken token = default)
    {
        var files = await _downloader.DownloadFileAsync(token);
        var hebrewFile = files.FirstOrDefault(x => x.Contains("Extended Strongs for Hebrew"))
                         ?? throw new InvalidOperationException("The STEP Hebrew extended Strongs file (TBESH) was not found.");

        var entries = await _hebrewStrategy.ParseFileAsync(hebrewFile, token);

        return await _repository.InsertMissingAsync(entries.ToDbo().ToArray(), token);
    }
}
