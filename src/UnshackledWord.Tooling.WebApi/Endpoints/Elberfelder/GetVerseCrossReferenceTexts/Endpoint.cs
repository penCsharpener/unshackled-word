using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.CrossReferences;
using UnshackledWord.Application.Features.Revision;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder;
using UnshackledWord.Domain.WebApi.Elberfelder.GetVerseCrossReferenceTexts;
using UnshackledWord.Domain.WebApi.Elberfelder.GetVersesOfChapter;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.GetVerseCrossReferenceTexts;

public class Endpoint : Ep.Req<GetVerseCrossReferenceTextsRequest>.Res<GetVerseCrossReferenceTextsResponse>
{
    private const int MaxReferences = 200;
    private const int MaxRangeVerses = 100;

    private readonly ITskRepository _tsk;
    private readonly IElb1871VerseRepository _verses;

    public Endpoint(ITskRepository tsk, IElb1871VerseRepository verses)
    {
        _tsk = tsk;
        _verses = verses;
    }

    public override void Configure()
    {
        Get("crossReferenceTexts/bookId/{BibleBookId:int}/chapterId/{ChapterId:int}/verseId/{VerseId:int}");
        Group<RouteGroupConfig>();
        AllowAnonymous();
    }

    public override async Task<GetVerseCrossReferenceTextsResponse> ExecuteAsync(GetVerseCrossReferenceTextsRequest req, CancellationToken ct)
    {
        var source = req.BibleBookId * 1_000_000 + req.ChapterId * 1_000 + req.VerseId;
        var rows = await _tsk.GetOutgoingAsync(source, source, MaxReferences, ct);
        var references = rows
            .Select(CrossReferenceMapper.Build)
            .DistinctBy(r => (r.BibleBookId, r.Chapter, r.Verse, r.EndVerse))
            .ToList();

        var result = new List<CrossReferenceTextResponse>(references.Count);
        foreach (var reference in references)
        {
            var from = reference.BibleBookId * 1_000_000 + reference.Chapter * 1_000 + reference.Verse;
            var to = from + Math.Min((reference.EndVerse ?? reference.Verse) - reference.Verse, MaxRangeVerses - 1);
            var rowsOfRange = req.Level == RevisionSql.BaseOnlyLevel
                ? (await _verses.GetByHebRefIdRangeAsync(from, to, 1000, ct)).Select(r => new EffectiveElbVerse { HebRefId = r.HebRefId, VerseText = r.VerseText }).ToList()
                : (await _verses.GetEffectiveByHebRefIdRangeAsync(from, to, req.Level, ct)).ToList();

            result.Add(new CrossReferenceTextResponse
            {
                BibleBookId = reference.BibleBookId,
                Chapter = reference.Chapter,
                Verse = reference.Verse,
                EndVerse = reference.EndVerse,
                Verses = rowsOfRange.Select(r => new ChapterVerseResponse { Verse = r.HebRefId % 1000, Text = r.VerseText }).ToList(),
            });
        }

        return new GetVerseCrossReferenceTextsResponse { References = result };
    }
}

public class GetVerseCrossReferenceTextsValidator : Validator<GetVerseCrossReferenceTextsRequest>
{
    private static readonly Dictionary<int, BibleBook> Books = BibleBook.AllBooks;

    public GetVerseCrossReferenceTextsValidator()
    {
        RuleFor(x => x.BibleBookId).InclusiveBetween(1, 66);
        RuleFor(x => x.Level).InclusiveBetween(0, 1000);
        RuleFor(x => x.VerseId).InclusiveBetween(0, 999);
        RuleFor(x => x.ChapterId)
            .Must((req, chapter) => Books.TryGetValue(req.BibleBookId, out var book)
                                    && chapter >= 1
                                    && chapter <= book.VersesPerChapterHeb.Length)
            .WithMessage("Chapter does not exist in this book.");
    }
}
