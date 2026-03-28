using UnshackledWord.Application.Abstractions.Step;
using UnshackledWord.Domain.Models.Extensions;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.StepBible.Models;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public sealed class StepDataMorphologyImporter : IRunner
{
    private readonly StepGithubDownloader _githubDownloader;
    private readonly StepHebrewMorphologyStrategy _hebrewMorphologyStrategy;
    private readonly StepGreekMorphologyStrategy _greekMorphologyStrategy;
    private readonly IStepHebrewMorphologyRepository _stepHebrewMorphologyRepository;
    private readonly IStepGreekMorphologyRepository _stepGreekMorphologyRepository;

    public StepDataMorphologyImporter(StepGithubDownloader githubDownloader,
        StepHebrewMorphologyStrategy hebrewMorphologyStrategy,
        StepGreekMorphologyStrategy greekMorphologyStrategy,
        IStepHebrewMorphologyRepository stepHebrewMorphologyRepository,
        IStepGreekMorphologyRepository stepGreekMorphologyRepository)
    {
        _githubDownloader = githubDownloader;
        _hebrewMorphologyStrategy = hebrewMorphologyStrategy;
        _greekMorphologyStrategy = greekMorphologyStrategy;
        _stepHebrewMorphologyRepository = stepHebrewMorphologyRepository;
        _stepGreekMorphologyRepository = stepGreekMorphologyRepository;
    }

        public async Task Run(CancellationToken token = default)
    {
        var files = await _githubDownloader.DownloadFileAsync(token);
        var totalGreekMorphology = new List<StepGreekMorphologyEntry>();
        var totalHebrewMorphology = new List<StepHebrewMorphologyEntry>();

        foreach (var file in files)
        {
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
            }
        }

        foreach (var chunk in totalHebrewMorphology.ToDbo().EnumerateWithIds().Chunk(5000))
        {
            await _stepHebrewMorphologyRepository.BulkInsertAsync(chunk, token);
        }

        foreach (var chunk in totalGreekMorphology.ToDbo().EnumerateWithIds().Chunk(5000))
        {
            await _stepGreekMorphologyRepository.BulkInsertAsync(chunk, token);
        }
    }
}
