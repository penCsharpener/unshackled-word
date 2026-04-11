using System.Globalization;
using System.Text;
using CsvHelper.Configuration;
using Microsoft.Extensions.Options;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Tooling.SeedDb.Services.Tsk.Extensions;
using UnshackledWord.Tooling.SeedDb.Services.Tsk.Models;

namespace UnshackledWord.Tooling.SeedDb.Services.Tsk;

public class TskTextReader
{
    private readonly AppSettings _appSettings;

    public TskTextReader(IOptions<AppSettings> appSettings)
    {
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
