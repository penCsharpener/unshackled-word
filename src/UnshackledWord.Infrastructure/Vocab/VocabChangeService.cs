using System.Data;
using System.Text.Json;
using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.WebApi.Vocab;

namespace UnshackledWord.Infrastructure.Vocab;

/// <summary>
/// Lexicon changes under active cards (roadmap 0022.8b). A card is flagged when its seen version is below the entry's content version, or when
/// its entry is retired or gone. Keep sets the seen version, reset deletes the card (the review log stays). Cards in state <c>new</c> (introduced,
/// never quizzed) are not listed: nothing was learned yet.
/// </summary>
public sealed class VocabChangeService
{
    private sealed class FlaggedRow
    {
        public int EntryId { get; init; }
        public bool Exists { get; init; }
        public bool Retired { get; init; }
        public string? Lemma { get; init; }
        public string? DictionaryForm { get; init; }
        public string? Transliteration { get; init; }
        public string? Track { get; init; }
    }

    private sealed class ChangeRow
    {
        public int EntryId { get; init; }
        public string Kind { get; init; } = string.Empty;
        public string? Before { get; init; }
        public string? After { get; init; }
        public DateTime ChangedAt { get; init; }
        public int? TargetEntryId { get; init; }
        public string? TargetLemma { get; init; }
        public string? TargetDictionaryForm { get; init; }
    }

    private sealed class CardEntryRow
    {
        public int? Id { get; init; }
        public int? ContentVersion { get; init; }
        public bool? Retired { get; init; }
    }

    private const string FlaggedWhere = """
        FROM "unshackled-word"."VocabCards" c
            LEFT JOIN "unshackled-word"."VocabEntries" e ON e."Id" = c."VocabEntryId"
        WHERE 1=1
            AND c."UserId" = @UserId
            AND c."State" <> 'new'
            AND (e."Id" IS NULL OR e."RetiredAt" IS NOT NULL OR c."SeenContentVersion" < e."ContentVersion")
        """;

    private const string SelectFlagged = """
        SELECT c."VocabEntryId" AS "EntryId"
             , (e."Id" IS NOT NULL) AS "Exists"
             , (e."RetiredAt" IS NOT NULL) AS "Retired"
             , e."Lemma"
             , e."DictionaryForm"
             , e."Transliteration"
             , e."Track"::text AS "Track"
        """ + "\n" + FlaggedWhere + """

        ORDER BY c."VocabEntryId"
        """;

    private const string CountFlagged = "SELECT count(*)::int\n" + FlaggedWhere;

    private const string SelectChanges = """
        SELECT ch."VocabEntryId" AS "EntryId"
             , ch."Kind"
             , ch."Before"::text AS "Before"
             , ch."After"::text AS "After"
             , ch."ChangedAt"
             , ch."TargetEntryId"
             , COALESCE(te."Lemma", ch."After"->>'mergedFromLemma') AS "TargetLemma"
             , COALESCE(te."DictionaryForm", ch."After"->>'mergedFromForm') AS "TargetDictionaryForm"
        FROM "unshackled-word"."VocabCards" c
            JOIN "unshackled-word"."VocabEntryChanges" ch ON ch."VocabEntryId" = c."VocabEntryId" AND ch."ContentVersion" > c."SeenContentVersion"
            LEFT JOIN "unshackled-word"."VocabEntries" te ON te."Id" = ch."TargetEntryId"
        WHERE 1=1
            AND c."UserId" = @UserId
            AND c."VocabEntryId" = ANY(@Ids)
            AND ch."Kind" <> 'root' -- root edits do not change what a card asks (roadmap 0030.5)
        ORDER BY ch."VocabEntryId", ch."ContentVersion", ch."Id"
        """;

    // a deleted entry has no lemma any more: the delete row holds it
    private const string SelectDeletedLemmas = """
        SELECT ch."VocabEntryId" AS "EntryId"
             , ch."Before"->>'lemma' AS "Lemma"
        FROM "unshackled-word"."VocabEntryChanges" ch
        WHERE 1=1
            AND ch."VocabEntryId" = ANY(@Ids)
            AND ch."Kind" = 'delete'
        """;

