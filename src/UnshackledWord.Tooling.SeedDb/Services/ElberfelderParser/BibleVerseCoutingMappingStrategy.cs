using System.Text;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Dbo;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;

public sealed class BibleVerseCoutingMappingStrategy : IFileParserStrategy
{
    private readonly IFileService _fileService;
    private readonly IDbWriter _writer;
    private readonly IDbReader _reader;
    private readonly ILogger<Elberfelder1871Strategy> _logger;

    public BibleVerseCoutingMappingStrategy(IFileService fileService, IDbWriter writer, IDbReader reader, ILogger<Elberfelder1871Strategy> logger)
    {
        _fileService = fileService;
        _writer = writer;
        _reader = reader;
        _logger = logger;
    }

    public async Task SaveToDatabase(string filePath, CancellationToken token = default)
    {
        var count = await GetCountVerseCoutingMappingsAsync(token);
        if (count > 0)
        {
            return;
        }

        var lines = await _fileService.ReadAllLinesAsync(filePath, Encoding.UTF8, token);
        var totalMappings = new List<BibleVerseCountingMappingDbo>();

        for (int i = 0; i < lines.Length; i++)
        {
            var lineItem = new ElbExportLineItem(lines[i]);
            var mapping = new BibleVerseCountingMappingDbo
            {
                HebRefId = lineItem.HebRefId.RefId, LxxRefId = lineItem.LxxRefId.RefId
            };
            totalMappings.AddRange(mapping);
        }

        await BulkInsertIntoDatabaseAsync(totalMappings, token);
    }

    private async Task BulkInsertIntoDatabaseAsync(List<BibleVerseCountingMappingDbo> mappings, CancellationToken token = default)
    {
        var parameters = new
        {
            LxxRefIds = mappings.Select(x => x.LxxRefId).ToArray(),
            HebRefIds = mappings.Select(x => x.HebRefId).ToArray(),
        };

        var sql = $"""
                   INSERT INTO {BibleVerseCountingMappingDbo.DboName} ("HebRefId", "LxxRefId")
                   SELECT *
                   FROM UNNEST(@HebRefIds, @LxxRefIds)
                   """;

        await _writer.WriteAsync(sql, parameters);
    }

    private async Task<int> GetCountVerseCoutingMappingsAsync(CancellationToken token = default)
    {
        var sql = $"""
                   select Count(*)
                   from {BibleVerseCountingMappingDbo.DboName}
                   """;

        return await _reader.ExecuteScalarAsync<int>(sql);
    }
}
