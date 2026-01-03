using System.Globalization;
using System.Text;
using CsvHelper.Configuration;
using UnshackledWord.Application.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.Elb1871WordsSrGntWordsMapper;

public class MappingFileReader
{
    private readonly IFileService _fileService;

    public MappingFileReader(IFileService fileService)
    {
        _fileService = fileService;
    }

    public async Task<List<ElbSrMap>> GetMappingsAsync(CancellationToken token = default)
    {
        var path = @"../../assets/Elb1871WordsSrWordsMapping.csv";
        var list = new List<ElbSrMap>();

        if (_fileService.FileExists(path))
        {
            var settings = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                Delimiter = "\t",
                Encoding = Encoding.UTF8
            };

            using var textReader = new StreamReader(path, Encoding.UTF8);
            using var csvReader = new CsvHelper.CsvReader(textReader, settings);

            await foreach (var row in csvReader.GetRecordsAsync<ElbSrMap>(token))
            {
                row.Elb1871WordList = row.Elb1871Words.Split('|', StringSplitOptions.RemoveEmptyEntries);

                list.Add(row);
            }
        }

        return list;
    }
}
