using System.Text;
using Microsoft.Extensions.Logging;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.Dbo;

namespace UnshackledWord.Infrastructure.Repositories;

public sealed class Elb1871WordRepository : IElb1871WordRepository
{
    private readonly IDbReader _dbReader;
    private readonly ILogger<Elb1871WordRepository> _logger;
    private readonly IDbWriter _dbWriter;

    public Elb1871WordRepository(IDbWriter dbWriter, IDbReader dbReader, ILogger<Elb1871WordRepository> logger)
    {
        _dbWriter = dbWriter;
        _dbReader = dbReader;
        _logger = logger;
    }

    public async Task<IEnumerable<Elb1871WordDbo>> GetWordForVerseAsync(int bookId, int chapterId, int verseId, CancellationToken token = default)
    {
        var sql = $"""
                   select *
                   from {Elb1871WordDbo.DboName}
                   where     "{nameof(Elb1871WordDbo.BibleBookId)}" = {bookId}
                         and "{nameof(Elb1871WordDbo.Chapter)}"     = {chapterId}
                         and "{nameof(Elb1871WordDbo.Verse)}"       = {verseId}
                   order by "{nameof(Elb1871WordDbo.BibleBookId)}",
                            "{nameof(Elb1871WordDbo.Chapter)}"    ,
                            "{nameof(Elb1871WordDbo.Verse)}"      ,
                            "{nameof(Elb1871WordDbo.PositionInVerse)}";
                   """;

        return await _dbReader.ReadAsListAsync<Elb1871WordDbo>(sql);
    }

    public async Task<IEnumerable<Elb1871WordDbo>> GetWordForChapterAsync(int bookId, int chapterId, CancellationToken token = default)
    {
        var sql = $"""
                   select *
                   from {Elb1871WordDbo.DboName}
                   where     "{nameof(Elb1871WordDbo.BibleBookId)}" = {bookId}
                         and "{nameof(Elb1871WordDbo.Chapter)}"     = {chapterId}
                   order by "{nameof(Elb1871WordDbo.BibleBookId)}",
                            "{nameof(Elb1871WordDbo.Chapter)}"    ,
                            "{nameof(Elb1871WordDbo.Verse)}"      ,
                            "{nameof(Elb1871WordDbo.PositionInVerse)}";
                   """;

        return await _dbReader.ReadAsListAsync<Elb1871WordDbo>(sql);
    }

    public async Task<List<int>> BulkUpdateStrongsAsync(List<Elb1871WordDbo> modifiedWords, CancellationToken token = default)
    {
        if (modifiedWords.Count == 0)
        {
            return [];
        }

        var sb = new StringBuilder();
        var modifiedRows = new List<int>();

        foreach (var elbWord in modifiedWords)
        {
            var sql = $"UPDATE {Elb1871WordDbo.DboName} SET \"{nameof(elbWord.Strongs)}\"=@Strongs WHERE \"{nameof(elbWord.Id)}\"={elbWord.Id};";
            modifiedRows.Add(elbWord.Id);

            sb.AppendLine(sql);
        }

        var param = new { modifiedWords.First().Strongs };

        await _dbWriter.WriteAsync(sb.ToString(), param);
        _logger.LogInformation("Updated rows with strongs {strongs}: {ids}", param.Strongs, modifiedRows.JoinStrings(","));

        return modifiedRows;
    }

    public async Task BulkUpdateGrammarAsync(IEnumerable<Elb1871WordDbo> modifiedWords, CancellationToken token = default)
    {

    }
}
