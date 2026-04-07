using System.Globalization;
using CsvHelper;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Backup;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.Models.Settings;

namespace UnshackledWord.Tooling.SeedDb.Services.AiMappingImport;

public sealed class AiMappingImportRunnerRepository
{
    private readonly IDbReader _dbReader;
    private readonly IDbWriter _dbWriter;
    private readonly IFileService _fileService;
    private readonly DatabaseSeedSettings _options;

    public AiMappingImportRunnerRepository(IDbReader dbReader, IDbWriter dbWriter, IFileService fileService, IOptions<AppSettings> options)
    {
        _dbReader = dbReader;
        _dbWriter = dbWriter;
        _fileService = fileService;
        _options = options.Value.DatabaseSeeding;
    }

    public async Task<Dictionary<int, List<ElbMappingBackup>>> ReadAllBackupsAsync(CancellationToken token = default)
    {
        var dictionary = new Dictionary<int, List<ElbMappingBackup>>();

        foreach (var (id, book) in BibleBook.AllBooks)
        {
            var csvFilename = $"{book.Id.ToString().PadLeft(2, '0')}-{book.Name}.csv";
            var csvPath = _fileService.Combine(_options.SolutionAssetsPath, "Elb1871Mappings", csvFilename);

            if (_fileService.FileExists(csvPath))
            {
                using var reader = new StreamReader(csvPath);
                using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
                var mappings = csv.GetRecords<ElbMappingBackup>().ToList();
                dictionary.TryAdd(id, mappings);
            }
        }

        return dictionary;
    }

    public async Task<List<ElbWordDto>> ReadAllIdsAsync(CancellationToken token = default)
    {
        var sql = """

                  """;

        
    }
}
