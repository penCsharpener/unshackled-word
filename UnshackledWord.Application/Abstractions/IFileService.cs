namespace UnshackledWord.Application.Abstractions;

public interface IFileService
{
    bool PathExists(string path);
    bool FileExists(string path);
    void CreateDirectoryIfNotExists(string path);
    Task<string> ReadAllTextAsync(string path, CancellationToken token = default);
    Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken token = default);
    void DeleteFolderRecursively(string path);
    string Combine(params string[] paths);
    string GetFileName(string path);
}