    private const string SelectCardEntry = """
        SELECT e."Id"
             , e."ContentVersion"
             , (e."RetiredAt" IS NOT NULL) AS "Retired"
        FROM "unshackled-word"."VocabCards" c
            LEFT JOIN "unshackled-word"."VocabEntries" e ON e."Id" = c."VocabEntryId"
        WHERE 1=1
            AND c."UserId" = @UserId
            AND c."VocabEntryId" = @EntryId
        FOR UPDATE OF c
        """;

    private const string KeepCard = """
        UPDATE "unshackled-word"."VocabCards"
        SET "SeenContentVersion" = @Version
        WHERE 1=1
            AND "UserId" = @UserId
            AND "VocabEntryId" = @EntryId
        """;

    private const string DeleteCard = """
        DELETE FROM "unshackled-word"."VocabCards"
        WHERE 1=1
            AND "UserId" = @UserId
            AND "VocabEntryId" = @EntryId
        """;

    private const string KeepAll = """
        UPDATE "unshackled-word"."VocabCards" c
        SET "SeenContentVersion" = e."ContentVersion"
        FROM "unshackled-word"."VocabEntries" e
        WHERE 1=1
            AND e."Id" = c."VocabEntryId"
            AND c."UserId" = @UserId
            AND e."RetiredAt" IS NULL
            AND c."SeenContentVersion" < e."ContentVersion"
        """;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IDbConnectionFactory _connectionFactory;

    public VocabChangeService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public Task<VocabSessionOutcome<VocabChangesResponse>> ListAsync(int userId, CancellationToken token) =>
        RunAsync((c, t) => ListAsync(c, t, userId), token);

    public Task<VocabSessionOutcome<VocabChangeActionResponse>> ApplyAsync(int userId, int entryId, string action, CancellationToken token) =>
        RunAsync((c, t) => ApplyAsync(c, t, userId, entryId, action), token);

    public Task<VocabSessionOutcome<VocabKeepAllResponse>> KeepAllAsync(int userId, CancellationToken token) =>
        RunAsync((c, t) => KeepAllAsync(c, t, userId), token);

    /// <summary>Flagged cards of the user (for the "n of your words were revised" notice); 0 without a user.</summary>
    public async Task<int> CountAsync(int userId, CancellationToken token)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();

