namespace UnshackledWord.Application.Abstractions;

public interface IFileDownloader
{
    Task<List<string>> DownloadFileAsync(CancellationToken token = default);
}
