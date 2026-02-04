using UnshackledWord.Application.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.SBL;

public class SblRunner : IRunner
{
    private readonly SblTextDownloader _textDownloader;
    private readonly SblGntTextStrategy _textTextStrategy;
    private readonly SblApparatusDownloader _apparatusDownloader;
    private readonly SblGntApparatusStrategy _apparatusStrategy;
    private readonly IDbReader _dbReader;
    private readonly ILogger<SblRunner> _logger;

    public SblRunner(SblTextDownloader textDownloader,
        SblGntTextStrategy textTextStrategy,
        SblApparatusDownloader apparatusDownloader,
        SblGntApparatusStrategy apparatusStrategy,
        IDbReader dbReader,
        ILogger<SblRunner> logger)
    {
        _textDownloader = textDownloader;
        _textTextStrategy = textTextStrategy;
        _apparatusDownloader = apparatusDownloader;
        _apparatusStrategy = apparatusStrategy;
        _dbReader = dbReader;
        _logger = logger;
    }

    public async Task Run(CancellationToken token = default)
    {
        var countText = await GetCountTextAsync(token);
        var countApparatusText = await GetCountApparatusAsync(token);

        if (countText == 0)
        {
            await _textDownloader.DownloadFileAsync(token);
            await _textTextStrategy.SaveToDatabase("", token);
        }

        if (countApparatusText == 0)
        {
            await _apparatusDownloader.DownloadFileAsync(token);
            await _apparatusStrategy.SaveToDatabase("", token);
        }
    }

    private async Task<int> GetCountTextAsync(CancellationToken token = default)
    {
        var sql = """
                  select count(*)
                  from "unshackled-word"."SblText"
                  """;

        return await _dbReader.ExecuteScalarAsync<int>(sql);
    }

    private async Task<int> GetCountApparatusAsync(CancellationToken token = default)
    {
        var sql = """
                  select count(*)
                  from "unshackled-word"."SblApparatus"
                  """;

        return await _dbReader.ExecuteScalarAsync<int>(sql);
    }
}