        return await CountAsync(connection, transaction, userId);
    }

    public static async Task<int> CountAsync(IDbConnection c, IDbTransaction t, int userId) =>
        await c.QuerySingleAsync<int>(CountFlagged, new { UserId = userId }, t);

    /// <summary>Users with a card on the entry and how many of them are flagged on it (cards in state new do not count as learners of the change).</summary>
    public async Task<VocabLearnersResponse> LearnersAsync(int entryId, CancellationToken token)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(token);

        return await connection.QuerySingleAsync<VocabLearnersResponse>("""
            SELECT count(*)::int AS "Learners"
                 , count(*) FILTER (WHERE c."SeenContentVersion" < e."ContentVersion")::int AS "Flagged"
            FROM "unshackled-word"."VocabCards" c
                JOIN "unshackled-word"."VocabEntries" e ON e."Id" = c."VocabEntryId"
            WHERE 1=1
                AND c."VocabEntryId" = @EntryId
                AND c."State" <> 'new'
            """, new { EntryId = entryId });
    }

    public static async Task<VocabSessionOutcome<VocabChangesResponse>> ListAsync(IDbConnection c, IDbTransaction t, int userId)
    {
        var rows = (await c.QueryAsync<FlaggedRow>(SelectFlagged, new { UserId = userId }, t)).ToList();
        var ids = rows.Select(r => r.EntryId).ToArray();
        var changes = ids.Length == 0
            ? new List<ChangeRow>()
            : (await c.QueryAsync<ChangeRow>(SelectChanges, new { UserId = userId, Ids = ids }, t)).ToList();
        var deleted = rows.Where(r => !r.Exists).Select(r => r.EntryId).ToArray();
        var deletedLemmas = deleted.Length == 0
            ? new Dictionary<int, string?>()
            : (await c.QueryAsync<(int EntryId, string? Lemma)>(SelectDeletedLemmas, new { Ids = deleted }, t))
                .GroupBy(x => x.EntryId)
                .ToDictionary(g => g.Key, g => g.First().Lemma);

        var items = rows.Select(r =>
        {
            var lemma = r.Lemma ?? deletedLemmas.GetValueOrDefault(r.EntryId) ?? string.Empty;

            return new VocabChangedEntryResponse
            {
                EntryId = r.EntryId,
                Lemma = lemma,
                DictionaryForm = r.DictionaryForm,
                Transliteration = r.Transliteration,
                Lang = r.Track is not null ? (r.Track == "H" ? "he" : "grc") : (lemma.Any(ch => ch is >= '\u0590' and <= '\u05FF') ? "he" : "grc"),
                Status = r.Exists && !r.Retired ? "changed" : "removed",
                Changes = changes.Where(x => x.EntryId == r.EntryId).Select(x => new VocabChangeResponse
                {
                    Kind = x.Kind,
                    Before = Parse(x.Before),
                    After = Parse(x.After),
                    ChangedAt = new DateTimeOffset(DateTime.SpecifyKind(x.ChangedAt, DateTimeKind.Utc)),
                    TargetEntryId = x.TargetEntryId,
                    TargetLemma = x.TargetLemma,
                    TargetDictionaryForm = x.TargetDictionaryForm,
                }).ToList(),
            };
        }).ToList();

        return VocabSessionOutcome<VocabChangesResponse>.Ok(new VocabChangesResponse { Count = items.Count, Items = items });
    }

    public static async Task<VocabSessionOutcome<VocabChangeActionResponse>> ApplyAsync(IDbConnection c, IDbTransaction t, int userId, int entryId, string action)
    {
        if (action is not (VocabChangeActionRequest.Keep or VocabChangeActionRequest.Reset))
        {
            return VocabSessionOutcome<VocabChangeActionResponse>.Fail(VocabSessionStatus.Invalid, "Action must be keep or reset.");
        }

        var card = await c.QueryFirstOrDefaultAsync<CardEntryRow>(SelectCardEntry, new { UserId = userId, EntryId = entryId }, t);

        if (card is null)
        {
            return VocabSessionOutcome<VocabChangeActionResponse>.Fail(VocabSessionStatus.NotFound, "No card for this word.");
        }

        if (action == VocabChangeActionRequest.Keep)
        {
            if (card.Id is null || card.Retired == true)
            {
                return VocabSessionOutcome<VocabChangeActionResponse>.Fail(VocabSessionStatus.Conflict, "The word was removed from the course; it can only be removed from your list.");
            }

            await c.ExecuteAsync(KeepCard, new { UserId = userId, EntryId = entryId, Version = card.ContentVersion }, t);
        }
        else
        {
            await c.ExecuteAsync(DeleteCard, new { UserId = userId, EntryId = entryId }, t);
        }

        return VocabSessionOutcome<VocabChangeActionResponse>.Ok(new VocabChangeActionResponse
        {
            Action = action,
            EntryId = entryId,
            Remaining = await CountAsync(c, t, userId),
        });
    }

    public static async Task<VocabSessionOutcome<VocabKeepAllResponse>> KeepAllAsync(IDbConnection c, IDbTransaction t, int userId)
    {
        var kept = await c.ExecuteAsync(KeepAll, new { UserId = userId }, t);

        return VocabSessionOutcome<VocabKeepAllResponse>.Ok(new VocabKeepAllResponse { Kept = kept, Remaining = await CountAsync(c, t, userId) });
    }

    private static JsonElement? Parse(string? json) => json is null ? null : JsonSerializer.Deserialize<JsonElement>(json, Json);

    private async Task<VocabSessionOutcome<T>> RunAsync<T>(Func<IDbConnection, IDbTransaction, Task<VocabSessionOutcome<T>>> operation, CancellationToken token)
        where T : class
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();
        var outcome = await operation(connection, transaction);

        if (outcome.Status == VocabSessionStatus.Ok)
        {
            transaction.Commit();
        }
        else
        {
            transaction.Rollback();
        }

        return outcome;
    }
}
