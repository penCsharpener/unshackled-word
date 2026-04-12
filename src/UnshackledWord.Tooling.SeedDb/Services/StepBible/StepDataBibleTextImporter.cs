using UnshackledWord.Application.Abstractions.Step;
using UnshackledWord.Domain.Models.Dbo.Step;
using UnshackledWord.Domain.Models.Extensions;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.StepBible.Models;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public sealed class StepDataBibleTextImporter : IRunner
{
    private readonly StepGithubDownloader _githubDownloader;
    private readonly StepGreekFileStrategy _greekFileStrategy;
    private readonly StepHebrewFileStrategy _hebrewFileStrategy;
    private readonly IStepGreekWordsRepository _stepGreekWordsRepository;
    private readonly IStepHebrewWordsRepository _stepHebrewWordsRepository;

    public StepDataBibleTextImporter(StepGithubDownloader githubDownloader,
        StepGreekFileStrategy greekFileStrategy,
        StepHebrewFileStrategy hebrewFileStrategy,
        IStepGreekWordsRepository stepGreekWordsRepository,
        IStepHebrewWordsRepository stepHebrewWordsRepository)
    {
        _githubDownloader = githubDownloader;
        _greekFileStrategy = greekFileStrategy;
        _hebrewFileStrategy = hebrewFileStrategy;
        _stepGreekWordsRepository = stepGreekWordsRepository;
        _stepHebrewWordsRepository = stepHebrewWordsRepository;
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
        await _stepGreekWordsRepository.BulkInsertAsync(greekWords, token);

        var hebrewWords = totalHebrewEntries.ToDbo().SortByBibleOrder().EnumerateWithIds().ToList();
        await _stepHebrewWordsRepository.BulkInsertAsync(hebrewWords, token);
    }
}
