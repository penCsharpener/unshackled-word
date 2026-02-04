using System.Net;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.Elb1871Lemmatizer;

public sealed class LemmatizerRunner : IRunner
{
    private readonly LemmatizerStrategy _lemmatizerStrategy;
    private readonly LemmatizerDownloader _downloader;
    private readonly LemmatizerRepository _repo;

    public LemmatizerRunner(LemmatizerStrategy lemmatizerStrategy,
        LemmatizerDownloader downloader,
        LemmatizerRepository repo)
    {
        _lemmatizerStrategy = lemmatizerStrategy;
        _downloader = downloader;
        _repo = repo;
    }

    public async Task Run(CancellationToken token = default)
    {
        var count = await _repo.GetCountAsync(token);
        if (count > 0)
        {
            return;
        }

        await _downloader.DownloadFileAsync(token);
        await _lemmatizerStrategy.SaveToDatabase(null!, token);
    }
}
