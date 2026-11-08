using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.WebApi.Elberfelder.GetVersesOfChapter;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.GetVersesOfChapter;

public class Endpoint : Ep.Req<GetVersesOfChapterRequest>.Res<GetVersesOfChapterResponse>
{
    private readonly IElb1871VerseRepository _verses;

    public Endpoint(IElb1871VerseRepository verses)
    {
        _verses = verses;
    }

    public override void Configure()
    {
        Get("chapterText/bookId/{BibleBookId:int}/chapterId/{ChapterId:int}");
        Group<RouteGroupConfig>();
        AllowAnonymous();
    }

    public override async Task<GetVersesOfChapterResponse> ExecuteAsync(GetVersesOfChapterRequest req, CancellationToken ct)
    {
        var start = req.BibleBookId * 1_000_000 + req.ChapterId * 1_000;
        var rows = await _verses.GetByHebRefIdRangeAsync(start, start + 999, 1000, ct);

        return new GetVersesOfChapterResponse
        {
            BibleBookId = req.BibleBookId,
            Chapter = req.ChapterId,
            Verses = rows.Select(r => new ChapterVerseResponse { Verse = r.HebRefId % 1000, Text = r.VerseText }).ToList()
        };
    }
}

public class GetVersesOfChapterValidator : Validator<GetVersesOfChapterRequest>
{
    private static readonly Dictionary<int, BibleBook> Books = BibleBook.AllBooks;

    public GetVersesOfChapterValidator()
    {
        RuleFor(x => x.BibleBookId).InclusiveBetween(1, 66);
        RuleFor(x => x.ChapterId)
            .Must((req, chapter) => Books.TryGetValue(req.BibleBookId, out var book)
                                    && chapter >= 1
                                    && chapter <= book.VersesPerChapterHeb.Length)
            .WithMessage("Chapter does not exist in this book.");
    }
}
