using System.Text;

namespace UnshackledWord.Application.Abstractions;

public interface IFileService
{
    bool PathExists(string path);
    bool FileExists(string path);
    void CreateDirectoryIfNotExists(string path);
    bool DirectoryExists(string path);
    Task<string> ReadAllTextAsync(string path, Encoding encoding, CancellationToken token = default);
    Task WriteAllTextAsync(string path, string content, Encoding encoding, CancellationToken token = default);
    Task<string[]> ReadAllLinesAsync(string path, Encoding encoding, CancellationToken token = default);
    Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken token = default);
    void DeleteFolderRecursively(string path);
    string Combine(params string[] paths);
    string UrlCombine(params string[] paths);
    string GetFileName(string path);
    string[] SearchFiles(string destinationPath, string searchPattern, SearchOption allDirectories);
    void Copy(string sourcePath, string destinationPath, bool overwrite = false);
}
