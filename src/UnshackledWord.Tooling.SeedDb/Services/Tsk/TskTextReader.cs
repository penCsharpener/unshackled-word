using System.Globalization;
using System.Reflection;
using System.Text;
using CsvHelper.Configuration;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Tooling.SeedDb.Services.Tsk.Models;

namespace UnshackledWord.Tooling.SeedDb.Services.Tsk;

public class TskTextReader
{
    private readonly IFileService _fileService;
    private readonly AppSettings _appSettings;

    public TskTextReader(IFileService fileService, IOptions<AppSettings> appSettings)
    {
        _fileService = fileService;
        _appSettings = appSettings.Value;
    }

    public async Task<ICollection<TskRow>> ReadAsync(CancellationToken token = default)
    {
        var settings = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            Delimiter = "\t",
            Encoding = Encoding.UTF8
        };

        using var textReader = new StreamReader(_appSettings.DatabaseSeeding.TskFilePath);
        using var csvReader = new CsvHelper.CsvReader(textReader, settings);
        var list = new List<TskRow>();

        await foreach (var row in csvReader.GetRecordsAsync<TskRow>(token))
        {


            list.Add(row);
        }

        return list;
    }
}
