namespace UnshackledWord.Seeding.Abstractions;

public interface IRunner
{
    Task Run(CancellationToken token = default);
}
