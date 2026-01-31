using UnshackledWord.Application.Abstractions.Step;
using UnshackledWord.Domain.Models.Dbo.Step;
using UnshackledWord.Domain.Models.Extensions;
using UnshackledWord.Tooling.SeedDb.Services.StepBible.LexiconParser;
using UnshackledWord.Tooling.SeedDb.Services.StepBible.Models;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public sealed class StepDataDbImporter
{
    private readonly StepGithubDownloader _githubDownloader;
    private readonly StepGreekFileStrategy _greekFileStrategy;
    private readonly StepHebrewFileStrategy _hebrewFileStrategy;
    private readonly StepHebrewStrongsStrategy _hebrewStrongsStrategy;
    private readonly StepHebrewMorphologyStrategy _hebrewMorphologyStrategy;
    private readonly StepGreekStrongsStrategy _greekStrongsStrategy;
    private readonly StepGreekMorphologyStrategy _greekMorphologyStrategy;
    private readonly IStepGreekWordsRepository _stepGreekWordsRepository;
    private readonly IStepHebrewWordsRepository _stepHebrewWordsRepository;
    private readonly IStepStrongsRepository _stepStrongsRepository;
    private readonly IStepHebrewMorphologyRepository _stepHebrewMorphologyRepository;
    private readonly IStepGreekMorphologyRepository _stepGreekMorphologyRepository;
    private readonly IStepPersonPlaceRepository _stepPersonPlaceRepository;
    private readonly StepStrongsNormalizingStrategy _stepStrongsNormalizingStrategy;
    private readonly StepBibleStructureStrategy _stepBibleStructureStrategy;
    private readonly StepPersonPlaceLexiconStrategy _lexiconStrategy;

    public StepDataDbImporter(StepGithubDownloader githubDownloader,
        StepGreekFileStrategy greekFileStrategy,
        StepHebrewFileStrategy hebrewFileStrategy,
        StepHebrewStrongsStrategy hebrewStrongsStrategy,
        StepHebrewMorphologyStrategy hebrewMorphologyStrategy,
        StepGreekStrongsStrategy greekStrongsStrategy,
        StepGreekMorphologyStrategy greekMorphologyStrategy,
        IStepGreekWordsRepository stepGreekWordsRepository,
        IStepHebrewWordsRepository stepHebrewWordsRepository,
        IStepStrongsRepository stepStrongsRepository,
        IStepHebrewMorphologyRepository stepHebrewMorphologyRepository,
        IStepGreekMorphologyRepository stepGreekMorphologyRepository,
        IStepPersonPlaceRepository stepPersonPlaceRepository,
        StepStrongsNormalizingStrategy stepStrongsNormalizingStrategy,
        StepBibleStructureStrategy stepBibleStructureStrategy,
        StepPersonPlaceLexiconStrategy lexiconStrategy)
    {
        _githubDownloader = githubDownloader;
        _greekFileStrategy = greekFileStrategy;
        _hebrewFileStrategy = hebrewFileStrategy;
        _hebrewStrongsStrategy = hebrewStrongsStrategy;
        _hebrewMorphologyStrategy = hebrewMorphologyStrategy;
        _greekStrongsStrategy = greekStrongsStrategy;
        _greekMorphologyStrategy = greekMorphologyStrategy;
        _stepGreekWordsRepository = stepGreekWordsRepository;
        _stepHebrewWordsRepository = stepHebrewWordsRepository;
        _stepStrongsRepository = stepStrongsRepository;
        _stepHebrewMorphologyRepository = stepHebrewMorphologyRepository;
        _stepGreekMorphologyRepository = stepGreekMorphologyRepository;
        _stepStrongsNormalizingStrategy = stepStrongsNormalizingStrategy;
        _stepBibleStructureStrategy = stepBibleStructureStrategy;
        _lexiconStrategy = lexiconStrategy;
        _stepPersonPlaceRepository = stepPersonPlaceRepository;
    }

        public async Task Run(CancellationToken token = default)
    {
        var files = await _githubDownloader.DownloadFileAsync(token);
        var totalGreekEntries = new List<StepAmalgamatedGreekEntry>();
        var totalHebrewEntries = new List<StepAmalgamatedHebrewEntry>();
        var totalStrongs = new List<StepStrongsDbo>();
        var totalGreekMorphology = new List<StepGreekMorphologyEntry>();
        var totalHebrewMorphology = new List<StepHebrewMorphologyEntry>();

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
                continue;
            }

            if (file.Contains("Extended Strongs for Greek"))
            {
                var entries = await _greekStrongsStrategy.SaveToDatabase(file, token);
                totalStrongs.AddRange(entries.ToDbo());
                continue;
            }

            if (file.Contains("Extended Strongs for Hebrew"))
            {
                var entries = await _hebrewStrongsStrategy.SaveToDatabase(file, token);
                totalStrongs.AddRange(entries.ToDbo());
                continue;
            }

            if (file.Contains("Greek Morphhology Codes"))
            {
                var entries = await _greekMorphologyStrategy.SaveToDatabase(file, token);
                totalGreekMorphology.AddRange(entries);
                continue;
            }

            if (file.Contains("Hebrew Morphology Codes"))
            {
                var entries = await _hebrewMorphologyStrategy.SaveToDatabase(file, token);
                totalHebrewMorphology.AddRange(entries);
                continue;
            }

            if (file.Contains("Individualised Proper Names"))
            {
                await _lexiconStrategy.SaveToDatabase(file, token);
                continue;
            }
        }

        foreach (var chunk in totalGreekEntries.ToDbo().SortByBibleOrder().EnumerateWithIds().Chunk(10000))
        {
            await _stepGreekWordsRepository.BulkInsertAsync(chunk, token);
        }

        foreach (var chunk in totalHebrewEntries.ToDbo().SortByBibleOrder().EnumerateWithIds().Chunk(10000))
        {
            await _stepHebrewWordsRepository.BulkInsertAsync(chunk, token);
        }

        foreach (var chunk in totalStrongs.EnumerateWithIds().Chunk(5000))
        {
            await _stepStrongsRepository.BulkInsertAsync(chunk, token);
        }

        foreach (var chunk in totalHebrewMorphology.ToDbo().EnumerateWithIds().Chunk(5000))
        {
            await _stepHebrewMorphologyRepository.BulkInsertAsync(chunk, token);
        }

        foreach (var chunk in totalGreekMorphology.ToDbo().EnumerateWithIds().Chunk(5000))
        {
            await _stepGreekMorphologyRepository.BulkInsertAsync(chunk, token);
        }

        await _stepStrongsNormalizingStrategy.SaveToDatabase(null!, token);
        await _stepBibleStructureStrategy.SaveToDatabase(null!, token);
    }
}
