using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.CsvImports;

public partial class CsvRunner : IRunner
{
    public partial Task Run(CancellationToken token = default);
}
