using System.Globalization;
using System.Text;
using CsvHelper.Configuration;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Tooling.SeedDb.Services.Tsk.Extensions;
using UnshackledWord.Tooling.SeedDb.Services.Tsk.Models;

namespace UnshackledWord.Tooling.SeedDb.Services.Tsk;

public class TskTextReader
{
    private readonly IFileService _fileService;
    private readonly ILogger<TskTextReader> _logger;
    private readonly AppSettings _appSettings;

    public TskTextReader(IFileService fileService, IOptions<AppSettings> appSettings, ILogger<TskTextReader> logger)
    {
        _fileService = fileService;
        _logger = logger;
        _appSettings = appSettings.Value;
    }

    public async Task<ICollection<TskReference>> ReadAsync(CancellationToken token = default)
    {
        var settings = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            Delimiter = "\t",
            Encoding = Encoding.UTF8
        };

        var tskPath = _fileService.Combine(_appSettings.DatabaseSeeding.SolutionAssetsPath, _appSettings.DatabaseSeeding.TskFilePath);

        if (!_fileService.FileExists(tskPath))
        {
            _logger.LogWarning("TSK file not found at path: '{TskPath}'. No TSK references will be seeded.", tskPath);
            return [];
        }

        using var textReader = new StreamReader(_appSettings.DatabaseSeeding.TskFilePath);
        using var csvReader = new CsvHelper.CsvReader(textReader, settings);
        var list = new List<TskReference>();

        await foreach (var row in csvReader.GetRecordsAsync<TskRow>(token))
        {
            list.Add(row.ToTskReference());
        }

        return list;
    }
}
