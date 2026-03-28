using UnshackledWord.Tooling.SeedDb.Services.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.StepBible.LexiconParser;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public sealed class StepDataLexiconImporter : IRunner
{
    private readonly StepGithubDownloader _githubDownloader;
    private readonly StepPersonPlaceLexiconStrategy _lexiconStrategy;

    public StepDataLexiconImporter(StepGithubDownloader githubDownloader,
        StepPersonPlaceLexiconStrategy lexiconStrategy)
    {
        _githubDownloader = githubDownloader;
        _lexiconStrategy = lexiconStrategy;
    }

        public async Task Run(CancellationToken token = default)
    {
        var files = await _githubDownloader.DownloadFileAsync(token);

        foreach (var file in files.Where(x => x.Contains("Individualised Proper Names")))
        {
            await _lexiconStrategy.SaveToDatabase(file, token);
        }
    }
}
