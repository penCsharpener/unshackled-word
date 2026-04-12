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

        var allGreekStrongsWords = new List<StepStrongsToTextDbo>();
        var allHebrewStrongsWords = new List<StepStrongsToTextDbo>();

        var greekWords = totalGreekEntries.ToDbo().SortByBibleOrder().EnumerateWithIds().ToList();
        foreach (var gkWord in greekWords)
        {
            foreach (var strongs in gkWord.StrongsNumbers)
            {
                strongs.StepGreekWordId = gkWord.Id;
                allGreekStrongsWords.Add(strongs);
            }
        }

        await _stepGreekWordsRepository.BulkInsertAsync(greekWords, token);

        await _stepStrongsNumbersRepository.BulkInsertInternalNewAsync(allGreekStrongsWords.EnumerateWithIds().ToArray(), token);

        var hebrewWords = totalHebrewEntries.ToDbo().SortByBibleOrder().EnumerateWithIds().ToList();
        foreach (var hebrewWord in hebrewWords)
        {
            foreach (var strongs in hebrewWord.StrongsNumbers)
            {
                strongs.StepHebrewWordId = hebrewWord.Id;
                allHebrewStrongsWords.Add(strongs);
            }
        }

        await _stepHebrewWordsRepository.BulkInsertAsync(hebrewWords, token);

        await _stepStrongsNumbersRepository.BulkInsertInternalNewAsync(allHebrewStrongsWords.EnumerateWithIds().ToList(), token);
    }
}
