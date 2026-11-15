using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.CrossReferences;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.WebApi.Elberfelder.GetChapterCrossReferences;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.GetChapterCrossReferences;

public class Endpoint : Ep.Req<GetChapterCrossReferencesRequest>.Res<GetChapterCrossReferencesResponse>
{
    private readonly IChapterCrossReferenceService _service;

    public Endpoint(IChapterCrossReferenceService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("chapterCrossReferences/bookId/{BibleBookId:int}/chapterId/{ChapterId:int}");
        Group<RouteGroupConfig>();
        AllowAnonymous();
    }

    public override Task<GetChapterCrossReferencesResponse> ExecuteAsync(GetChapterCrossReferencesRequest req, CancellationToken ct)
    {
        return _service.GetAsync(req.BibleBookId, req.ChapterId, ct);
    }
}

public class GetChapterCrossReferencesRequestValidator : Validator<GetChapterCrossReferencesRequest>
{
    private static readonly Dictionary<int, BibleBook> Books = BibleBook.AllBooks;

    public GetChapterCrossReferencesRequestValidator()
    {
        RuleFor(x => x.BibleBookId).InclusiveBetween(1, 66);
        RuleFor(x => x.ChapterId)
            .Must((req, chapter) => Books.TryGetValue(req.BibleBookId, out var book)
                                    && chapter >= 1
                                    && chapter <= book.VersesPerChapterHeb.Length)
            .WithMessage("Chapter does not exist in this book.");
    }
}
