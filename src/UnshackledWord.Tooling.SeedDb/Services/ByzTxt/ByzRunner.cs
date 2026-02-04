using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.ByzTxt;

public class ByzRunner : IRunner
{
    private readonly ByzTxtStrategy _byzTxtStrategy;
    private readonly ILogger<ByzRunner> _logger;

    public ByzRunner(ByzTxtStrategy byzTxtStrategy, ILogger<ByzRunner> logger)
    {
        _byzTxtStrategy = byzTxtStrategy;
        _logger = logger;
    }

    public async Task Run(CancellationToken token = default)
    {
        await _byzTxtStrategy.SaveToDatabase("", token);
    }
}
