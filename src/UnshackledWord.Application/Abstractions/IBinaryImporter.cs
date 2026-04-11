namespace UnshackledWord.Application.Abstractions;

public interface IBinaryImporter
{
    Task StartRowAsync(CancellationToken token = default);
    Task CompleteAsync(CancellationToken token = default);
    Task WriteAsync<T>(T value, CancellationToken token = default);
}
