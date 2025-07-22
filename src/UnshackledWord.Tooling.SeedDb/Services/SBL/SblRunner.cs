using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.SBL;

public class SblRunner : IRunner
{
    private readonly SblGntStrategy _textStrategy;
    private readonly ILogger<SblRunner> _logger;

    public SblRunner(SblGntStrategy textStrategy, ILogger<SblRunner> logger)
    {
        _textStrategy = textStrategy;
        _logger = logger;
    }

    public async Task Run(CancellationToken token = default)
    {
        await _textStrategy.SaveToDatabase("", token);
    }
}
