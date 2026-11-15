using UnshackledWord.Application.Features.Seed.Anchoring;
using UnshackledWord.Domain.WebApi.Revision;

namespace UnshackledWord.Application.Features.Revision;

public sealed class RevisionService : IRevisionService
{
    private readonly IRevisionRepository _repository;
    private readonly IRevisionCarryService _carry;

    public RevisionService(IRevisionRepository repository, IRevisionCarryService carry)
    {
        _repository = repository;
        _carry = carry;
    }

    public async Task<SaveRevisionResult> SaveTextAsync(int bibleBookId, int chapter, int verse, SaveRevisionVerseRequest request, int userId, CancellationToken token = default)
    {
        var hebRefId = RevisionSql.HebRefId(bibleBookId, chapter, verse);

        if (await _repository.GetBaseVerseTextAsync(hebRefId, token) is null)
        {
            return new SaveRevisionResult { Outcome = SaveRevisionOutcome.NotFound };
        }

        if (!await _repository.IsWritableLevelAsync(request.Level, token))
        {
            return new SaveRevisionResult { Outcome = SaveRevisionOutcome.Invalid, Error = "level is not a revision preset (or is the Elb1871 base level)." };
        }

        var active = await _repository.GetActiveAsync(hebRefId, request.Level, token);
        if (active?.Version != request.ExpectedVersion)
        {
            return Conflict();
        }

        int? parentId = null;
        if (request.BasedOnLevel != RevisionSql.BaseOnlyLevel)
        {
            parentId = (await _repository.GetActiveAsync(hebRefId, request.BasedOnLevel, token))?.Id;
            if (parentId is null)
            {
                return new SaveRevisionResult { Outcome = SaveRevisionOutcome.Invalid, Error = "basedOnLevel has no active variant for this verse." };
            }
        }

        var tokens = RevisionTokenizer.Tokenize(request.Text);
        var baseWords = await _repository.GetBaseWordsAsync(hebRefId, token);
        var version = await _repository.GetLastVersionAsync(hebRefId, request.Level, token) + 1;
        var carry = parentId is { } parent
            ? await _carry.CarryFromVariantAsync(parent, tokens, token)
            : await _carry.CarryFromBaseAsync(bibleBookId, chapter, verse, baseWords, tokens, token);

        var id = await _repository.InsertVersionAsync(new NewRevisionVersion
        {
            HebRefId = hebRefId,
            Level = request.Level,
            Version = version,
            VerseText = string.Join(' ', tokens.Select(t => t.WordInContext)),
            BaseTextHash = ChapterFingerprint.Compute(baseWords.Select(w => AnchorToken.ForWord(hebRefId, w.PositionInVerse, w.PlainWord))),
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            CreatedByUserId = userId,
            BasedOnRevisionVerseId = parentId,
            EndActiveRevisionVerseId = active?.Id,
            Tokens = tokens,
            Words = carry.Words,
            CarriedRows = carry.Rows,
            Lang = RevisionSql.Lang(bibleBookId),
        }, userId, token);

        return id is null
            ? Conflict()
            : new SaveRevisionResult
            {
                Outcome = SaveRevisionOutcome.Saved,
                Saved = new SaveRevisionVerseResponse
                {
                    RevisionVerseId = id.Value,
                    Level = request.Level,
                    Version = version,
                    Carry = new RevisionCarryReportEntry
                    {
                        Same = carry.Report.SameCount,
                        Changed = carry.Report.ChangedCount,
                        New = carry.Report.NewCount,
                        LostOriginalWordIds = carry.Report.LostStepWordIds.ToList(),
                    },
                },
            };
    }

    public async Task<RevisionVerseResponse?> GetVerseAsync(int bibleBookId, int chapter, int verse, CancellationToken token = default)
    {
        var hebRefId = RevisionSql.HebRefId(bibleBookId, chapter, verse);
        var baseText = await _repository.GetBaseVerseTextAsync(hebRefId, token);
        if (baseText is null)
        {
            return null;
        }

        var variants = await _repository.GetActiveVariantsAsync(hebRefId, token);
        return new RevisionVerseResponse
        {
            BaseText = baseText,
            Variants = variants
                .OrderBy(v => v.Level)
                .Select(v => new RevisionVariantEntry
                {
                    RevisionVerseId = v.RevisionVerseId,
                    Level = v.Level,
                    Version = v.Version,
                    Text = v.VerseText,
                    CreatedAt = v.CreatedAt,
                    CreatedBy = v.CreatedBy,
                    MappingReviewedAt = v.MappingReviewedAt,
                })
                .ToList(),
        };
    }

    public async Task<RevisionHistoryResponse?> GetHistoryAsync(int bibleBookId, int chapter, int verse, int level, CancellationToken token = default)
    {
        var hebRefId = RevisionSql.HebRefId(bibleBookId, chapter, verse);
        if (await _repository.GetBaseVerseTextAsync(hebRefId, token) is null)
        {
            return null;
        }

        var rows = await _repository.GetHistoryAsync(hebRefId, level, token);
        return new RevisionHistoryResponse
        {
            Level = level,
            Versions = rows
                .OrderByDescending(r => r.Version)
                .Select(r => new RevisionHistoryEntry
                {
                    RevisionVerseId = r.RevisionVerseId,
                    Version = r.Version,
                    Text = r.VerseText,
                    Note = r.Note,
                    IsActive = r.IsActive,
                    CreatedAt = r.CreatedAt,
                    CreatedBy = r.CreatedBy,
                    EndedAt = r.EndedAt,
                })
                .ToList(),
        };
    }

    public async Task<WithdrawRevisionOutcome> WithdrawAsync(int revisionVerseId, int userId, CancellationToken token = default)
    {
        var header = await _repository.GetHeaderAsync(revisionVerseId, token);
        if (header is null)
        {
            return WithdrawRevisionOutcome.NotFound;
        }

        return header.IsActive && await _repository.EndVariantAsync(revisionVerseId, userId, token)
            ? WithdrawRevisionOutcome.Withdrawn
            : WithdrawRevisionOutcome.Conflict;
    }

    private static SaveRevisionResult Conflict() => new()
    {
        Outcome = SaveRevisionOutcome.Conflict,
        Error = "The variant changed in the meantime (expectedVersion is not the active version). Reload and try again.",
    };
}
