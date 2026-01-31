using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Settings;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public sealed class StepGithubDownloader : IFileDownloader
{
    private readonly IFileService _fileService;
    private readonly AppSettings _options;
    private readonly HttpClient _httpclient;

    public StepGithubDownloader(HttpClient httpclient, IFileService fileService, IOptions<AppSettings> options)
    {
        _options = options.Value;
        _httpclient = httpclient;
        _fileService = fileService;
    }

    public async Task<List<string>> DownloadFileAsync(CancellationToken token = default)
    {
        EnsurePath();
        var fileList = new List<string>();
        var stepOptions = _options.DatabaseSeeding.StepBibleData;
        var dictionary = new Dictionary<string, string>();

        foreach (var fileSubPath in stepOptions.AmalgamatedFiles)
        {
            var fileUrl = $"{stepOptions.GithubRepoUrl}{stepOptions.AmalgamatedSubPath}{fileSubPath}";
            var fileName = _fileService.GetFileName(fileUrl).Replace("%20", " ");
            var savePath = _fileService.Combine(_options.DatabaseSeeding.FolderLocation, "Step", fileName);
            dictionary[fileUrl] = savePath;
        }

        foreach (var fileSubPath in stepOptions.StrongsFiles)
        {
            var fileUrl = $"{stepOptions.GithubRepoUrl}{stepOptions.StrongsLexiconSubPath}{fileSubPath}";
            var fileName = _fileService.GetFileName(fileUrl).Replace("%20", " ");
            var savePath = _fileService.Combine(_options.DatabaseSeeding.FolderLocation, "Step", fileName);
            dictionary[fileUrl] = savePath;
        }

        foreach (var fileSubPath in stepOptions.MorphologyFiles)
        {
            var fileUrl = $"{stepOptions.GithubRepoUrl}{fileSubPath}";
            var fileName = _fileService.GetFileName(fileUrl).Replace("%20", " ");
            var savePath = _fileService.Combine(_options.DatabaseSeeding.FolderLocation, "Step", fileName);
            dictionary[fileUrl] = savePath;
        }
        //,Rom,1Co,2Co,Gal,Eph,Php,Col,1Th,2Th,1Ti,2Ti,Tit,Phm,Heb,Jas,1Pe,2Pe,1Jn,2Jn,3Jn,Jud,Rev

        var personPlaceFileUrl = $"{stepOptions.GithubRepoUrl}{stepOptions.PersonPlaceFile}";
        var personPlaceFileName = _fileService.GetFileName(personPlaceFileUrl).Replace("%20", " ");
        var personPlaceSavePath = _fileService.Combine(_options.DatabaseSeeding.FolderLocation, "Step", personPlaceFileName);
        dictionary[personPlaceFileUrl] = personPlaceSavePath;

        var versificationFileUrl = $"{stepOptions.GithubRepoUrl}{stepOptions.VersificationFile}";
        var versificationFileName = _fileService.GetFileName(versificationFileUrl).Replace("%20", " ");
        var versificationSavePath = _fileService.Combine(_options.DatabaseSeeding.FolderLocation, "Step", versificationFileName);
        dictionary[versificationFileUrl] = versificationSavePath;

        foreach (var (fileUrl, savePath) in dictionary)
        {
            if (_fileService.FileExists(savePath))
            {
                fileList.Add(savePath);
                continue;
            }

            var response = await _httpclient.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead, token);
            if (response.IsSuccessStatusCode is false)
            {
                continue;
            }

            await using var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await using var httpStream = await response.Content.ReadAsStreamAsync(token);
            await httpStream.CopyToAsync(fileStream, token);
            fileList.Add(savePath);
        }

        return fileList;
    }

    public void EnsurePath()
    {
        _fileService.CreateDirectoryIfNotExists(_options.DatabaseSeeding.FolderLocation);
        var directoryPath = _fileService.Combine(_options.DatabaseSeeding.FolderLocation, "Step");
        _fileService.CreateDirectoryIfNotExists(directoryPath);
    }
}
