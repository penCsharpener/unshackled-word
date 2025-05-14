using System.Text;
using System.Text.Unicode;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Extensions;

namespace UnshackledWord.Infrastructure.Services;

public class FileService : IFileService
{
    public bool PathExists(string path)
    {
        return Path.Exists(path);
    }

    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    public void CreateDirectoryIfNotExists(string path)
    {
        if (!PathExists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    public bool DirectoryExists(string path)
    {
        return Directory.Exists(path);
    }

    public async Task<string> ReadAllTextAsync(string path, Encoding encoding, CancellationToken token = default)
    {
        return await File.ReadAllTextAsync(path, token);
    }

    public Task WriteAllTextAsync(string path, string content, Encoding encoding, CancellationToken token = default)
    {
        return File.WriteAllTextAsync(path, content, encoding, token);
    }

    public async Task<string[]> ReadAllLinesAsync(string path, Encoding encoding, CancellationToken token = default)
    {
        return await File.ReadAllLinesAsync(path, token);
    }

    public async Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken token = default)
    {
        await File.WriteAllBytesAsync(path, bytes, token);
    }

    public void Copy(string sourcePath, string destinationPath, bool overwrite = false)
    {
        File.Copy(sourcePath, destinationPath, overwrite);
    }

    public void DeleteFolderRecursively(string path)
    {
        if (PathExists(path))
        {
            Directory.Delete(path, true);
        }
    }

    public string Combine(params string[] paths)
    {
        return Path.Combine(paths);
    }

    public string UrlCombine(params string[] paths)
    {
        return paths
            .Select(x => x.Replace('\\', '/').Trim('/').Trim('\\'))
            .JoinStrings("/");
    }

    public string GetFileName(string path)
    {
        return Path.GetFileName(path);
    }

    public string[] SearchFiles(string destinationPath, string searchPattern, SearchOption allDirectories)
    {
        if (!PathExists(destinationPath))
        {
            return [];
        }

        var files = Directory.GetFiles(destinationPath, searchPattern, allDirectories);
        return files;
    }
}
