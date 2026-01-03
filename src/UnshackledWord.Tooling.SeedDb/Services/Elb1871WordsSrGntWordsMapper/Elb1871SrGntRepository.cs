using UnshackledWord.Application.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.Elb1871WordsSrGntWordsMapper;

public sealed class Elb1871SrGntRepository
{
    private readonly IDbReader _dbReader;
    private readonly ILogger<Elb1871SrGntRepository> _logger;

    public Elb1871SrGntRepository(IDbReader dbReader, ILogger<Elb1871SrGntRepository> logger)
    {
        _dbReader = dbReader;
        _logger = logger;
    }

    public async Task<List<NtWordsPerVerse>> GetElb1871Async(CancellationToken token = default)
    {
        var sql = $"""
                   SELECT elb."BibleBookId",
                          elb."Chapter" ChapterId,
                          elb."Verse" VerseId,
                          COALESCE(elb."Lemma", elb."PlainWord") Word,
                          elb."Strongs" Strongs,
                          elb."Id" WordId
                   FROM "unshackled-word"."Elb1871Words" elb
                   WHERE elb."BibleBookId" >= 40
                   """;

        var list = await _dbReader.ReadAsListAsync<NtWord>(sql);

        return list.GroupBy(x => new { x.BibleBookId, x.ChapterId, x.VerseId }).Select(g =>
            new NtWordsPerVerse
            {
                BibleBookId = g.Key.BibleBookId,
                ChapterId = g.Key.ChapterId,
                VerseId = g.Key.VerseId,
                Words = g.Select(x => x).ToList()
            }).ToList();
    }

    public async Task<List<NtWordsPerVerse>> GetSrWordsAsync(CancellationToken token = default)
    {
        var sql = $"""
                   SELECT sr."BibleBookId",
                          sr."Chapter" ChapterId,
                          sr."Verse" VerseId,
                          sr."Lemma" Word,
                          sr."Strongs" Strongs,
                          sr."Id" WordId
                   FROM "unshackled-word"."SrGntWords" sr
                   """;

        var list = await _dbReader.ReadAsListAsync<NtWord>(sql);

        return list.GroupBy(x => new { x.BibleBookId, x.ChapterId, x.VerseId }).Select(g =>
            new NtWordsPerVerse
            {
                BibleBookId = g.Key.BibleBookId,
                ChapterId = g.Key.ChapterId,
                VerseId = g.Key.VerseId,
                Words = g.Select(x => x).ToList()
            }).ToList();
    }
}
