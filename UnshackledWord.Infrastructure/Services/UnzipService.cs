using System.IO.Compression;
using UnshackledWord.Application.Abstractions;

namespace UnshackledWord.Infrastructure.Services;

public class UnzipService : IUnzipService
{
    private readonly IFileService _fileService;

    public UnzipService(IFileService fileService)
    {
        _fileService = fileService;
    }

    public void UnzipArchive(string zipFilePath, string destinationFolder, bool overwrite = true, CancellationToken token = default)
    {
        if (_fileService.FileExists(zipFilePath) is false)
        {
            throw new FileNotFoundException($"The file {zipFilePath} does not exist.");
        }

        _fileService.CreateDirectoryIfNotExists(destinationFolder);

        using var zipArchive = new ZipArchive(File.OpenRead(zipFilePath), ZipArchiveMode.Read);
        foreach (var entry in zipArchive.Entries)
        {
            var destinationPath = _fileService.Combine(destinationFolder, entry.FullName);
            if (entry.Name == "")
            {
                // This is a directory entry
                _fileService.CreateDirectoryIfNotExists(destinationPath);
            }
            else
            {
                // This is a file entry
                entry.ExtractToFile(destinationPath, overwrite: true);
            }
        }
    }
}
