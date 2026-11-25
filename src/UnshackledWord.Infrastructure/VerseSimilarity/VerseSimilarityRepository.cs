using System.Data;
using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.VerseSimilarity;
using UnshackledWord.Domain.Models.Dbo.VerseSimilarity;

namespace UnshackledWord.Infrastructure.VerseSimilarity;

/// <summary>
/// Dapper access to <c>VerseSimilarityRuns</c> and <c>VerseSimilarities</c> (roadmap 0027.2, migration 0049). The pairs are replaced as a whole
/// in one transaction: DELETE, then INSERT … SELECT FROM unnest(arrays) in batches of <see cref="BatchSize"/> rows (about 915k rows in total),
/// then the run row is marked succeeded before the commit.
/// </summary>
public sealed class VerseSimilarityRepository : IVerseSimilarityRepository
{
    /// <summary>
    /// First key of the session-level advisory lock that allows one rebuild at a time (second key <see cref="RebuildLockKey"/>). Class 270_001 is
    /// reserved for roadmap 0027 (TSK curation uses 240_003). The maintainer job (0027.5) takes the same lock.
    /// </summary>
    public const int LockClass = 270_001;

    /// <summary>Second key of the rebuild lock.</summary>
    public const int RebuildLockKey = 1;

    /// <summary>Rows per INSERT statement; keeps each parameter array at a few MB.</summary>
    public const int BatchSize = 100_000;

    /// <summary>Seconds for the DELETE and each insert batch (the whole replace takes seconds on the dev DB; slower hosts get headroom).</summary>
    private const int CommandTimeout = 900;

    private readonly IDbConnectionFactory _factory;

    public VerseSimilarityRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IAsyncDisposable?> TryAcquireRebuildLockAsync(CancellationToken token = default)
    {
        var connection = await _factory.CreateDbConnectionAsync(token);
        try
        {
            const string sql = """
                               SELECT pg_try_advisory_lock(@LockClass, @RebuildLockKey)
                               """;

            var acquired = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(sql, new { LockClass, RebuildLockKey }, cancellationToken: token));
            if (acquired)
            {
                return new RebuildLock(connection);
            }
        }
        catch
        {
            connection.Dispose();
            throw;
        }

