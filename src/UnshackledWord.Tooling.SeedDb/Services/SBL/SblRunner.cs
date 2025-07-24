using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.SBL;

public class SblRunner : IRunner
{
    private readonly SblGntTextStrategy _textTextStrategy;
    private readonly SblGntApparatusStrategy _apparatusStrategy;
    private readonly ILogger<SblRunner> _logger;

    public SblRunner(SblGntTextStrategy textTextStrategy, SblGntApparatusStrategy apparatusStrategy, ILogger<SblRunner> logger)
    {
        _textTextStrategy = textTextStrategy;
        _apparatusStrategy = apparatusStrategy;
        _logger = logger;
    }

    public async Task Run(CancellationToken token = default)
    {
        // await _textTextStrategy.SaveToDatabase("", token);
        await _apparatusStrategy.SaveToDatabase("", token);
    }
}
