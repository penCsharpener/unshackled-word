using System.Text;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.Dbo;
using UnshackledWord.Infrastructure.Repositories;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;

public sealed class Elberfelder1871VerseStrategy : IFileParserStrategy
{
    private readonly IDbReader _reader;
    private readonly IDbWriter _writer;
    private readonly IFileService _fileService;
    private readonly ILogger<Elberfelder1871Strategy> _logger;

    public Elberfelder1871VerseStrategy(IDbReader reader, IDbWriter writer, IFileService fileService, ILogger<Elberfelder1871Strategy> logger)
    {
        _reader = reader;
        _writer = writer;
        _fileService = fileService;
        _logger = logger;
    }

    public async Task SaveToDatabase(string filePath, CancellationToken token = default)
    {
        var count = await GetVersesCountAsync(token);

        if (count > 0)
        {
            _logger.LogInformation("Elberfelder 1871 verses already exist in the database. Skipping import. " +
                                   "{count} rows of words ", count);
            return;
        }

        var lines = await _fileService.ReadAllLinesAsync(filePath, Encoding.UTF8, token);
        var i = 1;
        var verses = new List<Elb1871VersesDbo>();

        foreach (var line in lines)
        {
            var lineItem = new ElbExportLineItem(line);

            var elbVerse = new Elb1871VersesDbo
            {
                Id = i,
                HebRefId = lineItem.HebRefId.RefId,
                LxxRefId = lineItem.LxxRefId.RefId,
                VerseText = lineItem.Verse
            };

            verses.Add(elbVerse);
            i++;
        }

        var parameters = new
        {
            Id = new List<int>(),
            HebRefId = new List<int>(),
            LxxRefId = new List<int>(),
            VerseText = new List<string>(),
        };

        foreach (var verse in verses)
        {
            parameters.Id.Add(verse.Id);
            parameters.HebRefId.Add(verse.HebRefId);
            parameters.LxxRefId.Add(verse.LxxRefId);
            parameters.VerseText.Add(verse.VerseText);
        }

        var names = PropertyListHelper.GetPropertyNames(parameters);

        var sql = $"""
                   INSERT INTO {Elb1871VersesDbo.DbName} (
                       {names.Select(x => $"\"{x}\"").JoinStrings(",")}
                   )
                   SELECT *
                   FROM UNNEST({names.Select(x => $"@{x}").JoinStrings(",")})
                   ON CONFLICT DO NOTHING;
                   """;

        await _writer.WriteAsync(sql, parameters);
    }

    private async Task<int> GetVersesCountAsync(CancellationToken token = default)
    {
        var sql = $"""
                   select Count(*)
                   from {Elb1871VersesDbo.DbName}
                   """;

        return await _reader.ExecuteScalarAsync<int>(sql);
    }
}
