using System.Data;
using System.Text.Json;
using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Vocab;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Infrastructure.Vocab;

public enum VocabEditStatus
{
    Ok,
    NotFound,
    Conflict,
    Invalid
}

/// <summary>The result of one editor operation: <see cref="Response"/> on success, otherwise a status and a message for the maintainer.</summary>
public sealed class VocabEditOutcome
{
    public VocabEditStatus Status { get; init; }
    public string? Message { get; init; }
    public VocabEditResponse? Response { get; init; }

    public static VocabEditOutcome Ok(VocabEditResponse response) => new() { Status = VocabEditStatus.Ok, Response = response };

    public static VocabEditOutcome NotFound(string message) => new() { Status = VocabEditStatus.NotFound, Message = message };

    public static VocabEditOutcome Conflict(string message) => new() { Status = VocabEditStatus.Conflict, Message = message };

    public static VocabEditOutcome Invalid(string message) => new() { Status = VocabEditStatus.Invalid, Message = message };
}

/// <summary>
/// Write side of the vocab editor (roadmap 0022.4, <c>api/maintainer/vocab/entries/{id}</c>). Every operation is one transaction: the
/// change itself, the <c>ContentVersion</c> bump and the <c>VocabEntryChanges</c> row for learning-relevant changes (see
/// <see cref="VocabChangeRules"/>), <c>UpdatedAt</c>/<c>UpdatedByUserId</c>, and the frequency ranks where keys or occurrences move. An
/// operation bumps the version of an entry at most once. Ranks, order, pins, the reviewed mark and the transliteration bump nothing. The
/// static methods run on the caller's connection and transaction (tests roll it back); the instance methods open and commit their own.
/// </summary>
public sealed class VocabEntryEditor
{
    private const int TimeoutSeconds = 120;
    private const int MaxLemmaLength = 50;

    private const string SelectEntryForUpdate = """
        SELECT e."Id"
             , e."Track"::text      AS "Track"
             , e."LanguageId"
             , e."Lemma"
             , e."Pos"
             , e."DictionaryForm"
             , e."DictionaryFormOrigin"
             , e."Transliteration"
             , e."IsProperName"
             , e."Occurrences"
             , e."ReviewedAt"
             , e."RetiredAt"
             , e."ContentVersion"
        FROM "unshackled-word"."VocabEntries" e
        WHERE 1=1
            AND e."Id" = @Id
        FOR UPDATE
        """;

    private const string UpdateEntry = """
        UPDATE "unshackled-word"."VocabEntries"
        SET "DictionaryForm" = @DictionaryForm
          , "DictionaryFormOrigin" = @DictionaryFormOrigin
          , "Transliteration" = @Transliteration
          , "ReviewedAt" = @ReviewedAt
          , "ReviewedByUserId" = @ReviewedByUserId
          , "ContentVersion" = "ContentVersion" + @Bump
          , "UpdatedAt" = now()
          , "UpdatedByUserId" = @UserId
        WHERE 1=1
            AND "Id" = @Id
        RETURNING "ContentVersion"
        """;

    private const string BumpVersion = """
        UPDATE "unshackled-word"."VocabEntries"
        SET "ContentVersion" = "ContentVersion" + 1
          , "UpdatedAt" = now()
          , "UpdatedByUserId" = @UserId
        WHERE 1=1
            AND "Id" = @Id
        RETURNING "ContentVersion"
        """;

    private const string TouchEntry = """
        UPDATE "unshackled-word"."VocabEntries"
        SET "UpdatedAt" = now()
          , "UpdatedByUserId" = @UserId
        WHERE 1=1
            AND "Id" = @Id
        """;

    private const string InsertChange = """
        INSERT INTO "unshackled-word"."VocabEntryChanges"
            ("VocabEntryId", "ContentVersion", "Kind", "Before", "After", "TargetEntryId", "ChangedByUserId")
        VALUES (@EntryId, @ContentVersion, @Kind, @Before::jsonb, @After::jsonb, @TargetEntryId, @UserId)
        """;

    private const string SelectAcceptedGerman = """
        SELECT m."German"
        FROM "unshackled-word"."VocabMeanings" m
        WHERE 1=1
            AND m."VocabEntryId" = @Id
            AND m."IsAccepted"
        ORDER BY m."Rank", m."Id"
        """;

    private const string SelectMeaningRows = """
        SELECT m."Id"
             , m."German"
             , m."Rank"
             , m."IsAccepted"
             , m."Origin"
             , m."IsPinned"
        FROM "unshackled-word"."VocabMeanings" m
        WHERE 1=1
            AND m."VocabEntryId" = @Id
        ORDER BY m."Rank", m."Id"
        """;

    private const string SelectKeyNames = """
        SELECT vs."StrongsKey"
        FROM "unshackled-word"."VocabStrongs" vs
        WHERE 1=1
            AND vs."VocabEntryId" = @Id
        ORDER BY vs."StrongsKey"
        """;

    private const string InsertManualMeaning = """
        INSERT INTO "unshackled-word"."VocabMeanings"
            ("VocabEntryId", "German", "Rank", "IsAccepted", "Origin", "IsPinned")
        VALUES (@Id, @German, COALESCE((SELECT max(m."Rank") FROM "unshackled-word"."VocabMeanings" m WHERE m."VocabEntryId" = @Id), 0) + 1, true, 'manual', false)
        RETURNING "Id"
        """;

