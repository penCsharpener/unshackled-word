using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public sealed class StepRunner : IRunner
{
    private readonly StepGithubDownloader _githubDownloader;
    private readonly StepFileStrategy _fileStrategy;

    public StepRunner(StepGithubDownloader githubDownloader, StepFileStrategy fileStrategy)
    {
        _githubDownloader = githubDownloader;
        _fileStrategy = fileStrategy;
    }

    public async Task Run(CancellationToken token = default)
    {
        var files = await _githubDownloader.DownloadFileAsync(token);

        foreach (var file in files)
        {
            await _fileStrategy.SaveToDatabase(file, token);
        }
    }
}
