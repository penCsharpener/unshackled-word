using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.VerseSimilarity;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.WebApi.Elberfelder.GetSimilarVerses;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.GetSimilarVerses;

/// <summary>Similar verses by shared Strong's for a whole chapter (roadmap 0027.3). Public read, like the chapter cross references.</summary>
public class Endpoint : Ep.Req<GetSimilarVersesRequest>.Res<GetSimilarVersesResponse>
{
    private readonly IVerseSimilarityReadService _service;

    public Endpoint(IVerseSimilarityReadService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("similarVerses/{BibleBookId:int}/{Chapter:int}");
        Group<RouteGroupConfig>();
        AllowAnonymous();
    }

    public override Task<GetSimilarVersesResponse> ExecuteAsync(GetSimilarVersesRequest req, CancellationToken ct) =>
        _service.GetChapterAsync(req.BibleBookId, req.Chapter, req.Limit ?? VerseSimilarityReadService.DefaultLimit, ct);
}

public class GetSimilarVersesRequestValidator : Validator<GetSimilarVersesRequest>
{
    private static readonly Dictionary<int, BibleBook> Books = BibleBook.AllBooks;

    public GetSimilarVersesRequestValidator()
    {
        RuleFor(x => x.BibleBookId).InclusiveBetween(1, 66);
        RuleFor(x => x.Chapter)
            .Must((req, chapter) => Books.TryGetValue(req.BibleBookId, out var book)
                                    && chapter >= 1
                                    && chapter <= book.VersesPerChapterHeb.Length)
            .WithMessage("Chapter does not exist in this book.");
        RuleFor(x => x.Limit).InclusiveBetween(1, VerseSimilarityReadService.MaxLimit).When(x => x.Limit is not null);
    }
}
