using System.Data;
using System.Text.Json;
using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.TskCuration;

namespace UnshackledWord.Infrastructure.TskCuration;

/// <summary>
/// Dapper access to <c>TskCurations</c> and <c>TskCurationAudit</c> (roadmap 0024.3, migration 0045). The instance methods open a connection;
/// the static ones run on a given connection and transaction so the database tests can roll back.
/// </summary>
public sealed class TskCurationRepository : ITskCurationRepository
{
    /// <summary>First key of the advisory lock that serialises writes per source verse (second key: <c>SourceHebRefId</c>).</summary>
    private const int LockClass = 240_003;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IDbConnectionFactory _factory;

    public TskCurationRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<bool> TskLinkExistsAsync(TskLinkKey key, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);

        return await TskLinkExistsAsync(connection, null, key, token);
    }

    public async Task<TskCurationRecord?> GetAsync(TskLinkKey key, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);

        return await GetAsync(connection, null, key, token);
    }

    public async Task<TskCurationRecord?> ApplyAsync(TskLinkKey key, Func<TskCurationState, TskCurationState> change, int? userId, string source, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();

        var result = await ApplyAsync(connection, transaction, key, change, userId, source, token);

        transaction.Commit();
        return result;
    }

    public static async Task<bool> TskLinkExistsAsync(IDbConnection connection, IDbTransaction? transaction, TskLinkKey key, CancellationToken token = default)
    {
        const string sql = """
                           SELECT EXISTS (
                               SELECT 1
                               FROM "unshackled-word"."Tsk" t
                               WHERE 1=1
                                   AND t."SourceHebRefId" = @SourceHebRefId
                                   AND t."TargetStartHebRefId" = @TargetStartHebRefId
                                   AND t."TargetEndHebRefId" IS NOT DISTINCT FROM @TargetEndHebRefId
                           )
                           """;

        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(sql, key, transaction, cancellationToken: token));
    }

    public static async Task<TskCurationRecord?> GetAsync(IDbConnection connection, IDbTransaction? transaction, TskLinkKey key, CancellationToken token = default,
        bool forUpdate = false)
    {
        var sql = $"""
                   SELECT c."SourceHebRefId"
                        , c."TargetStartHebRefId"
                        , c."TargetEndHebRefId"
                        , c."Origin"
                        , c."Rating"
                        , c."Comment"
                        , c."StartPosition"
                        , c."EndPosition"
                        , c."QuotedText"
                        , c."UpdatedAt"
                        , c."UpdatedByUserId"
                   FROM "unshackled-word"."TskCurations" c
                   WHERE 1=1
                       AND c."SourceHebRefId" = @SourceHebRefId
                       AND c."TargetStartHebRefId" = @TargetStartHebRefId
                       AND COALESCE(c."TargetEndHebRefId", 0) = COALESCE(@TargetEndHebRefId::int4, 0)
                   {(forUpdate ? "FOR UPDATE" : string.Empty)}
                   """;

        var row = await connection.QuerySingleOrDefaultAsync<CurationRow>(new CommandDefinition(sql, key, transaction, cancellationToken: token));
        return row?.ToRecord();
    }

    public static async Task<TskCurationRecord?> ApplyAsync(IDbConnection connection, IDbTransaction transaction, TskLinkKey key,
        Func<TskCurationState, TskCurationState> change, int? userId, string source, CancellationToken token = default)
    {
        // Two writers of one link would both read "no row" and the second insert would fail on the unique key: serialise per source verse.
        await connection.ExecuteAsync(new CommandDefinition("SELECT pg_advisory_xact_lock(@LockClass, @SourceHebRefId)",
            new { LockClass, key.SourceHebRefId }, transaction, cancellationToken: token));

        var before = await GetAsync(connection, transaction, key, token, forUpdate: true);
        var beforeState = before?.State;
        var afterState = change(beforeState ?? TskCurationState.Plain);
        if (afterState.IsDefault)
        {
            afterState = null;
        }

        if (afterState == beforeState)
        {
            return before;
        }

        if (afterState is null)
        {
            const string delete = """
                                  DELETE FROM "unshackled-word"."TskCurations" c
                                  WHERE 1=1
                                      AND c."SourceHebRefId" = @SourceHebRefId
                                      AND c."TargetStartHebRefId" = @TargetStartHebRefId
                                      AND COALESCE(c."TargetEndHebRefId", 0) = COALESCE(@TargetEndHebRefId::int4, 0)
                                  """;
            await connection.ExecuteAsync(new CommandDefinition(delete, key, transaction, cancellationToken: token));
        }
        else
        {
            const string upsert = """
                                  INSERT INTO "unshackled-word"."TskCurations"
                                      ("SourceHebRefId", "TargetStartHebRefId", "TargetEndHebRefId", "Origin", "Rating", "Comment", "StartPosition", "EndPosition", "QuotedText", "UpdatedAt", "UpdatedByUserId")
                                  VALUES (@SourceHebRefId, @TargetStartHebRefId, @TargetEndHebRefId, @Origin, @Rating, @Comment, @StartPosition, @EndPosition, @QuotedText, now(), @UserId)
                                  ON CONFLICT ("SourceHebRefId", "TargetStartHebRefId", COALESCE("TargetEndHebRefId", 0)) DO UPDATE
                                      SET "Origin" = EXCLUDED."Origin"
                                        , "Rating" = EXCLUDED."Rating"
                                        , "Comment" = EXCLUDED."Comment"
                                        , "StartPosition" = EXCLUDED."StartPosition"
                                        , "EndPosition" = EXCLUDED."EndPosition"
                                        , "QuotedText" = EXCLUDED."QuotedText"
                                        , "UpdatedAt" = EXCLUDED."UpdatedAt"
                                        , "UpdatedByUserId" = EXCLUDED."UpdatedByUserId"
                                  """;
            await connection.ExecuteAsync(new CommandDefinition(upsert, new
            {
                key.SourceHebRefId, key.TargetStartHebRefId, key.TargetEndHebRefId, afterState.Origin, afterState.Rating, afterState.Comment,
                afterState.StartPosition, afterState.EndPosition, afterState.QuotedText, UserId = userId,
            }, transaction, cancellationToken: token));
        }

        const string audit = """
                             INSERT INTO "unshackled-word"."TskCurationAudit"
                                 ("SourceHebRefId", "TargetStartHebRefId", "TargetEndHebRefId", "Before", "After", "UserId", "At", "Source")
                             VALUES (@SourceHebRefId, @TargetStartHebRefId, @TargetEndHebRefId, CAST(@Before AS jsonb), CAST(@After AS jsonb), @UserId, now(), @Source)
                             """;
        await connection.ExecuteAsync(new CommandDefinition(audit, new
        {
            key.SourceHebRefId, key.TargetStartHebRefId, key.TargetEndHebRefId, Before = ToJson(beforeState), After = ToJson(afterState), UserId = userId,
            Source = source,
        }, transaction, cancellationToken: token));

        return afterState is null ? null : await GetAsync(connection, transaction, key, token);
    }

    /// <summary>The audit shape: <c>{"origin", "rating", "comment", "startPosition", "endPosition", "quotedText"}</c>, null for "no row".</summary>
    public static string? ToJson(TskCurationState? state) => state is null ? null : JsonSerializer.Serialize(state, Json);

    private sealed class CurationRow
    {
        public int SourceHebRefId { get; init; }
        public int TargetStartHebRefId { get; init; }
        public int? TargetEndHebRefId { get; init; }
        public string Origin { get; init; } = default!;
        public string? Rating { get; init; }
        public string? Comment { get; init; }
        public int? StartPosition { get; init; }
        public int? EndPosition { get; init; }
        public string? QuotedText { get; init; }
        public DateTime UpdatedAt { get; init; }
        public int? UpdatedByUserId { get; init; }

        public TskCurationRecord ToRecord() => new(
            new TskLinkKey(SourceHebRefId, TargetStartHebRefId, TargetEndHebRefId),
            new TskCurationState(Origin, Rating, Comment, StartPosition, EndPosition, QuotedText),
            new DateTimeOffset(DateTime.SpecifyKind(UpdatedAt, DateTimeKind.Utc)),
            UpdatedByUserId);
    }
}
