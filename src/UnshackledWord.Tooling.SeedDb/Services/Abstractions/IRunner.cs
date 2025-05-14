namespace UnshackledWord.Tooling.SeedDb.Services.Abstractions;

public interface IRunner
{
    Task Run(CancellationToken token = default);
}