    private const string UpdateMeaningFlags = """
        UPDATE "unshackled-word"."VocabMeanings"
        SET "IsAccepted" = @IsAccepted
          , "IsPinned" = @IsPinned
        WHERE 1=1
            AND "Id" = @MeaningId
        """;

    private const string DeleteMeaning = """
        DELETE FROM "unshackled-word"."VocabMeanings"
        WHERE 1=1
            AND "Id" = @MeaningId
        """;

    private const string SetMeaningRanks = """
        UPDATE "unshackled-word"."VocabMeanings" m
        SET "Rank" = o."Rank"
        FROM unnest(@Ids::int[], @Ranks::int[]) AS o("Id", "Rank")
        WHERE 1=1
            AND m."Id" = o."Id"
            AND m."VocabEntryId" = @EntryId
        """;

    private const string RenumberMeanings = """
        UPDATE "unshackled-word"."VocabMeanings" m
        SET "Rank" = r."NewRank"
        FROM (
            SELECT x."Id", row_number() OVER (ORDER BY x."Rank", x."Id")::int AS "NewRank"
            FROM "unshackled-word"."VocabMeanings" x
            WHERE x."VocabEntryId" = @EntryId
        ) r
        WHERE 1=1
            AND m."Id" = r."Id"
            AND m."Rank" IS DISTINCT FROM r."NewRank"
        """;

    private const string SelectKeyOccurrences = """
        SELECT vs."Occurrences"
        FROM "unshackled-word"."VocabStrongs" vs
        WHERE 1=1
            AND vs."VocabEntryId" = @Id
            AND vs."StrongsKey" = @Key
        """;

    private const string SelectKeyLexicon = """
        SELECT x."OriginalWord"    AS "Lemma"
             , x."Transliteration"
             , x."LanguageId"
             , left(x."Morphology", 20) AS "Pos"
        FROM "unshackled-word"."StepStrongsLexicon" x
        WHERE 1=1
            AND x."OriginalWord" <> ''
            AND x."Number" = @Number
            AND CASE WHEN x."LanguageId" = 2 THEN 'G' ELSE 'H' END = @Lang
            AND (@Lang || CASE WHEN x."Number" >= 10000 THEN x."Number"::text ELSE lpad(x."Number"::text, 4, '0') END || upper(COALESCE(x."Extra", ''))) = @Key
        ORDER BY x."Id"
        LIMIT 1
        """;

    private const string InsertNewEntry = """
        INSERT INTO "unshackled-word"."VocabEntries"
            ("Track", "LanguageId", "Lemma", "LemmaNormalized", "Transliteration", "Pos", "IsProperName", "IsMorpheme", "Occurrences", "UpdatedByUserId")
        VALUES (@Track, @LanguageId, @Lemma, @LemmaNormalized, @Transliteration, @Pos, @IsProperName, @IsMorpheme, @Occurrences, @UserId)
        RETURNING "Id"
        """;

    private const string MoveKeyRow = """
        UPDATE "unshackled-word"."VocabStrongs"
        SET "VocabEntryId" = @TargetId
        WHERE 1=1
            AND "VocabEntryId" = @Id
            AND "StrongsKey" = @Key
        """;

    private const string MoveAllKeys = """
        UPDATE "unshackled-word"."VocabStrongs"
        SET "VocabEntryId" = @TargetId
        WHERE 1=1
            AND "VocabEntryId" = @Id
        """;

    private const string RecomputeOccurrences = """
        UPDATE "unshackled-word"."VocabEntries" e
        SET "Occurrences" = COALESCE((SELECT sum(vs."Occurrences") FROM "unshackled-word"."VocabStrongs" vs WHERE vs."VocabEntryId" = e."Id"), 0)::int
        WHERE 1=1
            AND e."Id" = ANY(@Ids)
        RETURNING e."Id", e."Occurrences"
        """;

    private const string MergeMeanings = """
        INSERT INTO "unshackled-word"."VocabMeanings"
            ("VocabEntryId", "German", "GermanKey", "Rank", "Count", "Share", "IsAccepted", "Origin", "IsPinned")
        SELECT @TargetId
             , a."German"
             , a."GermanKey"
             , @Offset + (row_number() OVER (ORDER BY a."Rank", a."Id"))::int
             , a."Count"
             , a."Share"
             , a."IsAccepted"
             , a."Origin"
             , a."IsPinned"
        FROM "unshackled-word"."VocabMeanings" a
        WHERE 1=1
            AND a."VocabEntryId" = @Id
            AND NOT EXISTS (SELECT 1
                            FROM "unshackled-word"."VocabMeanings" b
                            WHERE b."VocabEntryId" = @TargetId AND b."German" = a."German")
        """;

    private const string MaxMeaningRank = """
        SELECT COALESCE(max(m."Rank"), 0)
        FROM "unshackled-word"."VocabMeanings" m
        WHERE 1=1
            AND m."VocabEntryId" = @Id
        """;

    private const string OrFlags = """
        UPDATE "unshackled-word"."VocabEntries"
        SET "IsProperName" = "IsProperName" OR @IsProperName
        WHERE 1=1
            AND "Id" = @Id
        """;

