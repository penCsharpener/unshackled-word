using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.SBL;

public class SblRunner : IRunner
{
    private readonly SblTextDownloader _textDownloader;
    private readonly SblGntTextStrategy _textTextStrategy;
    private readonly SblApparatusDownloader _apparatusDownloader;
    private readonly SblGntApparatusStrategy _apparatusStrategy;
    private readonly ILogger<SblRunner> _logger;

    public SblRunner(SblTextDownloader textDownloader,
        SblGntTextStrategy textTextStrategy,
        SblApparatusDownloader apparatusDownloader,
        SblGntApparatusStrategy apparatusStrategy,
        ILogger<SblRunner> logger)
    {
        _textDownloader = textDownloader;
        _textTextStrategy = textTextStrategy;
        _apparatusDownloader = apparatusDownloader;
        _apparatusStrategy = apparatusStrategy;
        _logger = logger;
    }

    public async Task Run(CancellationToken token = default)
    {
        await _textDownloader.DownloadFileAsync(token);
        await _textTextStrategy.SaveToDatabase("", token);

        await _apparatusDownloader.DownloadFileAsync(token);
        await _apparatusStrategy.SaveToDatabase("", token);
    }
}
