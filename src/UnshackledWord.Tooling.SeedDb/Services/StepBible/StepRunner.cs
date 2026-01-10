using UnshackledWord.Tooling.SeedDb.Services.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.StepBible.Models;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public sealed class StepRunner : IRunner
{
    private readonly StepGithubDownloader _githubDownloader;
    private readonly StepGreekFileStrategy _greekFileStrategy;
    private readonly StepHebrewFileStrategy _hebrewFileStrategy;
    private readonly StepHebrewStrongsStrategy _hebrewStrongsStrategy;
    private readonly StepHebrewMorphologyStrategy _hebrewMorphologyStrategy;
    private readonly StepGreekStrongsStrategy _greekStrongsStrategy;
    private readonly StepGreekMorphologyStrategy _greekMorphologyStrategy;

    public StepRunner(StepGithubDownloader githubDownloader,
        StepGreekFileStrategy greekFileStrategy,
        StepHebrewFileStrategy hebrewFileStrategy,
        StepHebrewStrongsStrategy hebrewStrongsStrategy,
        StepHebrewMorphologyStrategy hebrewMorphologyStrategy,
        StepGreekStrongsStrategy greekStrongsStrategy,
        StepGreekMorphologyStrategy greekMorphologyStrategy)
    {
        _githubDownloader = githubDownloader;
        _greekFileStrategy = greekFileStrategy;
        _hebrewFileStrategy = hebrewFileStrategy;
        _hebrewStrongsStrategy = hebrewStrongsStrategy;
        _hebrewMorphologyStrategy = hebrewMorphologyStrategy;
        _greekStrongsStrategy = greekStrongsStrategy;
        _greekMorphologyStrategy = greekMorphologyStrategy;
    }

    public async Task Run(CancellationToken token = default)
    {
        var files = await _githubDownloader.DownloadFileAsync(token);
        var totalGreekEntries = new List<StepAmalgamatedGreekEntry>();
        var totalHebrewEntries = new List<StepAmalgamatedHebrewEntry>();
        var totalGreekStrongs = new List<StepGreekStrongsEntry>();
        var totalHebrewStrongs = new List<StepHebrewStrongsEntry>();
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
                totalGreekStrongs.AddRange(entries);
                continue;
            }

            if (file.Contains("Extended Strongs for Hebrew"))
            {
                var entries = await _hebrewStrongsStrategy.SaveToDatabase(file, token);
                totalHebrewStrongs.AddRange(entries);
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
        }
    }
}