    // Merge, cards. 1: a learner with a card on both entries gets the source's FSRS values on the target when the source is more stable.
    private const string MergeCardsTakeHigher = """
        UPDATE "unshackled-word"."VocabCards" b
        SET "State" = a."State"
          , "Stability" = a."Stability"
          , "Difficulty" = a."Difficulty"
          , "Due" = a."Due"
          , "Reps" = a."Reps"
          , "Lapses" = a."Lapses"
          , "LastReview" = a."LastReview"
          , "CreatedAt" = LEAST(a."CreatedAt", b."CreatedAt")
        FROM "unshackled-word"."VocabCards" a
        WHERE 1=1
            AND a."VocabEntryId" = @Id
            AND b."VocabEntryId" = @TargetId
            AND a."UserId" = b."UserId"
            AND a."Stability" > b."Stability"
        """;

    // 2: the source card of such a learner is dropped
    private const string MergeCardsDropDuplicate = """
        DELETE FROM "unshackled-word"."VocabCards" a
        USING "unshackled-word"."VocabCards" b
        WHERE 1=1
            AND a."VocabEntryId" = @Id
            AND b."VocabEntryId" = @TargetId
            AND a."UserId" = b."UserId"
        """;

    // 3: the remaining source cards move to the target (flagged: seen version below the merge version)
    private const string MergeCardsMove = """
        UPDATE "unshackled-word"."VocabCards"
        SET "VocabEntryId" = @TargetId
          , "SeenContentVersion" = @Seen
        WHERE 1=1
            AND "VocabEntryId" = @Id
        """;

    private const string DeleteEntry = """
        DELETE FROM "unshackled-word"."VocabEntries"
        WHERE 1=1
            AND "Id" = @Id
        """;

    private const string RetireEntry = """
        UPDATE "unshackled-word"."VocabEntries"
        SET "RetiredAt" = now()
          , "FrequencyRank" = NULL
          , "ContentVersion" = "ContentVersion" + 1
          , "UpdatedAt" = now()
          , "UpdatedByUserId" = @UserId
        WHERE 1=1
            AND "Id" = @Id
        RETURNING "ContentVersion"
        """;

