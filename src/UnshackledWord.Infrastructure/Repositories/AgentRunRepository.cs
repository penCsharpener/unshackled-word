using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.AgentMapping;

namespace UnshackledWord.Infrastructure.Repositories;

/// <summary>Run state of the agent worker: status, totals, caps, the AI users. Proposals themselves are written by <see cref="AgentStoreRepository"/>.</summary>
public sealed class AgentRunRepository : IAgentRunRepository
{
    private readonly IDbConnectionFactory _factory;

    public AgentRunRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<AgentRunRecord?> GetRunAsync(int runId, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        return await connection.QuerySingleOrDefaultAsync<AgentRunRecord>(new CommandDefinition(AgentRunSql.GetRun, new { RunId = runId }, cancellationToken: token));
    }

    public async Task<IReadOnlyList<AgentRunRecord>> ListRunsAsync(int limit, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        return (await connection.QueryAsync<AgentRunRecord>(new CommandDefinition(AgentRunSql.ListRuns, new { Limit = limit }, cancellationToken: token))).AsList();
    }

    public async Task SetRunStatusAsync(int runId, string status, string? note, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        await connection.ExecuteAsync(new CommandDefinition(AgentRunSql.SetStatus, new { RunId = runId, Status = status, Note = Trim(note) }, cancellationToken: token));
    }

    public async Task<bool> SetRunStatusUnlessCancelledAsync(int runId, string status, string? note, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        return await connection.ExecuteAsync(new CommandDefinition(AgentRunSql.SetStatusUnlessCancelled, new { RunId = runId, Status = status, Note = Trim(note) }, cancellationToken: token)) > 0;
    }

    public async Task<string?> GetRunStatusAsync(int runId, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(AgentRunSql.GetStatus, new { RunId = runId }, cancellationToken: token));
    }

    public async Task AddUsageAsync(int runId, Guid packageId, long inputTokens, long cachedInputTokens, long outputTokens, decimal costUsd, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();
        var args = new { RunId = runId, PackageId = packageId, InputTokens = inputTokens, CachedInputTokens = cachedInputTokens, OutputTokens = outputTokens, CostUsd = costUsd };
        await connection.ExecuteAsync(new CommandDefinition(AgentRunSql.AddRunUsage, args, transaction, cancellationToken: token));
        await connection.ExecuteAsync(new CommandDefinition(AgentRunSql.AddPackageUsage, args, transaction, cancellationToken: token));
        transaction.Commit();
    }

    public async Task AddRunUsageAsync(int runId, long inputTokens, long cachedInputTokens, long outputTokens, decimal costUsd, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        await connection.ExecuteAsync(new CommandDefinition(AgentRunSql.AddRunUsage, new { RunId = runId, InputTokens = inputTokens, CachedInputTokens = cachedInputTokens, OutputTokens = outputTokens, CostUsd = costUsd }, cancellationToken: token));
    }

    public async Task SetCapsAsync(int runId, long? maxTokens, decimal? maxCostUsd, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        await connection.ExecuteAsync(new CommandDefinition(AgentRunSql.SetCaps, new { RunId = runId, MaxTokens = maxTokens, MaxCostUsd = maxCostUsd }, cancellationToken: token));
    }

    public async Task<IReadOnlySet<int>> GetProposedRefIdsAsync(int runId, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        return (await connection.QueryAsync<int>(new CommandDefinition(AgentRunSql.GetProposedRefIds, new { RunId = runId }, cancellationToken: token))).ToHashSet();
    }

    public async Task<int> GetOrCreateAiUserAsync(string model, CancellationToken token = default)
    {
        var displayName = AiUserName(model);
        var email = AiUserEmail(model);
        using var connection = await _factory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();

        // Serialises concurrent first uses of one model; the unique email index is the second line of defence.
        await connection.ExecuteAsync(new CommandDefinition("SELECT pg_advisory_xact_lock(hashtext(@Email))", new { Email = email }, transaction, cancellationToken: token));
        var id = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(AgentRunSql.FindAiUser, new { Email = email }, transaction, cancellationToken: token));
        if (id is null)
        {
            id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(AgentRunSql.InsertAiUser, new { Email = email, DisplayName = displayName }, transaction, cancellationToken: token));
            await connection.ExecuteAsync(new CommandDefinition(AgentRunSql.InsertAiRole, new { UserId = id.Value, Role = "ai" }, transaction, cancellationToken: token));
        }

        transaction.Commit();

        return id.Value;
    }

    public async Task<int> MarkInterruptedRunsAsync(string note, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        return await connection.ExecuteAsync(new CommandDefinition(AgentRunSql.MarkInterrupted, new { Note = Trim(note) }, cancellationToken: token));
    }

    /// <summary>Display name of the AI user of a model: <c>ai:&lt;model&gt;</c> (e.g. <c>ai:gemma4:31b</c>).</summary>
    public static string AiUserName(string model) => $"ai:{model.Trim()}";

    /// <summary>Unique, never deliverable address (<c>.invalid</c>) of the AI user; the account has no password and no verified email, so nobody can sign in.</summary>
    public static string AiUserEmail(string model)
    {
        var slug = new string(model.Trim().ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' ? c : '_').ToArray());

        return $"ai-{slug}@ai.invalid";
    }

    private static string? Trim(string? note) => note is { Length: > 1000 } ? note[..1000] : note;
}

