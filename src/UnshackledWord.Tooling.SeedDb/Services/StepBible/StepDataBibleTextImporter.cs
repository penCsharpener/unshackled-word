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
    private readonly IStepStrongsNumbersRepository _stepStrongsNumbersRepository;
    private readonly IStepGreekWordsRepository _stepGreekWordsRepository;
    private readonly IStepHebrewWordsRepository _stepHebrewWordsRepository;

    public StepDataBibleTextImporter(StepGithubDownloader githubDownloader,
        StepGreekFileStrategy greekFileStrategy,
        StepHebrewFileStrategy hebrewFileStrategy,
        IStepStrongsNumbersRepository stepStrongsNumbersRepository,
        IStepGreekWordsRepository stepGreekWordsRepository,
        IStepHebrewWordsRepository stepHebrewWordsRepository)
    {
        _githubDownloader = githubDownloader;
        _greekFileStrategy = greekFileStrategy;
        _hebrewFileStrategy = hebrewFileStrategy;
        _stepStrongsNumbersRepository = stepStrongsNumbersRepository;
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

        var allStrongsWords = new List<StepStrongsToTextDbo>();

        foreach (var chunk in totalGreekEntries.ToDbo().SortByBibleOrder().EnumerateWithIds().Chunk(10000))
        {
            allStrongsWords.AddRange(chunk.SelectMany(x =>
            {
                foreach (var str in x.StrongsNumbers)
                {
                    str.StepGreekWordId = x.Id;
                }
                return x.StrongsNumbers;
            }));
            await _stepGreekWordsRepository.BulkInsertAsync(chunk, token);
        }

        foreach (var chunk in totalHebrewEntries.ToDbo().SortByBibleOrder().EnumerateWithIds().Chunk(10000))
        {
            allStrongsWords.AddRange(chunk.SelectMany(x =>
            {
                foreach (var str in x.StrongsNumbers)
                {
                    str.StepHebrewWordId = x.Id;
                }
                return x.StrongsNumbers;
            }));
            await _stepHebrewWordsRepository.BulkInsertAsync(chunk, token);
        }

        allStrongsWords.AddRange(totalHebrewEntries.SelectMany(x => x.StrongsNumbers));

        foreach (var chunk in allStrongsWords.EnumerateWithIds().Chunk(10000))
        {
            await _stepStrongsNumbersRepository.BulkInsertInternalNewAsync(chunk, token);
        }
    }
}
