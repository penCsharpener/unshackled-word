using System.Data;
using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.TskCuration;
using UnshackledWord.Domain.WebApi.Tsk;

namespace UnshackledWord.Infrastructure.TskCuration;

/// <summary>
/// Dapper access to <c>TskCurationProposals</c> (roadmap 0024.6, migration 0046). Accepting applies the desired state through
/// <see cref="TskCurationRepository.ApplyAsync(IDbConnection, IDbTransaction, TskLinkKey, Func{TskCurationState, TskCurationState}, int?, string, CancellationToken, int?)"/>
/// in the same transaction. The static methods run on a given connection and transaction so the database tests can roll back.
/// </summary>
public sealed class TskCurationProposalRepository : ITskCurationProposalRepository
{
    private const string Columns = """
                                   p."Id"
                                        , p."SourceHebRefId"
                                        , p."TargetStartHebRefId"
                                        , p."TargetEndHebRefId"
                                        , p."Origin"
                                        , p."Rating"
                                        , p."Comment"
                                        , p."StartPosition"
                                        , p."EndPosition"
                                        , p."QuotedText"
                                        , p."Delete"
                                        , p."BaseUpdatedAt"
                                        , p."UserId"
                                        , p."CreatedAt"
                                        , p."State"
                                        , p."DecidedByUserId"
                                        , p."DecidedAt"
                                        , p."Reason"
                                   """;

    private readonly IDbConnectionFactory _factory;

    public TskCurationProposalRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<TskProposalRecord?> GetAsync(int id, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);

