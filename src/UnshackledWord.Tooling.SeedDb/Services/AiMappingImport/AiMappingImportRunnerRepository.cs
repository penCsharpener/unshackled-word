using System.Globalization;
using CsvHelper;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Backup;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.Models.Dbo;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Infrastructure.Repositories;

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
                foreach (var group in mappings.GroupBy(x => x.HebRefId))
                {
                    dictionary.TryAdd(group.Key, group.Where(x => x.ElbWord.IsNotNullOrEmpty()).ToList());
                }
            }
        }

        return dictionary;
    }

    public async Task<Dictionary<int, List<StepWordDto>>> ReadAllStepIdsAsync(CancellationToken token = default)
    {
        var sql = """
                  SELECT sgw."Id" "StepWordId", bvcm."HebRefId", sgw."PositionInVerse", sgw."Greek" "StepWord"
                  FROM "unshackled-word"."StepGreekWords" sgw
                      INNER JOIN "unshackled-word"."BibleVerseCountingMapping" bvcm ON sgw."LxxRefId" = bvcm."LxxRefId"
                  UNION
                  SELECT shw."Id" "StepWordId", bvcm."HebRefId", shw."PositionInVerse", shw."Hebrew" "StepWord"
                  FROM "unshackled-word"."StepHebrewWords" shw
                      INNER JOIN "unshackled-word"."BibleVerseCountingMapping" bvcm ON shw."LxxRefId" = bvcm."LxxRefId";
                  """;

        return (await _dbReader.ReadAsListAsync<StepWordDto>(sql))
            .GroupBy(x => x.HebRefId)
            .ToDictionary(x => x.Key, y => y.ToList());
    }

    public async Task<Dictionary<int, List<ElbWordDto>>> ReadAllElbIdsAsync(CancellationToken token = default)
    {
        var sql = """
                  SELECT ew."Id" "ElbWordId", ew."HebRefId", ew."PositionInVerse", ew."PlainWord" "ElbWord"
                  FROM "unshackled-word"."Elb1871Words" ew
                  """;

        return (await _dbReader.ReadAsListAsync<ElbWordDto>(sql))
            .GroupBy(x => x.HebRefId)
            .ToDictionary(x => x.Key, y => y.ToList());
    }

    public async Task InsertMappingsAsync(IEnumerable<Elb1871MappingBase> mappings, string language)
    {
        var parameters = new
        {
            ElbWordId = new List<int>(),
            StepWordId = new List<int?>(),
            HebRefId = new List<int>(),
            PositionInVerse = new List<int>(),
            IsAddedWord = new List<bool>(),
            ParentGermanWordId = new List<int?>(),
            GermanWordPart = new List<string?>()
        };

        foreach (var mapping in mappings)
        {
            var refId = BibleReference.FromRefId(mapping.HebRefId);

            parameters.ElbWordId.Add(mapping.ElbWordId);
            parameters.StepWordId.Add(mapping.StepWordId);
            parameters.HebRefId.Add(refId.RefId);
            parameters.PositionInVerse.Add(mapping.PositionInVerse);
            parameters.IsAddedWord.Add(mapping.IsAddedWord);
            parameters.ParentGermanWordId.Add(mapping.ParentGermanWordId);
            parameters.GermanWordPart.Add(mapping.GermanWordPart);
        }

        var (quotedNames, parameterNames) = PropertyListHelper.GetAllNames(parameters);

        var sql = $"""
                   BEGIN;

                   INSERT INTO "unshackled-word"."Elb1871{language}MappingTest"
                   ({quotedNames})
                   SELECT *
                   FROM UNNEST({parameterNames})
                   ON CONFLICT ("ElbWordId", "StepWordId") DO NOTHING;

                   COMMIT;
                   """;

        await _dbWriter.WriteAsync(sql, parameters);
    }
}