        connection.Dispose();
        return null;
    }

    public async Task<int> FailInterruptedRunsAsync(CancellationToken token = default)
    {
        const string sql = """
                           UPDATE "unshackled-word"."VerseSimilarityRuns" r
                           SET "Status" = @Failed
                             , "FinishedAt" = clock_timestamp()
                             , "Error" = 'interrupted: still running when the next rebuild started'
                           WHERE 1=1
                               AND r."Status" = @Running
                           """;

        using var connection = await _factory.CreateDbConnectionAsync(token);
        return await connection.ExecuteAsync(new CommandDefinition(sql,
            new { Failed = VerseSimilarityRunDbo.StatusFailed, Running = VerseSimilarityRunDbo.StatusRunning }, cancellationToken: token));
    }

    public async Task<int> CreateRunAsync(VerseSimilarityParameters parameters, int? startedBy, CancellationToken token = default)
    {
        const string sql = """
                           INSERT INTO "unshackled-word"."VerseSimilarityRuns" ("StartedAt", "Status", "StartedBy", "StopDfShare", "TopK", "MinScore", "PhraseWeight")
                           VALUES (now(), @Status, @StartedBy, @StopDfShare, @TopK, @MinScore, @PhraseWeight)
                           RETURNING "Id"
                           """;

        using var connection = await _factory.CreateDbConnectionAsync(token);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, new
        {
            Status = VerseSimilarityRunDbo.StatusRunning,
            StartedBy = startedBy,
            StopDfShare = Round(parameters.StopDfShare),
            TopK = (short)parameters.TopK,
            MinScore = Round(parameters.MinScore),
            PhraseWeight = Round(parameters.PhraseWeight),
        }, cancellationToken: token));
    }

    public async Task ReplacePairsAsync(int runId, IReadOnlyCollection<VerseSimilarityHit> hits, Func<VerseSimilarityRunSuccess> complete, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();

        await ReplacePairsAsync(connection, transaction, runId, hits, token);

        var success = complete();

        // clock_timestamp(), not now(): inside the transaction now() is the time the replace began, not when it finished.
        const string succeeded = """
                                 UPDATE "unshackled-word"."VerseSimilarityRuns" r
                                 SET "Status" = @Status
                                   , "FinishedAt" = clock_timestamp()
                                   , "PairCount" = @PairCount
                                   , "StopWordCount" = @StopWordCount
                                   , "DurationMs" = @DurationMs
                                   , "Error" = NULL
                                 WHERE 1=1
                                     AND r."Id" = @RunId
                                 """;

        await connection.ExecuteAsync(new CommandDefinition(succeeded, new
        {
            Status = VerseSimilarityRunDbo.StatusSucceeded,
            success.PairCount,
            success.StopWordCount,
            success.DurationMs,
            RunId = runId,
        }, transaction, cancellationToken: token));

        transaction.Commit();
    }

    /// <summary>DELETE + batched unnest insert on a given connection and transaction (public for the database test, which rolls back).</summary>
    public static async Task ReplacePairsAsync(IDbConnection connection, IDbTransaction transaction, int runId, IReadOnlyCollection<VerseSimilarityHit> hits,
        CancellationToken token = default)
    {
        const string delete = """
                              DELETE FROM "unshackled-word"."VerseSimilarities" vs
                              """;

        await connection.ExecuteAsync(new CommandDefinition(delete, null, transaction, CommandTimeout, cancellationToken: token));

        const string insert = """
                              INSERT INTO "unshackled-word"."VerseSimilarities" ("SourceLxxRefId", "TargetLxxRefId", "Rank", "Score", "SharedCount", "PhraseCount", "RunId")
                              SELECT u."SourceLxxRefId"
                                   , u."TargetLxxRefId"
                                   , u."Rank"
                                   , u."Score"
                                   , u."SharedCount"
                                   , u."PhraseCount"
                                   , @RunId
                              FROM unnest(@Sources::int4[], @Targets::int4[], @Ranks::int2[], @Scores::numeric[], @Shared::int2[], @Phrases::int2[])
                                  AS u ("SourceLxxRefId", "TargetLxxRefId", "Rank", "Score", "SharedCount", "PhraseCount")
                              """;

        foreach (var batch in hits.Chunk(BatchSize))
        {
            token.ThrowIfCancellationRequested();

            var parameters = new
            {
                RunId = runId,
                Sources = batch.Select(h => h.SourceVerseId).ToArray(),
                Targets = batch.Select(h => h.TargetVerseId).ToArray(),
                Ranks = batch.Select(h => checked((short)h.Rank)).ToArray(),
                Scores = batch.Select(h => Round(h.Score)).ToArray(),
                Shared = batch.Select(h => (short)Math.Min(h.SharedCount, short.MaxValue)).ToArray(),
                Phrases = batch.Select(h => (short)Math.Min(h.PhraseCount, short.MaxValue)).ToArray(),
            };

            await connection.ExecuteAsync(new CommandDefinition(insert, parameters, transaction, CommandTimeout, cancellationToken: token));
        }
    }

    public async Task MarkFailedAsync(int runId, string error, int durationMs, CancellationToken token = default)
    {
        const string sql = """
                           UPDATE "unshackled-word"."VerseSimilarityRuns" r
                           SET "Status" = @Status
                             , "FinishedAt" = clock_timestamp()
                             , "DurationMs" = @DurationMs
                             , "Error" = @Error
                           WHERE 1=1
                               AND r."Id" = @RunId
                           """;

        using var connection = await _factory.CreateDbConnectionAsync(token);
        await connection.ExecuteAsync(new CommandDefinition(sql,
            new { Status = VerseSimilarityRunDbo.StatusFailed, DurationMs = durationMs, Error = error, RunId = runId }, cancellationToken: token));
    }

    public async Task<VerseSimilarityRunDbo?> GetLatestRunAsync(CancellationToken token = default)
    {
        const string sql = """
                           SELECT r."Id"
                                , r."StartedAt"
                                , r."FinishedAt"
                                , r."Status"
                                , r."StartedBy"
                                , r."StopDfShare"
                                , r."TopK"
                                , r."MinScore"
                                , r."PhraseWeight"
                                , r."PairCount"
                                , r."StopWordCount"
                                , r."DurationMs"
                                , r."Evaluation"::text "Evaluation"
                                , r."Error"
                           FROM "unshackled-word"."VerseSimilarityRuns" r
                           ORDER BY r."Id" DESC
                           LIMIT 1
                           """;

        using var connection = await _factory.CreateDbConnectionAsync(token);
        return await connection.QueryFirstOrDefaultAsync<VerseSimilarityRunDbo>(new CommandDefinition(sql, cancellationToken: token));
    }

    public async Task<bool> PairsMissingAsync(CancellationToken token = default)
    {
        const string sql = """
                           SELECT NOT EXISTS (SELECT 1 FROM "unshackled-word"."VerseSimilarities")
                           """;

        using var connection = await _factory.CreateDbConnectionAsync(token);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(sql, cancellationToken: token));
    }

    public async Task<VerseSimilarityRunDbo?> GetRunAsync(int runId, CancellationToken token = default)
    {
        const string sql = """
                           SELECT r."Id"
                                , r."StartedAt"
                                , r."FinishedAt"
                                , r."Status"
                                , r."StartedBy"
                                , r."StopDfShare"
                                , r."TopK"
                                , r."MinScore"
                                , r."PhraseWeight"
                                , r."PairCount"
                                , r."StopWordCount"
                                , r."DurationMs"
                                , r."Evaluation"::text "Evaluation"
                                , r."Error"
                           FROM "unshackled-word"."VerseSimilarityRuns" r
                           WHERE 1=1
                               AND r."Id" = @RunId
                           """;

        using var connection = await _factory.CreateDbConnectionAsync(token);
        return await connection.QueryFirstOrDefaultAsync<VerseSimilarityRunDbo>(new CommandDefinition(sql, new { RunId = runId }, cancellationToken: token));
    }

    public async Task<SimilarVersePage> GetPageAsync(int fromHebRefId, int toHebRefId, int limit, CancellationToken token = default)
    {
        const string sourcesSql = """
                                  SELECT ev."HebRefId"
                                       , ev."LxxRefId"
                                  FROM "unshackled-word"."Elb1871Verses" ev
                                  WHERE 1=1
                                      AND ev."HebRefId" BETWEEN @From AND @To
                                  ORDER BY ev."HebRefId"
                                  """;

        // A STEP verse can belong to two Elberfelder verses (Psalm titles, Rom 16:25-27 ...): the target takes the one with the same number, else the lowest.
        const string hitsSql = """
                               SELECT es."HebRefId" "SourceHebRefId"
                                    , vs."SourceLxxRefId"
                                    , vs."TargetLxxRefId"
                                    , et."HebRefId" "TargetHebRefId"
                                    , et."VerseText" "TargetText"
                                    , vs."Rank"::int4 "Rank"
                                    , vs."Score"
                                    , vs."SharedCount"::int4 "SharedCount"
                                    , vs."PhraseCount"::int4 "PhraseCount"
                                    , vs."RunId"
                               FROM "unshackled-word"."Elb1871Verses" es
                                   JOIN "unshackled-word"."VerseSimilarities" vs ON vs."SourceLxxRefId" = es."LxxRefId"
                                   LEFT JOIN LATERAL (SELECT e."HebRefId"
                                                           , e."VerseText"
                                                      FROM "unshackled-word"."Elb1871Verses" e
                                                      WHERE 1=1
                                                          AND e."LxxRefId" = vs."TargetLxxRefId"
                                                      ORDER BY (e."HebRefId" = e."LxxRefId") DESC, e."HebRefId"
                                                      LIMIT 1) et ON TRUE
                               WHERE 1=1
                                   AND es."HebRefId" BETWEEN @From AND @To
                                   AND vs."Rank" <= @Limit
                               ORDER BY es."HebRefId", vs."Rank"
                               """;

        var args = new { From = fromHebRefId, To = toHebRefId, Limit = limit };
        using var connection = await _factory.CreateDbConnectionAsync(token);
        var sources = (await connection.QueryAsync<SimilarSourceVerse>(new CommandDefinition(sourcesSql, args, cancellationToken: token))).ToList();
        var hits = (await connection.QueryAsync<SimilarHitRow>(new CommandDefinition(hitsSql, args, cancellationToken: token))).ToList();
        return new SimilarVersePage(sources, hits);
    }

    private static decimal Round(double value) => Math.Round((decimal)value, 4, MidpointRounding.AwayFromZero);

    /// <summary>Holds the connection with the session-level lock; disposing unlocks and closes it.</summary>
    private sealed class RebuildLock(IDbConnection connection) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                const string sql = """
                                   SELECT pg_advisory_unlock(@LockClass, @RebuildLockKey)
                                   """;

                await connection.ExecuteScalarAsync<bool>(new CommandDefinition(sql, new { LockClass, RebuildLockKey }));
            }
            finally
            {
                // Closing also ends the session lock if the unlock failed (pooled connections are reset with DISCARD ALL).
                connection.Dispose();
            }
        }
    }
}
