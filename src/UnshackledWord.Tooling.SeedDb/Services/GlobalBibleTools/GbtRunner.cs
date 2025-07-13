using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.GlobalBibleTools;

public class GbtRunner : IRunner
{
    private readonly GbtCsvStrategy _gbtCsvStrategy;

    public GbtRunner(GbtCsvStrategy gbtCsvStrategy)
    {
        _gbtCsvStrategy = gbtCsvStrategy;
    }

    public async Task Run(CancellationToken token = default)
    {
        await _gbtCsvStrategy.SaveToDatabase(null, token);
    }
}
