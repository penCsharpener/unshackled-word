using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Abstractions;
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
        Get("chapterWords/bookId/{BibleBookId:int}/chapterId/{chapterId:int}");
        Group<RouteGroupConfig>();
    }

    public override async Task<GetWordsOfChapterResponse> ExecuteAsync(GetWordsOfChapterRequest req, CancellationToken ct)
    {
        var (words, revisedVerses) = await ChapterWordsQuery.ReadAsync(_reader, req.BibleBookId, req.ChapterId, req.Level);

        return new GetWordsOfChapterResponse
        {
            BibleBookId = req.BibleBookId,
            Chapter = req.ChapterId,
            Words = words,
            RevisedVerses = revisedVerses
        };
    }
}

public class GetWordsOfChapterValidator : Validator<GetWordsOfChapterRequest>
{
    public GetWordsOfChapterValidator()
    {
        RuleFor(x => x.Level).InclusiveBetween(0, 1000);
    }
}
