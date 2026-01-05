using FastEndpoints;
using UnshackledWord.Application.Abstractions;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.GetVersesOfChapter;

public class Endpoint : Ep.Req<GetVersesOfChapterRequest>.Res<GetVersesOfChapterResponse>
{
    private readonly IDbReader _reader;

    public Endpoint(IDbReader reader)
    {
        _reader = reader;
    }

    public override void Configure()
    {
        Get("elberfelder/chapterText/bookId/{BibleBookId:int}/chapterId/{chapterId:int}");
    }

    public override async Task<GetVersesOfChapterResponse> ExecuteAsync(GetVersesOfChapterRequest req, CancellationToken ct)
    {
        var sql = $"""
                  SELECT "Verse", "VerseText"
                  FROM "unshackled-word"."Elb1871Verses"
                  WHERE "BibleBookId" = {req.BibleBookId}
                  AND "Chapter" = {req.ChapterId};
                  """;

        var verses = await _reader.ReadAsListAsync<VerseModel>(sql);

        return new GetVersesOfChapterResponse
        {
            BibleBookId = req.BibleBookId,
            Chapter = req.ChapterId,
            Verses = verses.ToDictionary(k => k.Verse, v => v.VerseText)
        };
    }
}

public record GetVersesOfChapterRequest
{
    public int BibleBookId { get; set; }
    public int ChapterId { get; set; }
}

public record GetVersesOfChapterResponse
{
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public Dictionary<int, string> Verses { get; set; }
}

public record VerseModel
{
    public int Verse { get; set; }
    public string VerseText { get; set; } = default!;
}
