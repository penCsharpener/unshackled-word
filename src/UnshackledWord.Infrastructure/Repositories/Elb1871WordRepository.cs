using System.Text;
using Dapper;
using Microsoft.Extensions.Logging;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.Dbo;
using UnshackledWord.Domain.Models.Dto;

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

    public async Task<List<Elb1871WordDbo>> GetWordForVerseAsync(int bookId, int chapterId, int verseId, CancellationToken token = default)
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

        var result = await _dbReader.ReadAsListAsync<Elb1871WordDbo>(sql);

        return result.ToList();
    }

    public async Task<List<Elb1871WordDbo>> GetWordForChapterAsync(int bookId, int chapterId, CancellationToken token = default)
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

        var result = await _dbReader.ReadAsListAsync<Elb1871WordDbo>(sql);

        return result.ToList();
    }
}