    private const string CountLearners = """
        SELECT count(*)::int
        FROM "unshackled-word"."VocabCards" c
        WHERE 1=1
            AND c."VocabEntryId" = @Id
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public VocabEntryEditor(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public Task<VocabEditOutcome> UpdateEntryAsync(int id, string? dictionaryForm, string? transliteration, bool reviewed, int? userId, CancellationToken token) =>
        RunAsync((c, t) => UpdateEntryAsync(c, t, id, dictionaryForm, transliteration, reviewed, userId), token);

    public Task<VocabEditOutcome> AddMeaningAsync(int id, string german, int? userId, CancellationToken token) =>
        RunAsync((c, t) => AddMeaningAsync(c, t, id, german, userId), token);

    public Task<VocabEditOutcome> UpdateMeaningAsync(int id, int meaningId, bool? isAccepted, bool? isPinned, int? userId, CancellationToken token) =>
        RunAsync((c, t) => UpdateMeaningAsync(c, t, id, meaningId, isAccepted, isPinned, userId), token);

    public Task<VocabEditOutcome> DeleteMeaningAsync(int id, int meaningId, int? userId, CancellationToken token) =>
        RunAsync((c, t) => DeleteMeaningAsync(c, t, id, meaningId, userId), token);

    public Task<VocabEditOutcome> ReorderMeaningsAsync(int id, IReadOnlyList<int> meaningIds, int? userId, CancellationToken token) =>
        RunAsync((c, t) => ReorderMeaningsAsync(c, t, id, meaningIds, userId), token);

    public Task<VocabEditOutcome> MoveKeyAsync(int id, string key, int? targetEntryId, int? userId, CancellationToken token) =>
        RunAsync((c, t) => MoveKeyAsync(c, t, id, key, targetEntryId, userId), token);

    public Task<VocabEditOutcome> MergeAsync(int id, int targetId, int? userId, CancellationToken token) =>
        RunAsync((c, t) => MergeAsync(c, t, id, targetId, userId), token);

    public Task<VocabEditOutcome> RetireAsync(int id, int? userId, CancellationToken token) =>
        RunAsync((c, t) => RetireAsync(c, t, id, userId), token);

    public Task<VocabEditOutcome> DeleteAsync(int id, int? userId, CancellationToken token) =>
        RunAsync((c, t) => DeleteAsync(c, t, id, userId), token);

    private async Task<VocabEditOutcome> RunAsync(Func<IDbConnection, IDbTransaction, Task<VocabEditOutcome>> operation, CancellationToken token)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();

        var outcome = await operation(connection, transaction);

        if (outcome.Status == VocabEditStatus.Ok)
        {
            transaction.Commit();
        }
        else
        {
            transaction.Rollback();
        }

        return outcome;
    }

    // ---- entry fields -------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Form, transliteration and the reviewed mark. A changed form is the only learning-relevant part: one bump, one <c>form</c> row.
    /// The reviewed mark keeps its first time and reviewer while it stays on.
    /// </summary>
    public static async Task<VocabEditOutcome> UpdateEntryAsync(IDbConnection c, IDbTransaction t, int id, string? dictionaryForm, string? transliteration, bool reviewed, int? userId)
    {
        var entry = await LockAsync(c, t, id);

        if (entry is null)
        {
            return NoEntry(id);
        }

        var plan = VocabChangeRules.PlanForm(entry.DictionaryForm, entry.DictionaryFormOrigin, dictionaryForm);
        var translit = string.IsNullOrWhiteSpace(transliteration) ? null : transliteration.Trim();
        var reviewedAt = reviewed ? entry.ReviewedAt ?? DateTime.UtcNow : (DateTime?)null;
        var reviewedBy = reviewed ? (entry.ReviewedAt is null ? userId : await GetReviewerAsync(c, t, id)) : null;

        var version = await c.QuerySingleAsync<int>(UpdateEntry, new
        {
            Id = id,
            plan.DictionaryForm,
            DictionaryFormOrigin = plan.Origin,
            Transliteration = translit,
            ReviewedAt = reviewedAt,
            ReviewedByUserId = reviewedBy,
            Bump = plan.Changed ? 1 : 0,
            UserId = userId
        }, t);

        if (plan.Changed)
        {
            await WriteChangeAsync(c, t, id, version, VocabChangeKinds.Form, new { form = entry.DictionaryForm }, new { form = plan.DictionaryForm }, null, userId);
        }

        return VocabEditOutcome.Ok(new VocabEditResponse { EntryId = id, ContentVersion = version, Bumped = plan.Changed, Learners = await CountLearnersAsync(c, t, id) });
    }

    /// <summary>
    /// The agent run writes a form (roadmap 0022.13). Same path as the editor: a changed text bumps once and writes a <c>form</c> row, the
    /// origin becomes <c>agent</c>. A form that is not missing or <c>generated</c> is refused (conflict), the entry stays untouched.
    /// </summary>
    public static async Task<VocabEditOutcome> ApplyAgentFormAsync(IDbConnection c, IDbTransaction t, int id, string form, int? aiUserId)
    {
        var entry = await LockAsync(c, t, id);

        if (entry is null)
        {
            return NoEntry(id);
        }

        if (entry.RetiredAt is not null)
        {
            return VocabEditOutcome.Conflict($"Entry {id} is retired.");
        }

        var plan = VocabChangeRules.PlanAgentForm(entry.DictionaryForm, entry.DictionaryFormOrigin, form);

        return plan is null
            ? VocabEditOutcome.Conflict($"Entry {id} has a {entry.DictionaryFormOrigin} form; an agent only fills missing or generated forms.")
            : await WriteFormAsync(c, t, entry, plan, aiUserId);
    }

    /// <summary>The maintainer accepts the stored form as written: origin <c>manual</c>, no version bump, no change row.</summary>
    public static async Task<VocabEditOutcome> AcceptFormAsync(IDbConnection c, IDbTransaction t, int id, int? userId)
    {
        var entry = await LockAsync(c, t, id);

        if (entry is null)
        {
            return NoEntry(id);
        }

        var plan = VocabChangeRules.PlanAcceptForm(entry.DictionaryForm);

        return plan is null
            ? VocabEditOutcome.Conflict($"Entry {id} has no dictionary form to accept.")
            : await WriteFormAsync(c, t, entry, plan, userId);
    }

    public Task<VocabEditOutcome> AcceptFormAsync(int id, int? userId, CancellationToken token) =>
        RunAsync((c, t) => AcceptFormAsync(c, t, id, userId), token);

    public Task<VocabEditOutcome> ApplyAgentFormAsync(int id, string form, int? aiUserId, CancellationToken token) =>
        RunAsync((c, t) => ApplyAgentFormAsync(c, t, id, form, aiUserId), token);

    private static async Task<VocabEditOutcome> WriteFormAsync(IDbConnection c, IDbTransaction t, EntryRow entry, VocabFormPlan plan, int? userId)
    {
        var version = await c.QuerySingleAsync<int>(UpdateEntry, new
        {
            entry.Id,
            plan.DictionaryForm,
            DictionaryFormOrigin = plan.Origin,
            entry.Transliteration,
            entry.ReviewedAt,
            ReviewedByUserId = entry.ReviewedAt is null ? null : await GetReviewerAsync(c, t, entry.Id),
            Bump = plan.Changed ? 1 : 0,
            UserId = userId
        }, t);

        if (plan.Changed)
        {
            await WriteChangeAsync(c, t, entry.Id, version, VocabChangeKinds.Form, new { form = entry.DictionaryForm }, new { form = plan.DictionaryForm }, null, userId);
        }

        return VocabEditOutcome.Ok(new VocabEditResponse { EntryId = entry.Id, ContentVersion = version, Bumped = plan.Changed, Learners = await CountLearnersAsync(c, t, entry.Id) });
    }

    // ---- meanings -----------------------------------------------------------------------------------------------------------------

    /// <summary>Adds a manual, accepted meaning at the end. The accepted set grows, so this bumps.</summary>
    public static async Task<VocabEditOutcome> AddMeaningAsync(IDbConnection c, IDbTransaction t, int id, string german, int? userId)
    {
        var entry = await LockAsync(c, t, id);

        if (entry is null)
        {
            return NoEntry(id);
        }

        var text = german.Trim();
        var before = await AcceptedAsync(c, t, id);

        if ((await MeaningsAsync(c, t, id)).Any(m => string.Equals(m.German, text, StringComparison.Ordinal)))
        {
            return VocabEditOutcome.Conflict($"The entry already has the meaning '{text}'.");
        }

        var meaningId = await c.QuerySingleAsync<int>(InsertManualMeaning, new { Id = id, German = text }, t);
        var after = await AcceptedAsync(c, t, id);
        var outcome = await FinishMeaningEditAsync(c, t, id, before, after, userId);
        outcome.Response!.MeaningId = meaningId;

        return outcome;
    }

    /// <summary>
    /// Accepts, unaccepts or pins a meaning. Unaccepting a derived meaning pins it as well, otherwise the next derivation would accept it
    /// again. The last accepted meaning cannot be unaccepted. Only a change of the accepted set bumps; pinning does not.
    /// </summary>
    public static async Task<VocabEditOutcome> UpdateMeaningAsync(IDbConnection c, IDbTransaction t, int id, int meaningId, bool? isAccepted, bool? isPinned, int? userId)
    {
        var entry = await LockAsync(c, t, id);

        if (entry is null)
        {
            return NoEntry(id);
        }

        var meanings = await MeaningsAsync(c, t, id);
        var meaning = meanings.FirstOrDefault(m => m.Id == meaningId);

        if (meaning is null)
        {
            return VocabEditOutcome.NotFound($"Meaning {meaningId} does not belong to vocab entry {id}.");
        }

        var accepted = isAccepted ?? meaning.IsAccepted;
        var pinned = isPinned ?? meaning.IsPinned;

        if (isAccepted == false && meaning.IsAccepted && isPinned is null && meaning.Origin == VocabMeaningSelector.OriginDerived)
        {
            pinned = true;
        }

        if (!accepted && meaning.IsAccepted && meanings.Count(m => m.IsAccepted) == 1)
        {
            return VocabEditOutcome.Conflict("The last accepted meaning cannot be unaccepted; add the replacement first.");
        }

        var before = await AcceptedAsync(c, t, id);
        await c.ExecuteAsync(UpdateMeaningFlags, new { MeaningId = meaningId, IsAccepted = accepted, IsPinned = pinned }, t);
        var after = await AcceptedAsync(c, t, id);

        return await FinishMeaningEditAsync(c, t, id, before, after, userId);
    }

    /// <summary>Deletes a manual meaning and closes the rank gap. A derived meaning can only be unaccepted.</summary>
    public static async Task<VocabEditOutcome> DeleteMeaningAsync(IDbConnection c, IDbTransaction t, int id, int meaningId, int? userId)
    {
        var entry = await LockAsync(c, t, id);

        if (entry is null)
        {
            return NoEntry(id);
        }

        var meanings = await MeaningsAsync(c, t, id);
        var meaning = meanings.FirstOrDefault(m => m.Id == meaningId);

        if (meaning is null)
        {
            return VocabEditOutcome.NotFound($"Meaning {meaningId} does not belong to vocab entry {id}.");
        }

        if (meaning.Origin != VocabMeaningSelector.OriginManual)
        {
            return VocabEditOutcome.Conflict("Only manual meanings can be deleted; a derived meaning can be unaccepted.");
        }

        if (meaning.IsAccepted && meanings.Count(m => m.IsAccepted) == 1)
        {
            return VocabEditOutcome.Conflict("The last accepted meaning cannot be deleted; add the replacement first.");
        }

        var before = await AcceptedAsync(c, t, id);
        await c.ExecuteAsync(DeleteMeaning, new { MeaningId = meaningId }, t);
        await c.ExecuteAsync(RenumberMeanings, new { EntryId = id }, t);
        var after = await AcceptedAsync(c, t, id);

        return await FinishMeaningEditAsync(c, t, id, before, after, userId);
    }

    /// <summary>Renumbers the meanings 1..n in the given order. Order is not content: it bumps nothing.</summary>
    public static async Task<VocabEditOutcome> ReorderMeaningsAsync(IDbConnection c, IDbTransaction t, int id, IReadOnlyList<int> meaningIds, int? userId)
    {
        var entry = await LockAsync(c, t, id);

        if (entry is null)
        {
            return NoEntry(id);
        }

        var meanings = await MeaningsAsync(c, t, id);
        var ranks = VocabChangeRules.PlanMeaningOrder(meanings.Select(m => m.Id).ToArray(), meaningIds);

        if (ranks is null)
        {
            return VocabEditOutcome.Invalid("meaningIds must list every meaning of the entry exactly once.");
        }

        await c.ExecuteAsync(SetMeaningRanks, new { EntryId = id, Ids = ranks.Keys.ToArray(), Ranks = ranks.Values.ToArray() }, t);

        return await FinishMeaningEditAsync(c, t, id, await AcceptedAsync(c, t, id), await AcceptedAsync(c, t, id), userId);
    }

    private static async Task<VocabEditOutcome> FinishMeaningEditAsync(IDbConnection c, IDbTransaction t, int id, List<string> before, List<string> after, int? userId)
    {
        var bumps = VocabChangeRules.AcceptedSetChanged(before, after);
        int version;

        if (bumps)
        {
            version = await BumpAsync(c, t, id, userId);
            await WriteChangeAsync(c, t, id, version, VocabChangeKinds.Meanings, new { meanings = before }, new { meanings = after }, null, userId);
        }
        else
        {
            version = await TouchAsync(c, t, id, userId);
        }

        return VocabEditOutcome.Ok(new VocabEditResponse { EntryId = id, ContentVersion = version, Bumped = bumps, Learners = await CountLearnersAsync(c, t, id) });
    }

    // ---- keys ---------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Moves one Strong's key to an existing entry of the same track or, without a target, into a new entry that takes the lemma from the
    /// lexicon. The source gets a <c>split</c> row (new entry) or a <c>keys</c> row (existing entry, which gets one as well); the version
    /// of each touched entry rises once. A new entry starts at version 1 without cards and meanings (run the derivation for it). Occurrences
    /// of both entries are summed again and the ranks of the track are recomputed.
    /// </summary>
    public static async Task<VocabEditOutcome> MoveKeyAsync(IDbConnection c, IDbTransaction t, int id, string key, int? targetEntryId, int? userId)
    {
        if (targetEntryId == id)
        {
            return VocabEditOutcome.Invalid("The target entry is the source entry.");
        }

        var (source, target) = await LockPairAsync(c, t, id, targetEntryId);

        if (source is null)
        {
            return NoEntry(id);
        }

        if (targetEntryId is not null)
        {
            if (target is null)
            {
                return NoEntry(targetEntryId.Value);
            }

            if (target.Track != source.Track)
            {
                return VocabEditOutcome.Conflict("A key can only move to an entry of the same track.");
            }

            if (target.RetiredAt is not null)
            {
                return VocabEditOutcome.Conflict("The target entry is retired.");
            }
        }

        var keyOccurrences = await c.QuerySingleOrDefaultAsync<int?>(SelectKeyOccurrences, new { Id = id, Key = key }, t);

        if (keyOccurrences is null || !VocabChangeRules.TryParseKey(key, out var lang, out var number))
        {
            return VocabEditOutcome.NotFound($"Vocab entry {id} has no Strong's key '{key}'.");
        }

        var sourceKeysBefore = await KeysAsync(c, t, id);

        if (targetEntryId is null && sourceKeysBefore.Count == 1)
        {
            return VocabEditOutcome.Conflict("The key is the only key of the entry; moving it into a new entry would only copy the entry.");
        }

        var targetId = targetEntryId;
        var targetKeysBefore = targetId is null ? [] : await KeysAsync(c, t, targetId.Value);

        if (targetId is null)
        {
            var lexicon = await c.QueryFirstOrDefaultAsync<LexiconRow>(SelectKeyLexicon, new { Lang = lang.ToString(), Number = number, Key = key }, t);
            var lemma = Truncate(lexicon?.Lemma ?? source.Lemma, MaxLemmaLength);
            var normalized = Truncate(VocabText.Normalize(lemma), MaxLemmaLength);

            if (normalized.Length == 0)
            {
                lemma = source.Lemma;
                normalized = Truncate(VocabText.Normalize(lemma), MaxLemmaLength);
            }

            targetId = await c.QuerySingleAsync<int>(InsertNewEntry, new
            {
                source.Track,
                LanguageId = lexicon?.LanguageId ?? source.LanguageId,
                Lemma = lemma,
                LemmaNormalized = normalized,
                Transliteration = lexicon is null ? null : NullIfEmpty(lexicon.Transliteration),
                Pos = lexicon is null ? source.Pos : NullIfEmpty(lexicon.Pos),
                source.IsProperName,
                IsMorpheme = VocabChangeRules.IsMorphemeKey(key),
                Occurrences = keyOccurrences.Value,
                UserId = userId
            }, t);
        }

        await c.ExecuteAsync(MoveKeyRow, new { Id = id, TargetId = targetId, Key = key }, t);

        var occurrences = (await c.QueryAsync<(int Id, int Occurrences)>(RecomputeOccurrences, new { Ids = new[] { id, targetId!.Value } }, t)).ToDictionary(r => r.Id, r => r.Occurrences);
        var sourceKeysAfter = await KeysAsync(c, t, id);

        var sourceVersion = await BumpAsync(c, t, id, userId);
        await WriteChangeAsync(c, t, id, sourceVersion, targetEntryId is null ? VocabChangeKinds.Split : VocabChangeKinds.Keys,
            new { keys = sourceKeysBefore }, new { keys = sourceKeysAfter }, targetId, userId);

        int targetVersion = 1;

        if (targetEntryId is not null)
        {
            targetVersion = await BumpAsync(c, t, targetEntryId.Value, userId);
            await WriteChangeAsync(c, t, targetEntryId.Value, targetVersion, VocabChangeKinds.Keys,
                new { keys = targetKeysBefore }, new { keys = await KeysAsync(c, t, targetEntryId.Value) }, id, userId);
        }

        await RecomputeRanksAsync(c, t);

        return VocabEditOutcome.Ok(new VocabEditResponse
        {
            EntryId = id,
            ContentVersion = sourceVersion,
            Bumped = true,
            TargetEntryId = targetId,
            TargetContentVersion = targetVersion,
            Learners = await CountLearnersAsync(c, t, id),
            Occurrences = occurrences[id],
            TargetOccurrences = occurrences[targetId.Value]
        });
    }

    /// <summary>
    /// Merges entry <paramref name="id"/> into <paramref name="targetId"/>: its keys and the meanings the target does not have (by German)
    /// move over, the target's occurrences are summed again, both get a <c>merge</c> row (each naming the other entry) and the source is
    /// deleted. The target bumps once.
    /// </summary>
    public static async Task<VocabEditOutcome> MergeAsync(IDbConnection c, IDbTransaction t, int id, int targetId, int? userId)
    {
        if (targetId == id)
        {
            return VocabEditOutcome.Invalid("An entry cannot be merged into itself.");
        }

        var (source, target) = await LockPairAsync(c, t, id, targetId);

        if (source is null)
        {
            return NoEntry(id);
        }

        if (target is null)
        {
            return NoEntry(targetId);
        }

        if (target.Track != source.Track)
        {
            return VocabEditOutcome.Conflict("Entries can only be merged within the same track.");
        }

        if (target.RetiredAt is not null)
        {
            return VocabEditOutcome.Conflict("The target entry is retired.");
        }

        var learners = await CountLearnersAsync(c, t, id);
        var sourceKeys = await KeysAsync(c, t, id);
        var sourceMeanings = await AcceptedAsync(c, t, id);
        var targetKeysBefore = await KeysAsync(c, t, targetId);
        var targetMeaningsBefore = await AcceptedAsync(c, t, targetId);

        var offset = await c.QuerySingleAsync<int>(MaxMeaningRank, new { Id = targetId }, t);
        await c.ExecuteAsync(MergeMeanings, new { Id = id, TargetId = targetId, Offset = offset }, t);
        await c.ExecuteAsync(RenumberMeanings, new { EntryId = targetId }, t);
        await c.ExecuteAsync(MoveAllKeys, new { Id = id, TargetId = targetId }, t);
        await c.ExecuteAsync(OrFlags, new { Id = targetId, source.IsProperName }, t);
        var occurrences = (await c.QueryAsync<(int Id, int Occurrences)>(RecomputeOccurrences, new { Ids = new[] { targetId } }, t)).Single().Occurrences;

        var targetVersion = await BumpAsync(c, t, targetId, userId);
        await WriteChangeAsync(c, t, targetId, targetVersion, VocabChangeKinds.Merge,
            new { keys = targetKeysBefore, meanings = targetMeaningsBefore },
            new { keys = await KeysAsync(c, t, targetId), meanings = await AcceptedAsync(c, t, targetId), mergedFrom = id, mergedFromLemma = source.Lemma, mergedFromForm = source.DictionaryForm }, id, userId);

        var sourceVersion = source.ContentVersion + 1;
        await WriteChangeAsync(c, t, id, sourceVersion, VocabChangeKinds.Merge,
            new { keys = sourceKeys, meanings = sourceMeanings }, new { mergedInto = targetId }, targetId, userId);

        // cards of the source move to the target; a learner who had both keeps the card with the higher stability (the review log stays).
        // The moved card is flagged by the merge row: its seen version is one below the version the merge produced.
        await c.ExecuteAsync(MergeCardsTakeHigher, new { Id = id, TargetId = targetId }, t);
        await c.ExecuteAsync(MergeCardsDropDuplicate, new { Id = id, TargetId = targetId }, t);
        await c.ExecuteAsync(MergeCardsMove, new { Id = id, TargetId = targetId, Seen = targetVersion - 1 }, t);
        await c.ExecuteAsync(DeleteEntry, new { Id = id }, t);
        await RecomputeRanksAsync(c, t);

        return VocabEditOutcome.Ok(new VocabEditResponse
        {
            EntryId = id,
            ContentVersion = sourceVersion,
            Bumped = true,
            TargetEntryId = targetId,
            TargetContentVersion = targetVersion,
            Learners = learners,
            Occurrences = 0,
            TargetOccurrences = occurrences
        });
    }

    // ---- retire / delete ----------------------------------------------------------------------------------------------------------

    /// <summary>Takes the entry out of the courses: <c>RetiredAt</c> set, no rank, ranks of the track recomputed, one <c>retire</c> row.</summary>
    public static async Task<VocabEditOutcome> RetireAsync(IDbConnection c, IDbTransaction t, int id, int? userId)
    {
        var entry = await LockAsync(c, t, id);

        if (entry is null)
        {
            return NoEntry(id);
        }

        if (entry.RetiredAt is not null)
        {
            return VocabEditOutcome.Conflict("The entry is already retired.");
        }

        var version = await c.QuerySingleAsync<int>(RetireEntry, new { Id = id, UserId = userId }, t);
        await WriteChangeAsync(c, t, id, version, VocabChangeKinds.Retire, new { retired = false }, new { retired = true }, null, userId);
        await RecomputeRanksAsync(c, t);

        return VocabEditOutcome.Ok(new VocabEditResponse { EntryId = id, ContentVersion = version, Bumped = true, Learners = await CountLearnersAsync(c, t, id) });
    }

    /// <summary>Deletes an entry that has no keys left (409 otherwise). Its meanings go with it; a <c>delete</c> row stays.</summary>
    public static async Task<VocabEditOutcome> DeleteAsync(IDbConnection c, IDbTransaction t, int id, int? userId)
    {
        var entry = await LockAsync(c, t, id);

        if (entry is null)
        {
            return NoEntry(id);
        }

        var keys = await KeysAsync(c, t, id);

        if (keys.Count > 0)
        {
            return VocabEditOutcome.Conflict($"The entry still has {keys.Count} Strong's key(s); move them to another entry or merge the entry first.");
        }

        var learners = await CountLearnersAsync(c, t, id);
        var version = entry.ContentVersion + 1;
        await WriteChangeAsync(c, t, id, version, VocabChangeKinds.Delete, new { lemma = entry.Lemma, meanings = await AcceptedAsync(c, t, id) }, null, null, userId);
        await c.ExecuteAsync(DeleteEntry, new { Id = id }, t);
        await RecomputeRanksAsync(c, t);

        return VocabEditOutcome.Ok(new VocabEditResponse { EntryId = id, ContentVersion = version, Bumped = true, Learners = learners });
    }

    // ---- helpers ------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Recomputes the frequency ranks of both tracks with the generator's own SQL (occurrences desc, lowest Strong's number, id). The
    /// unique constraint is deferred, so the renumbering is one statement per track pass. A rank is a position, not content: no bump.
    /// </summary>
    public static async Task RecomputeRanksAsync(IDbConnection c, IDbTransaction t)
    {
        await c.ExecuteAsync(VocabLexiconGeneratorSql.UpdateRanks, transaction: t, commandTimeout: TimeoutSeconds);
        await c.ExecuteAsync(VocabLexiconGeneratorSql.ClearStaleRanks, transaction: t, commandTimeout: TimeoutSeconds);
    }

    private static VocabEditOutcome NoEntry(int id) => VocabEditOutcome.NotFound($"No vocab entry {id}.");

    private static async Task<EntryRow?> LockAsync(IDbConnection c, IDbTransaction t, int id) =>
        await c.QuerySingleOrDefaultAsync<EntryRow>(SelectEntryForUpdate, new { Id = id }, t);

    /// <summary>Locks both rows in id order, so two concurrent moves in opposite directions cannot deadlock.</summary>
    private static async Task<(EntryRow? Source, EntryRow? Target)> LockPairAsync(IDbConnection c, IDbTransaction t, int id, int? targetId)
    {
        if (targetId is null)
        {
            return (await LockAsync(c, t, id), null);
        }

        if (id < targetId)
        {
            var source = await LockAsync(c, t, id);

            return (source, await LockAsync(c, t, targetId.Value));
        }

        var target = await LockAsync(c, t, targetId.Value);

        return (await LockAsync(c, t, id), target);
    }

    private static async Task<int?> GetReviewerAsync(IDbConnection c, IDbTransaction t, int id) =>
        await c.QuerySingleOrDefaultAsync<int?>("""SELECT "ReviewedByUserId" FROM "unshackled-word"."VocabEntries" WHERE "Id" = @Id""", new { Id = id }, t);

    private static async Task<List<string>> AcceptedAsync(IDbConnection c, IDbTransaction t, int id) =>
        (await c.QueryAsync<string>(SelectAcceptedGerman, new { Id = id }, t)).ToList();

    private static async Task<List<MeaningRow>> MeaningsAsync(IDbConnection c, IDbTransaction t, int id) =>
        (await c.QueryAsync<MeaningRow>(SelectMeaningRows, new { Id = id }, t)).ToList();

    private static async Task<List<string>> KeysAsync(IDbConnection c, IDbTransaction t, int id) =>
        (await c.QueryAsync<string>(SelectKeyNames, new { Id = id }, t)).ToList();

    private static async Task<int> CountLearnersAsync(IDbConnection c, IDbTransaction t, int id) =>
        await c.QuerySingleAsync<int>(CountLearners, new { Id = id }, t);

    private static async Task<int> BumpAsync(IDbConnection c, IDbTransaction t, int id, int? userId) =>
        await c.QuerySingleAsync<int>(BumpVersion, new { Id = id, UserId = userId }, t);

    private static async Task<int> TouchAsync(IDbConnection c, IDbTransaction t, int id, int? userId)
    {
        await c.ExecuteAsync(TouchEntry, new { Id = id, UserId = userId }, t);

        return await c.QuerySingleAsync<int>("""SELECT "ContentVersion" FROM "unshackled-word"."VocabEntries" WHERE "Id" = @Id""", new { Id = id }, t);
    }

    private static async Task WriteChangeAsync(IDbConnection c, IDbTransaction t, int entryId, int version, string kind, object? before, object? after, int? targetEntryId, int? userId)
    {
        await c.ExecuteAsync(InsertChange, new
        {
            EntryId = entryId,
            ContentVersion = version,
            Kind = kind,
            Before = before is null ? null : JsonSerializer.Serialize(before),
            After = after is null ? null : JsonSerializer.Serialize(after),
            TargetEntryId = targetEntryId,
            UserId = userId
        }, t);
    }

    private static string Truncate(string text, int length) => text.Length <= length ? text : text[..length];

    private static string? NullIfEmpty(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;

    private sealed class EntryRow
    {
        public int Id { get; init; }
        public string Track { get; init; } = string.Empty;
        public int LanguageId { get; init; }
        public string Lemma { get; init; } = string.Empty;
        public string? Pos { get; init; }
        public string? DictionaryForm { get; init; }
        public string? DictionaryFormOrigin { get; init; }
        public string? Transliteration { get; init; }
        public bool IsProperName { get; init; }
        public int Occurrences { get; init; }
        public DateTime? ReviewedAt { get; init; }
        public DateTime? RetiredAt { get; init; }
        public int ContentVersion { get; init; }
    }

    private sealed class MeaningRow
    {
        public int Id { get; init; }
        public string German { get; init; } = string.Empty;
        public int Rank { get; init; }
        public bool IsAccepted { get; init; }
        public string Origin { get; init; } = string.Empty;
        public bool IsPinned { get; init; }
    }

    private sealed class LexiconRow
    {
        public string Lemma { get; init; } = string.Empty;
        public string? Transliteration { get; init; }
        public int LanguageId { get; init; }
        public string? Pos { get; init; }
    }
}
