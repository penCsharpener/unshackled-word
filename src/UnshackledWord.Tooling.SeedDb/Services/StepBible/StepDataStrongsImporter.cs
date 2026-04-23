using UnshackledWord.Application.Abstractions.Step;
using UnshackledWord.Domain.Models.Dbo.Step;
using UnshackledWord.Domain.Models.Extensions;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public sealed class StepDataStrongsImporter : IRunner
{
    private readonly StepGithubDownloader _githubDownloader;
    private readonly StepHebrewStrongsStrategy _hebrewStrongsStrategy;
    private readonly StepGreekStrongsStrategy _greekStrongsStrategy;
    private readonly IStepStrongsRepository _stepStrongsRepository;

    public StepDataStrongsImporter(StepGithubDownloader githubDownloader,
        StepHebrewStrongsStrategy hebrewStrongsStrategy,
        StepGreekStrongsStrategy greekStrongsStrategy,
        IStepStrongsRepository stepStrongsRepository)
    {
        _githubDownloader = githubDownloader;
        _hebrewStrongsStrategy = hebrewStrongsStrategy;
        _greekStrongsStrategy = greekStrongsStrategy;
        _stepStrongsRepository = stepStrongsRepository;
    }

        public async Task Run(CancellationToken token = default)
    {
        var files = await _githubDownloader.DownloadFileAsync(token);
        var totalStrongs = new List<StepStrongsLexiconDbo>();

        foreach (var file in files.Where(x => x.Contains("Extended Strongs for")))
        {
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
            }
        }

        await _stepStrongsRepository.BulkInsertAsync(totalStrongs.EnumerateWithIds().ToArray(), token);
    }
}
