using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.Revision;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder;
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
        var (from, to) = RevisionSql.ChapterRange(req.BibleBookId, req.ChapterId);
        var rows = req.Level == RevisionSql.BaseOnlyLevel
            ? (await _verses.GetByHebRefIdRangeAsync(from, to, 1000, ct)).Select(r => new EffectiveElbVerse { HebRefId = r.HebRefId, VerseText = r.VerseText }).ToList()
            : (await _verses.GetEffectiveByHebRefIdRangeAsync(from, to, req.Level, ct)).ToList();
        var revised = rows
            .Where(r => r.RevisionVerseId.HasValue)
            .Select(r => new RevisedVerseResponse { Verse = r.HebRefId % 1000, Level = r.RevisionLevel ?? 0, RevisionVerseId = r.RevisionVerseId!.Value })
            .ToList();

        return new GetVersesOfChapterResponse
        {
            BibleBookId = req.BibleBookId,
            Chapter = req.ChapterId,
            Verses = rows.Select(r => new ChapterVerseResponse { Verse = r.HebRefId % 1000, Text = r.VerseText }).ToList(),
            RevisedVerses = revised.Count == 0 ? null : revised
        };
    }
}

public class GetVersesOfChapterValidator : Validator<GetVersesOfChapterRequest>
{
    private static readonly Dictionary<int, BibleBook> Books = BibleBook.AllBooks;

    public GetVersesOfChapterValidator()
    {
        RuleFor(x => x.BibleBookId).InclusiveBetween(1, 66);
        RuleFor(x => x.Level).InclusiveBetween(0, 1000);
        RuleFor(x => x.ChapterId)
            .Must((req, chapter) => Books.TryGetValue(req.BibleBookId, out var book)
                                    && chapter >= 1
                                    && chapter <= book.VersesPerChapterHeb.Length)
            .WithMessage("Chapter does not exist in this book.");
    }
}
