using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public sealed class StepRunner : IRunner
{
    private readonly StepGithubDownloader _githubDownloader;
    private readonly StepGreekFileStrategy _greekFileStrategy;
    private readonly StepHebrewFileStrategy _hebrewFileStrategy;

    public StepRunner(StepGithubDownloader githubDownloader, StepGreekFileStrategy greekFileStrategy, StepHebrewFileStrategy hebrewFileStrategy)
    {
        _githubDownloader = githubDownloader;
        _greekFileStrategy = greekFileStrategy;
        _hebrewFileStrategy = hebrewFileStrategy;
    }

    public async Task Run(CancellationToken token = default)
    {
        var files = await _githubDownloader.DownloadFileAsync(token);
        var totalGreekEntries = new List<StepAmalgamatedGreekEntry>();
        var totalHebrewEntries = new List<StepAmalgamatedHebrewEntry>();

        // foreach (var file in files.Where(f => f.Contains("Amalgamated Hebrew")))
        // {
        //     var parsedEntries = await _hebrewFileStrategy.SaveToDatabase(file, token);
        //     totalHebrewEntries.AddRange(parsedEntries);
        // }

        foreach (var file in files.Where(f => f.Contains("Amalgamated Greek")))
        {
            var parsedEntries = await _greekFileStrategy.SaveToDatabase(file, token);
            totalGreekEntries.AddRange(parsedEntries);
        }
    }
}
