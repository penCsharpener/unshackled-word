namespace UnshackledWord.Application.Abstractions;

public interface IUnzipService
{
    void UnzipArchive(string zipFilePath, string destinationFolder, bool overwrite = true,
        CancellationToken token = default);
}
