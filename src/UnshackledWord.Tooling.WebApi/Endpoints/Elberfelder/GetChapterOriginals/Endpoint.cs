using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.ChapterOriginals;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.WebApi.Elberfelder.GetChapterOriginals;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.GetChapterOriginals;

public class Endpoint : Ep.Req<GetChapterOriginalsRequest>.Res<GetChapterOriginalsResponse>
{
    private readonly IChapterOriginalsService _service;

    public Endpoint(IChapterOriginalsService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("chapterOriginals/bookId/{BibleBookId:int}/chapterId/{ChapterId:int}");
        Group<RouteGroupConfig>();
        AllowAnonymous();
    }

    public override Task<GetChapterOriginalsResponse> ExecuteAsync(GetChapterOriginalsRequest req, CancellationToken ct)
    {
        return _service.GetAsync(req.BibleBookId, req.ChapterId, ct);
    }
}

public class GetChapterOriginalsRequestValidator : Validator<GetChapterOriginalsRequest>
{
    private static readonly Dictionary<int, BibleBook> Books = BibleBook.AllBooks;

    public GetChapterOriginalsRequestValidator()
    {
        RuleFor(x => x.BibleBookId).InclusiveBetween(1, 66);
        RuleFor(x => x.ChapterId)
            .Must((req, chapter) => Books.TryGetValue(req.BibleBookId, out var book)
                                    && chapter >= 1
                                    && chapter <= book.VersesPerChapterHeb.Length)
            .WithMessage("Chapter does not exist in this book.");
    }
}