        return await GetAsync(connection, null, id, token);
    }

    public async Task<IReadOnlyList<TskProposalRecord>> GetPendingOfUserAsync(int userId, int fromHebRefId, int toHebRefId, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);

        return await GetPendingOfUserAsync(connection, null, userId, fromHebRefId, toHebRefId, token);
    }

    public async Task<IReadOnlyList<TskProposalRecord?>> ProposeAsync(int userId, IReadOnlyList<TskProposalWrite> writes, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();

        var result = await ProposeAsync(connection, transaction, userId, writes, token);

        transaction.Commit();
        return result;
    }

    public async Task<IReadOnlyList<TskProposalReviewRecord>> GetPendingAsync(int limit, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);

        return await GetPendingAsync(connection, null, limit, token);
    }

    public async Task<IReadOnlyList<TskProposalRecord>> GetOfUserAsync(int userId, int limit, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);

        return await GetOfUserAsync(connection, null, userId, limit, token);
    }

    public async Task<TskProposalDecision> AcceptAsync(int id, int maintainerId, bool force, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();

        var result = await AcceptAsync(connection, transaction, id, maintainerId, force, token);

        transaction.Commit();
        return result;
    }

    public async Task<TskProposalDecision> RejectAsync(int id, int maintainerId, string? reason, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();

        var result = await RejectAsync(connection, transaction, id, maintainerId, reason, token);

        transaction.Commit();
        return result;
    }

    public static async Task<TskProposalRecord?> GetAsync(IDbConnection connection, IDbTransaction? transaction, int id, CancellationToken token = default,
        bool forUpdate = false)
    {
        var sql = $"""
                   SELECT {Columns}
                   FROM "unshackled-word"."TskCurationProposals" p
                   WHERE 1=1
                       AND p."Id" = @Id
                   {(forUpdate ? "FOR UPDATE" : string.Empty)}
                   """;

        var row = await connection.QuerySingleOrDefaultAsync<ProposalRow>(new CommandDefinition(sql, new { Id = id }, transaction, cancellationToken: token));
        return row?.ToRecord();
    }

    public static async Task<IReadOnlyList<TskProposalRecord>> GetPendingOfUserAsync(IDbConnection connection, IDbTransaction? transaction, int userId, int fromHebRefId,
        int toHebRefId, CancellationToken token = default)
    {
        const string sql = $"""
                            SELECT {Columns}
                            FROM "unshackled-word"."TskCurationProposals" p
                            WHERE 1=1
                                AND p."UserId" = @UserId
                                AND p."State" = 'pending'
                                AND p."SourceHebRefId" BETWEEN @From AND @To
                            ORDER BY p."SourceHebRefId", p."CreatedAt", p."Id"
                            """;

        var rows = await connection.QueryAsync<ProposalRow>(new CommandDefinition(sql, new { UserId = userId, From = fromHebRefId, To = toHebRefId }, transaction,
            cancellationToken: token));
        return rows.Select(r => r.ToRecord()).ToList();
    }

    public static async Task<IReadOnlyList<TskProposalRecord?>> ProposeAsync(IDbConnection connection, IDbTransaction transaction, int userId,
        IReadOnlyList<TskProposalWrite> writes, CancellationToken token = default)
    {
        const string supersede = """
                                 UPDATE "unshackled-word"."TskCurationProposals" p
                                 SET "State" = 'superseded'
                                   , "DecidedAt" = now()
                                 WHERE 1=1
                                     AND p."UserId" = @UserId
                                     AND p."State" = 'pending'
                                     AND p."SourceHebRefId" = @SourceHebRefId
                                     AND p."TargetStartHebRefId" = @TargetStartHebRefId
                                     AND COALESCE(p."TargetEndHebRefId", 0) = COALESCE(@TargetEndHebRefId::int4, 0)
                                 """;
        const string insert = """
                              INSERT INTO "unshackled-word"."TskCurationProposals"
                                  ("SourceHebRefId", "TargetStartHebRefId", "TargetEndHebRefId", "Origin", "Rating", "Comment", "StartPosition", "EndPosition", "QuotedText", "Delete", "BaseUpdatedAt", "UserId")
                              VALUES (@SourceHebRefId, @TargetStartHebRefId, @TargetEndHebRefId, @Origin, @Rating, @Comment, @StartPosition, @EndPosition, @QuotedText, @Delete, @BaseUpdatedAt, @UserId)
                              RETURNING "Id"
                              """;

        var result = new List<TskProposalRecord?>(writes.Count);
        foreach (var write in writes)
        {
            var key = write.Key;
            // Same lock as the curation writes: one pending proposal per user and key even with two requests at once.
            await connection.ExecuteAsync(new CommandDefinition("SELECT pg_advisory_xact_lock(@LockClass, @SourceHebRefId)",
                new { TskCurationRepository.LockClass, key.SourceHebRefId }, transaction, cancellationToken: token));
            await connection.ExecuteAsync(new CommandDefinition(supersede,
                new { UserId = userId, key.SourceHebRefId, key.TargetStartHebRefId, key.TargetEndHebRefId }, transaction, cancellationToken: token));

            if (write.Withdraw)
            {
                result.Add(null);
                continue;
            }

            var state = write.State;
            var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(insert, new
            {
                key.SourceHebRefId, key.TargetStartHebRefId, key.TargetEndHebRefId, state.Origin, state.Rating, state.Comment, state.StartPosition, state.EndPosition,
                state.QuotedText, write.Delete, write.BaseUpdatedAt, UserId = userId,
            }, transaction, cancellationToken: token));
            result.Add(await GetAsync(connection, transaction, id, token));
        }

        return result;
    }

    public static async Task<IReadOnlyList<TskProposalReviewRecord>> GetPendingAsync(IDbConnection connection, IDbTransaction? transaction, int limit,
        CancellationToken token = default)
    {
        const string sql = $"""
                            SELECT {Columns}
                                 , u."DisplayName" AS "AuthorDisplayName"
                                 , EXISTS (
                                       SELECT 1
                                       FROM "unshackled-word"."Tsk" t
                                       WHERE 1=1
                                           AND t."SourceHebRefId" = p."SourceHebRefId"
                                           AND t."TargetStartHebRefId" = p."TargetStartHebRefId"
                                           AND t."TargetEndHebRefId" IS NOT DISTINCT FROM p."TargetEndHebRefId"
                                   ) AS "InTsk"
                                 , c."Origin" AS "CurrentOrigin"
                                 , c."Rating" AS "CurrentRating"
                                 , c."Comment" AS "CurrentComment"
                                 , c."StartPosition" AS "CurrentStartPosition"
                                 , c."EndPosition" AS "CurrentEndPosition"
                                 , c."QuotedText" AS "CurrentQuotedText"
                                 , c."UpdatedAt" AS "CurrentUpdatedAt"
                                 , c."UpdatedByUserId" AS "CurrentUpdatedByUserId"
                            FROM "unshackled-word"."TskCurationProposals" p
                            INNER JOIN "unshackled-word"."Users" u ON u."Id" = p."UserId"
                            LEFT JOIN "unshackled-word"."TskCurations" c
                                ON c."SourceHebRefId" = p."SourceHebRefId"
                                AND c."TargetStartHebRefId" = p."TargetStartHebRefId"
                                AND COALESCE(c."TargetEndHebRefId", 0) = COALESCE(p."TargetEndHebRefId", 0)
                            WHERE 1=1
                                AND p."State" = 'pending'
                            ORDER BY p."SourceHebRefId", p."TargetStartHebRefId", p."TargetEndHebRefId" NULLS FIRST, p."CreatedAt", p."Id"
                            LIMIT @Limit
                            """;

        var rows = await connection.QueryAsync<ReviewRow>(new CommandDefinition(sql, new { Limit = limit }, transaction, cancellationToken: token));
        return rows.Select(r => r.ToReview()).ToList();
    }

    public static async Task<IReadOnlyList<TskProposalRecord>> GetOfUserAsync(IDbConnection connection, IDbTransaction? transaction, int userId, int limit,
        CancellationToken token = default)
    {
        const string sql = $"""
                            SELECT {Columns}
                            FROM "unshackled-word"."TskCurationProposals" p
                            WHERE 1=1
                                AND p."UserId" = @UserId
                            ORDER BY p."CreatedAt" DESC, p."Id" DESC
                            LIMIT @Limit
                            """;

        var rows = await connection.QueryAsync<ProposalRow>(new CommandDefinition(sql, new { UserId = userId, Limit = limit }, transaction, cancellationToken: token));
        return rows.Select(r => r.ToRecord()).ToList();
    }

    public static async Task<TskProposalDecision> AcceptAsync(IDbConnection connection, IDbTransaction transaction, int id, int maintainerId, bool force,
        CancellationToken token = default)
    {
        var proposal = await GetAsync(connection, transaction, id, token, forUpdate: true);
        if (proposal is null)
        {
            return new TskProposalDecision(TskProposalDecisionStatus.NotFound);
        }

        if (proposal.ProposalState != TskProposalStates.Pending)
        {
            return new TskProposalDecision(TskProposalDecisionStatus.NotPending, proposal);
        }

        // Lock before reading the base so no curation write slips in between the check and the apply.
        await connection.ExecuteAsync(new CommandDefinition("SELECT pg_advisory_xact_lock(@LockClass, @SourceHebRefId)",
            new { TskCurationRepository.LockClass, proposal.Key.SourceHebRefId }, transaction, cancellationToken: token));
        var current = await TskCurationRepository.GetAsync(connection, transaction, proposal.Key, token);
        if (!force && current?.UpdatedAt != proposal.BaseUpdatedAt)
        {
            return new TskProposalDecision(TskProposalDecisionStatus.Stale, proposal, current);
        }

        var record = await TskCurationRepository.ApplyAsync(connection, transaction, proposal.Key,
            state => proposal.Delete
                ? state.Origin == TskOrigins.Added ? TskCurationState.Plain : state
                : proposal.State,
            maintainerId, TskCurationSources.Proposal, token, proposal.Id);

        var decided = await DecideAsync(connection, transaction, id, TskProposalStates.Accepted, maintainerId, null, token);
        return new TskProposalDecision(TskProposalDecisionStatus.Ok, decided, record);
    }

    public static async Task<TskProposalDecision> RejectAsync(IDbConnection connection, IDbTransaction transaction, int id, int maintainerId, string? reason,
        CancellationToken token = default)
    {
        var proposal = await GetAsync(connection, transaction, id, token, forUpdate: true);
        if (proposal is null)
        {
            return new TskProposalDecision(TskProposalDecisionStatus.NotFound);
        }

        if (proposal.ProposalState != TskProposalStates.Pending)
        {
            return new TskProposalDecision(TskProposalDecisionStatus.NotPending, proposal);
        }

        var decided = await DecideAsync(connection, transaction, id, TskProposalStates.Rejected, maintainerId, reason, token);
        return new TskProposalDecision(TskProposalDecisionStatus.Ok, decided);
    }

    private static async Task<TskProposalRecord> DecideAsync(IDbConnection connection, IDbTransaction transaction, int id, string state, int maintainerId, string? reason,
        CancellationToken token)
    {
        const string sql = """
                           UPDATE "unshackled-word"."TskCurationProposals" p
                           SET "State" = @State
                             , "DecidedByUserId" = @MaintainerId
                             , "DecidedAt" = now()
                             , "Reason" = @Reason
                           WHERE 1=1
                               AND p."Id" = @Id
                           """;
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id, State = state, MaintainerId = maintainerId, Reason = reason }, transaction,
            cancellationToken: token));

        return (await GetAsync(connection, transaction, id, token))!;
    }

    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private class ProposalRow
    {
        public int Id { get; init; }
        public int SourceHebRefId { get; init; }
        public int TargetStartHebRefId { get; init; }
        public int? TargetEndHebRefId { get; init; }
        public string Origin { get; init; } = default!;
        public string? Rating { get; init; }
        public string? Comment { get; init; }
        public int? StartPosition { get; init; }
        public int? EndPosition { get; init; }
        public string? QuotedText { get; init; }
        public bool Delete { get; init; }
        public DateTime? BaseUpdatedAt { get; init; }
        public int UserId { get; init; }
        public DateTime CreatedAt { get; init; }
        public string State { get; init; } = default!;
        public int? DecidedByUserId { get; init; }
        public DateTime? DecidedAt { get; init; }
        public string? Reason { get; init; }

        public TskProposalRecord ToRecord() => new(
            Id,
            new TskLinkKey(SourceHebRefId, TargetStartHebRefId, TargetEndHebRefId),
            new TskCurationState(Origin, Rating, Comment, StartPosition, EndPosition, QuotedText),
            Delete,
            BaseUpdatedAt is { } b ? Utc(b) : null,
            UserId,
            Utc(CreatedAt),
            State,
            DecidedByUserId,
            DecidedAt is { } d ? Utc(d) : null,
            Reason);
    }

    private sealed class ReviewRow : ProposalRow
    {
        public string AuthorDisplayName { get; init; } = string.Empty;
        public bool InTsk { get; init; }
        public string? CurrentOrigin { get; init; }
        public string? CurrentRating { get; init; }
        public string? CurrentComment { get; init; }
        public int? CurrentStartPosition { get; init; }
        public int? CurrentEndPosition { get; init; }
        public string? CurrentQuotedText { get; init; }
        public DateTime? CurrentUpdatedAt { get; init; }
        public int? CurrentUpdatedByUserId { get; init; }

        public TskProposalReviewRecord ToReview()
        {
            var proposal = ToRecord();
            var current = CurrentOrigin is null
                ? null
                : new TskCurationRecord(proposal.Key,
                    new TskCurationState(CurrentOrigin, CurrentRating, CurrentComment, CurrentStartPosition, CurrentEndPosition, CurrentQuotedText),
                    Utc(CurrentUpdatedAt!.Value), CurrentUpdatedByUserId);

            return new TskProposalReviewRecord(proposal, AuthorDisplayName, InTsk, current);
        }
    }
}
