namespace UnshackledWord.Application.Abstractions;

public interface IFileDownloader
{
    Task DownloadFileAsync(CancellationToken token = default);
}
