using UnshackledWord.Application.Features.VerseMapping;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping;
using UnshackledWord.Domain.WebApi.Revision;

namespace UnshackledWord.Application.Features.Revision;

public sealed class RevisionMappingService : IRevisionMappingService
{
    private readonly IRevisionRepository _repository;
    private readonly IVerseMappingRepository _verseMapping;

    public RevisionMappingService(IRevisionRepository repository, IVerseMappingRepository verseMapping)
    {
        _repository = repository;
        _verseMapping = verseMapping;
    }

    public async Task<RevisionVerseMappingResponse?> GetAsync(int revisionVerseId, CancellationToken token = default)
    {
        var header = await _repository.GetHeaderAsync(revisionVerseId, token);
        if (header is null)
        {
            return null;
        }

        var (bookId, chapter, verse) = RevisionSql.Split(header.HebRefId);
        var hebrew = RevisionSql.IsHebrew(bookId);
        var words = await _repository.GetWordsAsync(revisionVerseId, token);
        var rows = await _repository.GetMappingAsync(revisionVerseId, token);
        var originalWords = await _verseMapping.GetOriginalWordsAsync(hebrew, bookId, chapter, verse, token);

        var order = words.Select((w, i) => (w.Id, Index: i)).ToDictionary(x => x.Id, x => x.Index);
        var originalOrder = originalWords.Select((w, i) => (w.Id, Index: i)).ToDictionary(x => x.Id, x => x.Index);

        // Defensive like the Elb1871 read: a row whose word or original word is not part of this verse is dropped.
        var links = rows
            .Where(r => order.ContainsKey(r.RevisionWordId))
            .Where(r => r.IsAddedWord || (r.StepWordId is { } step && originalOrder.ContainsKey(step)))
            .Select(r => new RevisionMappingLink
            {
                ElbWordId = r.RevisionWordId,
                StepWordId = r.IsAddedWord ? null : r.StepWordId,
                IsAddedWord = r.IsAddedWord,
                ParentElbWordId = r.IsAddedWord && r.ParentRevisionWordId is { } parent && order.ContainsKey(parent) ? parent : null,
                Origin = r.Origin,
            })
            .Where(l => !l.IsAddedWord || l.ParentElbWordId is not null)
            .OrderBy(l => order[l.ElbWordId])
            .ThenBy(l => l.StepWordId is { } id ? originalOrder[id] : -1)
            .ToList();

        return new RevisionVerseMappingResponse
        {
            RevisionVerseId = header.Id,
            Level = header.Level,
            Version = header.Version,
            BibleBookId = bookId,
            Chapter = chapter,
            Verse = verse,
            Language = hebrew ? "hebrew" : "greek",
            MappingReviewedAt = header.MappingReviewedAt,
            GermanWords = words
                .Select(w => new RevisionMappingGermanWord { Id = w.Id, PositionInVerse = w.PositionInVerse, Text = w.WordInContext, Change = w.Change })
                .ToList(),
            OriginalWords = originalWords.Select(w => w.ToResponse()).ToList(),
            Links = links,
        };
    }

    public async Task<SaveRevisionMappingResult> SaveAsync(int revisionVerseId, IReadOnlyList<VerseMappingLink> links, int userId, CancellationToken token = default)
    {
        var header = await _repository.GetHeaderAsync(revisionVerseId, token);
        if (header is null)
        {
            return new SaveRevisionMappingResult { Outcome = SaveRevisionMappingOutcome.NotFound };
        }

        var (bookId, chapter, verse) = RevisionSql.Split(header.HebRefId);
        var words = await _repository.GetWordsAsync(revisionVerseId, token);
        var originalWords = await _verseMapping.GetOriginalWordsAsync(RevisionSql.IsHebrew(bookId), bookId, chapter, verse, token);

        var validation = await new SaveVerseMappingValidator().ValidateAsync(new VerseMappingSaveInput
        {
            Links = links,
            GermanWordIds = words.Select(w => w.Id).ToHashSet(),
            OriginalWordIds = originalWords.Select(w => w.Id).ToHashSet(),
        }, token);

        if (!validation.IsValid)
        {
            return new SaveRevisionMappingResult
            {
                Outcome = SaveRevisionMappingOutcome.Invalid,
                Errors = validation.Errors.Select(e => e.ErrorMessage).ToList(),
            };
        }

        var existing = (await _repository.GetMappingAsync(revisionVerseId, token))
            .GroupBy(r => (r.RevisionWordId, r.StepWordId))
            .ToDictionary(g => g.Key, g => g.First());

        var rows = links
            .Select(l =>
            {
                var step = l.IsAddedWord ? null : l.StepWordId;
                var parent = l.IsAddedWord ? l.ParentElbWordId : null;
                existing.TryGetValue((l.ElbWordId, step), out var old);
                var unchanged = old is not null && old.IsAddedWord == l.IsAddedWord && old.ParentRevisionWordId == parent;

                return new RevisionMappingRow
                {
                    RevisionWordId = l.ElbWordId,
                    StepWordId = step,
                    IsAddedWord = l.IsAddedWord,
                    ParentRevisionWordId = parent,
                    GermanWordPart = old?.GermanWordPart,
                    Origin = unchanged ? old!.Origin : RevisionOrigin.Manual,
                };
            })
            .ToList();

        if (!await _repository.ReplaceMappingAsync(revisionVerseId, RevisionSql.Lang(bookId), rows, userId, token))
        {
            return Conflict();
        }

        return new SaveRevisionMappingResult { Outcome = SaveRevisionMappingOutcome.Saved, Mapping = await GetAsync(revisionVerseId, token) };
    }

    private static SaveRevisionMappingResult Conflict() => new()
    {
        Outcome = SaveRevisionMappingOutcome.Conflict,
        Errors = ["The variant was withdrawn or its text was saved again in the meantime. Reload and try again."],
    };
}
