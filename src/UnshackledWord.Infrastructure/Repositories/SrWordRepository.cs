using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Dbo;

namespace UnshackledWord.Infrastructure.Repositories;

public sealed class SrWordRepository : ISrWordRepository
{
    private readonly IDbReader _dbReader;

    public SrWordRepository(IDbReader dbReader)
    {
        _dbReader = dbReader;
    }

    public async Task<List<SrGntWordDbo>> GetWordForVerseAsync(int bookId, int chapterId, int verseId, CancellationToken token = default)
    {
        var sql = $"""
                   select *
                   from {SrGntWordDbo.DboName}
                   where     "{nameof(SrGntWordDbo.BibleBookId)}" = {bookId}
                         and "{nameof(SrGntWordDbo.Chapter)}"     = {chapterId}
                         and "{nameof(SrGntWordDbo.Verse)}"       = {verseId}
                   order by "{nameof(SrGntWordDbo.BibleBookId)}",
                            "{nameof(SrGntWordDbo.Chapter)}"    ,
                            "{nameof(SrGntWordDbo.Verse)}"      ,
                            "{nameof(SrGntWordDbo.PositionInVerse)}";
                   """;

        var result = await _dbReader.ReadAsListAsync<SrGntWordDbo>(sql);

        return result.ToList();
    }

    public async Task<List<SrGntWordDbo>> GetWordForChapterAsync(int bookId, int chapterId, CancellationToken token = default)
    {
        var sql = $"""
                   select *
                   from {SrGntWordDbo.DboName}
                   where     "{nameof(SrGntWordDbo.BibleBookId)}" = {bookId}
                         and "{nameof(SrGntWordDbo.Chapter)}"     = {chapterId}
                   order by "{nameof(SrGntWordDbo.BibleBookId)}",
                            "{nameof(SrGntWordDbo.Chapter)}"    ,
                            "{nameof(SrGntWordDbo.Verse)}"      ,
                            "{nameof(SrGntWordDbo.PositionInVerse)}";
                   """;

        var result = await _dbReader.ReadAsListAsync<SrGntWordDbo>(sql);

        return result.ToList();
    }
}
