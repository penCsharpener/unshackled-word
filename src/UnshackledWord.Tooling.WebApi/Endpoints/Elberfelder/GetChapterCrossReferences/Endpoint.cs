using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.CrossReferences;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.WebApi.Elberfelder.GetChapterCrossReferences;
using UnshackledWord.Tooling.WebApi.Auth;

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
        // Hidden links (roadmap 0024.3) only for curators; everyone else gets the reader list whatever the query says.
        return _service.GetAsync(req.BibleBookId, req.ChapterId, req.IncludeHidden && User.HasWriteRole(), req.Level ?? 0, ct);
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
        RuleFor(x => x.Level).InclusiveBetween(0, 1000).When(x => x.Level is not null);
    }
}
