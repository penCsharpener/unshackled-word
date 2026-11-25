using UnshackledWord.Application.Abstractions.Step;
using UnshackledWord.Domain.Models.Dbo.Step;
using UnshackledWord.Domain.Models.Extensions;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;
using Microsoft.Extensions.Logging;
using UnshackledWord.Tooling.SeedDb.Services.StepBible.Models;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public sealed class StepDataBibleTextImporter : IRunner
{
    private readonly StepGithubDownloader _githubDownloader;
    private readonly StepGreekFileStrategy _greekFileStrategy;
    private readonly StepHebrewFileStrategy _hebrewFileStrategy;
    private readonly IStepGreekWordsRepository _stepGreekWordsRepository;
    private readonly IStepHebrewWordsRepository _stepHebrewWordsRepository;
    private readonly StepImportResult _importResult;
    private readonly ILogger<StepDataBibleTextImporter> _logger;

    public StepDataBibleTextImporter(StepGithubDownloader githubDownloader,
        StepGreekFileStrategy greekFileStrategy,
        StepHebrewFileStrategy hebrewFileStrategy,
        IStepGreekWordsRepository stepGreekWordsRepository,
        IStepHebrewWordsRepository stepHebrewWordsRepository,
        StepImportResult importResult,
        ILogger<StepDataBibleTextImporter> logger)
    {
        _githubDownloader = githubDownloader;
        _greekFileStrategy = greekFileStrategy;
        _hebrewFileStrategy = hebrewFileStrategy;
        _stepGreekWordsRepository = stepGreekWordsRepository;
        _stepHebrewWordsRepository = stepHebrewWordsRepository;
        _importResult = importResult;
        _logger = logger;
    }

    public async Task Run(CancellationToken token = default)
    {
        var files = await _githubDownloader.DownloadFileAsync(token);
        var totalGreekEntries = new List<StepAmalgamatedGreekEntry>();
        var totalHebrewEntries = new List<StepAmalgamatedHebrewEntry>();

        foreach (var file in files)
        {
            if (file.Contains("Amalgamated Greek"))
            {
                var entries = await _greekFileStrategy.SaveToDatabase(file, token);
                totalGreekEntries.AddRange(entries);
                continue;
            }

            if (file.Contains("Amalgamated Hebrew"))
            {
                var entries = await _hebrewFileStrategy.SaveToDatabase(file, token);
                totalHebrewEntries.AddRange(entries);
            }
        }

        var greekWords = totalGreekEntries.ToDbo().SortByBibleOrder().EnumerateWithIds().ToList();
        var insertedGreekWords = await _stepGreekWordsRepository.BulkInsertAsync(greekWords, token);
        _importResult.InsertedGreekWords = insertedGreekWords;
        await RefreshGreekEditionsIfNeededAsync(insertedGreekWords, token);

        var hebrewWords = totalHebrewEntries.ToDbo().SortByBibleOrder().EnumerateWithIds().ToList();
        // Read by VerseSimilarityRunner (roadmap 0027.2), which rebuilds after the Hebrew normalization only when words were inserted.
        _importResult.InsertedHebrewWords = await _stepHebrewWordsRepository.BulkInsertAsync(hebrewWords, token);
    }

    /// <summary>
    /// Rebuilds the word editions (roadmap 0025) only when this run inserted Greek words, or when there are words but no edition rows (self-healing
    /// after an interrupted run). A skipped STEP import leaves the ~1.1M rows alone: SeedDb runs on every start, and the refresh truncates and
    /// rebuilds (~20 s, table locked) what the live API reads.
    /// </summary>
    public async Task RefreshGreekEditionsIfNeededAsync(int insertedGreekWords, CancellationToken token = default)
    {
        if (insertedGreekWords == 0 && !await _stepGreekWordsRepository.EditionsMissingAsync(token))
        {
            _logger.LogInformation("Greek editions up to date, no refresh");
            return;
        }

        var editionRows = await _stepGreekWordsRepository.RefreshEditionsAsync(token);
        _logger.LogInformation("Greek editions refreshed: {count} rows", editionRows);
    }
}
