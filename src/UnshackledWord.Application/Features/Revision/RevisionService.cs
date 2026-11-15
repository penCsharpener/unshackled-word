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

        RevisionActiveRow? parent = null;
        if (request.BasedOnLevel != RevisionSql.BaseOnlyLevel)
        {
            parent = await _repository.GetActiveAsync(hebRefId, request.BasedOnLevel, token);
            if (parent is null)
            {
                return new SaveRevisionResult { Outcome = SaveRevisionOutcome.Invalid, Error = "basedOnLevel has no variant for this verse." };
            }
        }

        // The carry source (Elb1871 or the parent variant) is read here, before the repository rewrites any row: a re-save at the same level
        // carries from the very words and mapping the save then deletes and replaces.
        var tokens = RevisionTokenizer.Tokenize(request.Text);
        var baseWords = await _repository.GetBaseWordsAsync(hebRefId, token);
        var carry = parent is not null
            ? await _carry.CarryFromVariantAsync(parent.Id, tokens, token)
            : await _carry.CarryFromBaseAsync(bibleBookId, chapter, verse, baseWords, tokens, token);

        var saved = await _repository.SaveVersionAsync(new NewRevisionVersion
        {
            HebRefId = hebRefId,
            Level = request.Level,
            ExpectedVersion = request.ExpectedVersion,
            VerseText = string.Join(' ', tokens.Select(t => t.WordInContext)),
            BaseTextHash = ChapterFingerprint.Compute(baseWords.Select(w => AnchorToken.ForWord(hebRefId, w.PositionInVerse, w.PlainWord))),
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            CreatedByUserId = userId,
            BasedOnRevisionId = parent?.RevisionId,
            BasedOnVersion = parent?.Version,
            Tokens = tokens,
            Words = carry.Words,
            CarriedRows = carry.Rows,
            Lang = RevisionSql.Lang(bibleBookId),
        }, token);

        return saved is null
            ? Conflict()
            : new SaveRevisionResult
            {
                Outcome = SaveRevisionOutcome.Saved,
                Saved = new SaveRevisionVerseResponse
                {
                    RevisionVerseId = saved.RevisionVerseId,
                    Level = request.Level,
                    Version = saved.Version,
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

        var rows = (await _repository.GetHistoryAsync(hebRefId, level, token)).OrderByDescending(r => r.AuditId).ToList();
        var entries = new List<RevisionHistoryEntry>(rows.Count);
        DateTime? nextEnd = null; // At of the next (newer) text or withdraw entry
        var newestTextSeen = false;
        var withdrawn = false;

        foreach (var r in rows)
        {
            var isText = r.Action == RevisionHistoryAction.Text;
            var isActive = isText && !newestTextSeen && !withdrawn;
            if (r.Action == RevisionHistoryAction.Withdraw && !newestTextSeen)
            {
                withdrawn = true;
            }

            entries.Add(new RevisionHistoryEntry
            {
                AuditId = r.AuditId,
                Version = r.Version,
                Action = r.Action,
                Text = r.VerseText,
                Note = r.Note,
                IsActive = isActive,
                At = r.At,
                By = r.By,
                EndedAt = isText ? nextEnd : null,
            });

            if (isText)
            {
                newestTextSeen = true;
            }

            if (isText || r.Action == RevisionHistoryAction.Withdraw)
            {
                nextEnd = r.At;
            }
        }

        return new RevisionHistoryResponse { Level = level, Versions = entries };
    }

    public async Task<WithdrawRevisionOutcome> WithdrawAsync(int revisionVerseId, int userId, CancellationToken token = default)
    {
        var header = await _repository.GetHeaderAsync(revisionVerseId, token);
        if (header is null)
        {
            return WithdrawRevisionOutcome.NotFound;
        }

        return await _repository.EndVariantAsync(revisionVerseId, userId, token)
            ? WithdrawRevisionOutcome.Withdrawn
            : WithdrawRevisionOutcome.Conflict;
    }

    private static SaveRevisionResult Conflict() => new()
    {
        Outcome = SaveRevisionOutcome.Conflict,
        Error = "The variant changed in the meantime (expectedVersion is not the live version). Reload and try again.",
    };
}
