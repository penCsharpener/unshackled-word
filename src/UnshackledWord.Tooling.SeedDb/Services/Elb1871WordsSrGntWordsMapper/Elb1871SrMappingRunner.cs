using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.Elb1871WordsSrGntWordsMapper;

public class Elb1871SrMappingRunner : IRunner
{
    private readonly Elb1871SrGntStrategy _strategy;

    public Elb1871SrMappingRunner(Elb1871SrGntStrategy strategy)
    {
        _strategy = strategy;
    }

    public async Task Run(CancellationToken token = default)
    {
        await _strategy.SaveToDatabase(null, token);
    }
}
