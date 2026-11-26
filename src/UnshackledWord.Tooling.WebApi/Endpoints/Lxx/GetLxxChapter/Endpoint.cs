using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.Lxx;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.WebApi.Lxx;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Lxx.GetLxxChapter;

/// <summary>The Septuagint (Swete text, Rahlfs labels) for one Elberfelder chapter (roadmap 0028.4). Public read; the general anonymous rate limit applies.</summary>
public class Endpoint : Ep.Req<GetLxxChapterRequest>.Res<GetLxxChapterResponse>
{
    private readonly ILxxChapterReadService _service;

    public Endpoint(ILxxChapterReadService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("chapter/{BibleBookId:int}/{Chapter:int}");
        Group<RouteGroupConfig>();
        AllowAnonymous();
    }

    public override Task<GetLxxChapterResponse> ExecuteAsync(GetLxxChapterRequest req, CancellationToken ct) =>
        _service.GetChapterAsync(req.BibleBookId, req.Chapter, ct);
}

public class GetLxxChapterRequestValidator : Validator<GetLxxChapterRequest>
{
    private static readonly Dictionary<int, BibleBook> Books = BibleBook.AllBooks;

    public GetLxxChapterRequestValidator()
    {
        RuleFor(x => x.BibleBookId).InclusiveBetween(1, 39).WithMessage("The Septuagint covers the Old Testament books 1..39.");
        RuleFor(x => x.Chapter)
            .Must((req, chapter) => Books.TryGetValue(req.BibleBookId, out var book)
                                    && chapter >= 1
                                    && chapter <= book.VersesPerChapterHeb.Length)
            .WithMessage("Chapter does not exist in this book.");
    }
}
