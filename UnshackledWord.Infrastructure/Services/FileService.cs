using System.Text;
using UnshackledWord.Application.Abstractions;

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

    public async Task<string> ReadAllTextAsync(string path, CancellationToken token = default)
    {
        return await File.ReadAllTextAsync(path, token);
    }

    public async Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken token = default)
    {
        await File.WriteAllBytesAsync(path, bytes, token);
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

    public string GetFileName(string path)
    {
        return Path.GetFileName(path);
    }
}
