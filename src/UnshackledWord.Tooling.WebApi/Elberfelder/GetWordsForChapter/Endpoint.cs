using FastEndpoints;
using UnshackledWord.Application.Abstractions;

namespace UnshackledWord.Tooling.WebApi.Elberfelder.GetWordsForChapter;

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
                   SELECT "Verse", "WordInContext", "PlainWord", "PositionInVerse"
                   FROM "unshackled-word"."Elb1871Words"
                   WHERE "BibleBookId" = {req.BibleBookId}
                   AND "Chapter" = {req.ChapterId};
                   """;

        var verses = await _reader.ReadAsListAsync<WordModel>(sql);

        return new GetWordsOfChapterResponse
        {
            BibleBookId = req.BibleBookId,
            Chapter = req.ChapterId,
            Words = verses.ToDictionary(k => $"{k.Verse}|{k.PositionInVerse}", v => new WordResponse { WordInContext = v.WordInContext, PlainWord = v.PlainWord })
        };
    }
}

public record GetWordsOfChapterRequest
{
    public int BibleBookId { get; set; }
    public int ChapterId { get; set; }
}

public record GetWordsOfChapterResponse
{
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public Dictionary<string, WordResponse> Words { get; set; }
}

public record WordResponse
{
    public string WordInContext { get; set; } = default!;
    public string PlainWord { get; set; } = default!;
}

public record WordModel : WordResponse
{
    public int Verse { get; set; }
    public int PositionInVerse { get; set; }
}
