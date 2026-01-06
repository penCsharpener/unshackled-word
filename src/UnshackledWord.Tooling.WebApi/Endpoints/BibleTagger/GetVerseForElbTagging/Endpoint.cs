using FastEndpoints;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.WebApi.BibleTagger.GetVerse;

namespace UnshackledWord.Tooling.WebApi.Endpoints.BibleTagger.GetVerseForElbTagging;

public sealed class Endpoint : Ep.Req<GetVerseForElbTaggingRequest>.Res<GetVerseForElbTaggingResponse>
{
    private readonly IElb1871WordRepository _elbRepo;
    private readonly ISrWordRepository _srRepo;

    public Endpoint(IElb1871WordRepository elbRepo, ISrWordRepository srRepo)
    {
        _elbRepo = elbRepo;
        _srRepo = srRepo;
    }

    public override void Configure()
    {
        Get("bookId/{BibleBookId:int}/chapterId/{ChapterId:int}/verseId/{VerseId:int}");
        Group<RouteGroupConfig>();
    }

    public override async Task<GetVerseForElbTaggingResponse> ExecuteAsync(GetVerseForElbTaggingRequest req, CancellationToken ct)
    {
        var elbWords = await _elbRepo.GetWordForVerseAsync(req.BibleBookId, req.ChapterId, req.VerseId, ct);
        var srWords = await _srRepo.GetWordForVerseAsync(req.BibleBookId, req.ChapterId, req.VerseId, ct);

        var response = new GetVerseForElbTaggingResponse { ElberfelderWords = elbWords, SrWords = srWords };

        return response;
    }
}