public static class AgentRunSql
{
    private const string RunColumns = """
                                      r."Id"
                                           , r."CreatedByUserId"
                                           , r."AiUserId"
                                           , r."ApiTokenId"
                                           , r."Profile"
                                           , r."Model"
                                           , r."PromptVersion"
                                           , r."BibleBookId"
                                           , r."FromHebRefId" "FromRefId"
                                           , r."ToHebRefId" "ToRefId"
                                           , r."Mode"
                                           , r."Status"
                                           , r."StatusNote"
                                           , r."InputTokens"
                                           , r."CachedInputTokens"
                                           , r."OutputTokens"
                                           , r."CostUsd"
                                           , r."EstimatedTokens"
                                           , r."EstimatedCostUsd"
                                           , r."MaxTokens"
                                           , r."MaxCostUsd"
                                           , r."CreatedAt"
                                           , r."StartedAt"
                                           , r."FinishedAt"
                                           , (SELECT count(*) FROM "unshackled-word"."AgentPackages" p WHERE p."RunId" = r."Id")::int "Packages"
                                           , (SELECT count(*) FROM "unshackled-word"."AgentPackages" p WHERE p."RunId" = r."Id" AND p."Status" = 'completed')::int "PackagesCompleted"
                                           , (SELECT count(*) FROM "unshackled-word"."AgentPackages" p WHERE p."RunId" = r."Id" AND p."Status" = 'failed')::int "PackagesFailed"
                                           , (SELECT count(*) FROM "unshackled-word"."AgentVerseProposals" v WHERE v."RunId" = r."Id")::int "ProposedVerses"
                                           , (SELECT count(*) FROM "unshackled-word"."AgentVerseProposals" v WHERE v."RunId" = r."Id" AND CASE jsonb_typeof(v."Flags") WHEN 'array' THEN v."Flags" <> '[]'::jsonb WHEN 'object' THEN COALESCE(v."Flags" -> 'flags', '[]'::jsonb) <> '[]'::jsonb ELSE FALSE END)::int "FlaggedVerses"
                                           , (SELECT count(*) FROM "unshackled-word"."AgentVerseProposals" v WHERE v."RunId" = r."Id" AND v."Status" = 'open')::int "OpenProposals"
                                      """;

    public const string GetRun = $"""
                                  SELECT {RunColumns}
                                  FROM "unshackled-word"."AgentRuns" r
                                  WHERE 1=1
                                      AND r."Id" = @RunId
                                  """;

