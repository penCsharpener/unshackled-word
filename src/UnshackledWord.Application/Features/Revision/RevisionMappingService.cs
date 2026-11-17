using UnshackledWord.Domain.Models.Study;
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
                Role = r.Role == MappingRoles.Unit && !r.IsAddedWord ? MappingRoles.Unit : MappingRoles.Head,
                StrongsNumber = hebrew && !r.IsAddedWord && !string.IsNullOrWhiteSpace(r.StrongsNumber) ? r.StrongsNumber : null,
                AllowMultiHead = r.MultiHead && !r.IsAddedWord ? true : null,
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
            OriginalWords = originalWords.Select(w => w.ToResponse(withMorphemes: hebrew)).ToList(),
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
        var hebrew = RevisionSql.IsHebrew(bookId);
        var words = await _repository.GetWordsAsync(revisionVerseId, token);
        var originalWords = await _verseMapping.GetOriginalWordsAsync(hebrew, bookId, chapter, verse, token);
        var stored = await _repository.GetMappingAsync(revisionVerseId, token);

        // Revision words have no STTS tag, so the APPRART exception of the unit rule does not apply here.
        var germanWords = words.Select(w => new VerseGermanWordRow { Id = w.Id, PositionInVerse = w.PositionInVerse, Text = w.WordInContext }).ToList();
        var roleAware = MappingRoles.IsRoleAware(links);
        if (!roleAware)
        {
            links = VerseMappingService.KeepStoredRoles(
                links,
                stored.Select(r => (r.RevisionWordId, r.StepWordId, r.IsAddedWord, r.Role, r.StrongsNumber, r.MultiHead, r.GermanWordPart, r.PartLemma)),
                germanWords,
                originalWords,
                hebrew);
        }

        // Compound parts are not edited in revisions (kept from the stored row below), so the input has no word texts.
        var input = VerseMappingService.BuildInput(links, germanWords, originalWords, hebrew, enforceSingleHead: roleAware, partsSupported: false);
        var validation = await new SaveVerseMappingValidator().ValidateAsync(input, token);

        if (!validation.IsValid)
        {
            return new SaveRevisionMappingResult
            {
                Outcome = SaveRevisionMappingOutcome.Invalid,
                Errors = validation.Errors.Select(e => e.ErrorMessage).ToList(),
            };
        }

        var existing = stored
            .GroupBy(r => (r.RevisionWordId, r.StepWordId))
            .ToDictionary(g => g.Key, g => g.First());
        var multiHead = VerseMappingService.AcceptedMultiHeads(links, input);

        var rows = links
            .Select(l =>
            {
                var step = l.IsAddedWord ? null : l.StepWordId;
                var parent = l.IsAddedWord ? l.ParentElbWordId : null;
                existing.TryGetValue((l.ElbWordId, step), out var old);
                var real = !l.IsAddedWord && step is not null;
                var role = real && MappingRoles.IsUnit(l) ? MappingRoles.Unit : MappingRoles.Head;
                var strongs = hebrew && real && !string.IsNullOrWhiteSpace(l.StrongsNumber) ? input.MorphemeKey(l).Morpheme : null;
                var unchanged = old is not null && old.IsAddedWord == l.IsAddedWord && old.ParentRevisionWordId == parent
                                && old.Role == role && old.StrongsNumber == strongs;

                return new RevisionMappingRow
                {
                    RevisionWordId = l.ElbWordId,
                    StepWordId = step,
                    IsAddedWord = l.IsAddedWord,
                    ParentRevisionWordId = parent,
                    GermanWordPart = old?.GermanWordPart,
                    Origin = unchanged ? old!.Origin : RevisionOrigin.Manual,
                    Role = role,
                    StrongsNumber = strongs,
                    PartLemma = old?.GermanWordPart is null ? null : old.PartLemma,
                    MultiHead = real && role == MappingRoles.Head && multiHead.Contains(input.MorphemeKey(l)),
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
