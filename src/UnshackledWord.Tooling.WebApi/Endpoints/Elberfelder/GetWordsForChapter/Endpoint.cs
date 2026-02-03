using FastEndpoints;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Dbo;
using UnshackledWord.Domain.WebApi.BibleTagger.Reading;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.GetWordsForChapter;

public class Endpoint : Ep.Req<GetWordsOfChapterRequest>.Res<GetWordsOfChapterResponse>
{
    private readonly IDbReader _reader;

    public Endpoint(IDbReader reader)
    {
        _reader = reader;
    }

    public override void Configure()
    {
        Get("elberfelder/chapterWords/bookId/{BibleBookId:int}/chapterId/{chapterId:int}");
    }

    public override async Task<GetWordsOfChapterResponse> ExecuteAsync(GetWordsOfChapterRequest req, CancellationToken ct)
    {
        var sql = $"""
                   SELECT "Id", "Verse", "WordInContext", "PlainWord", "Lemma", "Strongs", "PositionInVerse"
                   FROM "unshackled-word"."Elb1871Words"
                   WHERE "BibleBookId" = {req.BibleBookId}
                      AND "Chapter" = {req.ChapterId}
                   ORDER BY "Verse", "PositionInVerse";
                   """;

        var verses = await _reader.ReadAsListAsync<Elb1871WordDbo>(sql);

        return new GetWordsOfChapterResponse
        {
            BibleBookId = req.BibleBookId,
            Chapter = req.ChapterId,
            Words = verses.Select(x =>
            {
                return new WordResponse
                {
                    Id = x.Id,
                    Lemma = x.Lemma,
                    PlainWord = x.PlainWord,
                    PositionInVerse = x.PositionInVerse,
                    Strongs = x.Strongs,
                    Verse = x.Verse,
                    WordInContext = x.WordInContext
                };
            }).ToList()
        };
    }
}
