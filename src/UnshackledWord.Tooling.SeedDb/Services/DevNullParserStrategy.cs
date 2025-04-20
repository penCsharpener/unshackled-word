using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services;

public sealed class DevNullParserStrategy : IFileParserStrategy
{
    public Task SaveToDatabase(string filePath, CancellationToken token = default)
    {
        return Task.CompletedTask;
    }
}
