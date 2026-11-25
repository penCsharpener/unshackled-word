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
    private readonly IVerseMappingRepository _originals;
    private readonly IStrongsStatsRepository _stats;

    public Endpoint(ITskRepository tsk, IElb1871VerseRepository verses, IVerseMappingRepository originals, IStrongsStatsRepository stats)
    {
        _tsk = tsk;
        _verses = verses;
        _originals = originals;
        _stats = stats;
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
        // Same order, hiding and de-duplication as the chapter cross references (roadmap 0024.3).
        var references = CrossReferenceCuration.Arrange(rows)
            .Select(CrossReferenceMapper.Build)
            .DistinctBy(r => (r.BibleBookId, r.Chapter, r.Verse, r.EndVerse))
            .ToList();

        var result = new List<CrossReferenceTextResponse>(references.Count);
        var verseIds = new HashSet<int>();
        foreach (var reference in references)
        {
            var from = reference.BibleBookId * 1_000_000 + reference.Chapter * 1_000 + reference.Verse;
            var to = from + Math.Min((reference.EndVerse ?? reference.Verse) - reference.Verse, MaxRangeVerses - 1);
            var rowsOfRange = req.Level == RevisionSql.BaseOnlyLevel
                ? (await _verses.GetByHebRefIdRangeAsync(from, to, 1000, ct)).Select(r => new EffectiveElbVerse { HebRefId = r.HebRefId, VerseText = r.VerseText }).ToList()
                : (await _verses.GetEffectiveByHebRefIdRangeAsync(from, to, req.Level, ct)).ToList();

            verseIds.UnionWith(rowsOfRange.Select(r => r.HebRefId));
            result.Add(new CrossReferenceTextResponse
            {
                BibleBookId = reference.BibleBookId,
                Chapter = reference.Chapter,
                Verse = reference.Verse,
                EndVerse = reference.EndVerse,
                Rating = reference.Rating,
                Comment = reference.Comment,
                Origin = reference.Origin,
                Word = reference.Word,
                Verses = rowsOfRange.Select(r => new ChapterVerseResponse { Verse = r.HebRefId % 1000, Text = r.VerseText }).ToList(),
            });
        }

        // Originals of all target verses: one query per testament (same source and verse numbering as GetChapterOriginals: Elberfelder HebRefId = STEP LxxRefId).
        var originals = new Dictionary<int, VerseOriginalTextResponse>();
        var strongs = new Dictionary<int, List<string>>();
        var snapshot = await _stats.GetSnapshotRefreshedAtAsync(ct) is not null;
        foreach (var hebrew in new[] { true, false })
        {
            var ids = verseIds.Where(id => CrossReferenceOriginals.IsHebrewBook(id) == hebrew).ToList();
            var words = await _originals.GetOriginalWordsOfVersesAsync(hebrew, ids, ct);
            foreach (var (id, original) in CrossReferenceOriginals.Build(words, hebrew))
            {
                originals[id] = original;
            }

            // Very frequent lemmas are left out (stats snapshot); without a snapshot nothing is filtered.
            var frequent = snapshot
                ? await _stats.GetSnapshotFrequentBasesAsync(
                    words.Select(w => CrossReferenceOriginals.BaseNumber(w.Strongs)).OfType<string>().Distinct().ToList(),
                    hebrew,
                    StatsRules.OriginalHighFrequencyThreshold,
                    ct)
                : new HashSet<string>();
            foreach (var (id, keys) in CrossReferenceOriginals.BuildStrongs(words, frequent))
            {
                strongs[id] = keys;
            }
        }

        foreach (var reference in result)
        {
            var book = reference.BibleBookId * 1_000_000 + reference.Chapter * 1_000;
            foreach (var verse in reference.Verses)
            {
                verse.Original = originals.GetValueOrDefault(book + verse.Verse);
                verse.Strongs = strongs.GetValueOrDefault(book + verse.Verse);
            }
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
