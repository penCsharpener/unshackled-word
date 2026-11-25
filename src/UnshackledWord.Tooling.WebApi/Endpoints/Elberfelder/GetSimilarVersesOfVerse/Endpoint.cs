using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.VerseSimilarity;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.WebApi.Elberfelder.GetSimilarVerses;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.GetSimilarVersesOfVerse;

/// <summary>Up to 30 similar verses of one verse (roadmap 0027.3, the reader's "more"). Public read.</summary>
public class Endpoint : Ep.Req<GetSimilarVersesOfVerseRequest>.Res<VerseSimilarVersesResponse>
{
    private readonly IVerseSimilarityReadService _service;

    public Endpoint(IVerseSimilarityReadService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("similarVerses/verse/{HebRefId:int}");
        Group<RouteGroupConfig>();
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetSimilarVersesOfVerseRequest req, CancellationToken ct)
    {
        var response = await _service.GetVerseAsync(req.HebRefId, ct);
        if (response is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(response, ct);
    }
}

public class GetSimilarVersesOfVerseRequestValidator : Validator<GetSimilarVersesOfVerseRequest>
{
    private static readonly Dictionary<int, BibleBook> Books = BibleBook.AllBooks;

    public GetSimilarVersesOfVerseRequestValidator()
    {
        RuleFor(x => x.HebRefId)
            .Must(id => Books.TryGetValue(id / 1_000_000, out var book)
                        && id / 1_000 % 1_000 is var chapter && chapter >= 1 && chapter <= book.VersesPerChapterHeb.Length
                        && id % 1_000 >= 1)
            .WithMessage("Not an Elberfelder verse id (book * 1000000 + chapter * 1000 + verse).");
    }
}