    public const string ListRuns = $"""
                                    SELECT {RunColumns}
                                    FROM "unshackled-word"."AgentRuns" r
                                    ORDER BY r."Id" DESC
                                    LIMIT @Limit
                                    """;

    public const string SetStatus = """
                                    UPDATE "unshackled-word"."AgentRuns"
                                    SET "Status" = @Status
                                      , "StatusNote" = @Note
                                      , "StartedAt" = CASE WHEN @Status = 'running' THEN coalesce("StartedAt", now()) ELSE "StartedAt" END
                                      , "FinishedAt" = CASE WHEN @Status IN ('queued', 'running') THEN NULL ELSE now() END
                                    WHERE "Id" = @RunId
                                    """;

    public const string SetStatusUnlessCancelled = """
                                                   UPDATE "unshackled-word"."AgentRuns"
                                                   SET "Status" = @Status
                                                     , "StatusNote" = @Note
                                                     , "StartedAt" = CASE WHEN @Status = 'running' THEN coalesce("StartedAt", now()) ELSE "StartedAt" END
                                                     , "FinishedAt" = CASE WHEN @Status IN ('queued', 'running') THEN NULL ELSE now() END
                                                   WHERE "Id" = @RunId
                                                     AND "Status" <> 'cancelled'
                                                   """;

    public const string GetStatus = """
                                    SELECT "Status"
                                    FROM "unshackled-word"."AgentRuns"
                                    WHERE "Id" = @RunId
                                    """;

    public const string AddRunUsage = """
                                      UPDATE "unshackled-word"."AgentRuns"
                                      SET "InputTokens" = "InputTokens" + @InputTokens
                                        , "CachedInputTokens" = "CachedInputTokens" + @CachedInputTokens
                                        , "OutputTokens" = "OutputTokens" + @OutputTokens
                                        , "CostUsd" = "CostUsd" + @CostUsd
                                      WHERE "Id" = @RunId
                                      """;

    public const string AddPackageUsage = """
                                          UPDATE "unshackled-word"."AgentPackages"
                                          SET "InputTokens" = "InputTokens" + @InputTokens
                                            , "CachedInputTokens" = "CachedInputTokens" + @CachedInputTokens
                                            , "OutputTokens" = "OutputTokens" + @OutputTokens
                                            , "CostUsd" = "CostUsd" + @CostUsd
                                            , "Calls" = "Calls" + 1
                                          WHERE "Id" = @PackageId
                                          """;

    public const string SetCaps = """
                                  UPDATE "unshackled-word"."AgentRuns"
                                  SET "MaxTokens" = @MaxTokens
                                    , "MaxCostUsd" = @MaxCostUsd
                                  WHERE "Id" = @RunId
                                  """;

    public const string GetProposedRefIds = """
                                            SELECT DISTINCT v."BibleBookId" * 1000000 + v."Chapter" * 1000 + v."Verse"
                                            FROM "unshackled-word"."AgentVerseProposals" v
                                            WHERE 1=1
                                                AND v."RunId" = @RunId
                                            """;

    public const string FindAiUser = """
                                     SELECT u."Id"
                                     FROM "unshackled-word"."Users" u
                                     WHERE 1=1
                                         AND u."EmailNormalized" = @Email
                                     """;

    public const string InsertAiUser = """
                                       INSERT INTO "unshackled-word"."Users" ("Email", "EmailNormalized", "PasswordHash", "DisplayName")
                                       VALUES (@Email, @Email, NULL, @DisplayName)
                                       RETURNING "Id"
                                       """;

    public const string InsertAiRole = """
                                       INSERT INTO "unshackled-word"."UserRoles" ("UserId", "Role")
                                       VALUES (@UserId, @Role)
                                       """;

    public const string MarkInterrupted = """
                                          UPDATE "unshackled-word"."AgentRuns"
                                          SET "Status" = 'failed'
                                            , "StatusNote" = @Note
                                            , "FinishedAt" = now()
                                          WHERE "Status" IN ('queued', 'running')
                                          """;
}
