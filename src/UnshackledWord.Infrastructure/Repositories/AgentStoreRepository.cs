using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.AgentMapping;

namespace UnshackledWord.Infrastructure.Repositories;

/// <summary>Runs, packages and proposals of the agent mapping (tables from migration 0025). Proposals are never live data.</summary>
public sealed class AgentStoreRepository : IAgentStoreRepository
{
    private readonly IDbConnectionFactory _factory;

    public AgentStoreRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<int> CreateRunAsync(AgentRunInsert run, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(AgentStoreSql.InsertRun, new
        {
            run.CreatedByUserId,
            run.AiUserId,
            run.Profile,
            run.Model,
            run.PromptVersion,
            run.BibleBookId,
            FromHebRefId = run.FromRefId,
            ToHebRefId = run.ToRefId,
            run.Mode,
            run.Status,
            run.StatusNote,
            run.EstimatedTokens,
            run.EstimatedCostUsd,
            run.MaxTokens,
            run.MaxCostUsd,
            run.ApiTokenId,
        }, cancellationToken: token));
    }

    public async Task InsertPackageAsync(AgentPackageInsert package, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        await connection.ExecuteAsync(new CommandDefinition(AgentStoreSql.InsertPackage, new
        {
            package.Id,
            package.RunId,
            FromHebRefId = package.FromRefId,
            ToHebRefId = package.ToRefId,
            package.WordIdsHash,
            LocalIdMap = package.LocalIdMapJson,
            ExpiresAt = package.ExpiresAt.UtcDateTime,
        }, cancellationToken: token));
    }

    public async Task<AgentPackageRecord?> GetPackageAsync(Guid packageId, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        return await connection.QuerySingleOrDefaultAsync<AgentPackageRecord>(new CommandDefinition(AgentStoreSql.GetPackage, new { PackageId = packageId }, cancellationToken: token));
    }

    public async Task SetPackageStatusAsync(Guid packageId, string status, bool countAttempt, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        await connection.ExecuteAsync(new CommandDefinition(AgentStoreSql.SetPackageStatus, new { PackageId = packageId, Status = status, CountAttempt = countAttempt }, cancellationToken: token));
    }

    public async Task<int> CountProposedVersesAsync(Guid packageId, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(AgentStoreSql.CountProposedVerses, new { PackageId = packageId }, cancellationToken: token));
    }

    public async Task<AgentProposalStoreResult> StoreProposalsAsync(int runId, Guid packageId, IReadOnlyList<AgentProposalInsert> proposals, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();
        var superseded = 0;
        var ids = new List<long>();

        foreach (var proposal in proposals)
        {
            superseded += await connection.ExecuteAsync(new CommandDefinition(AgentStoreSql.SupersedeOpen, new { proposal.BibleBookId, proposal.Chapter, proposal.Verse, proposal.Language, proposal.TextForm, Subverse = proposal.Subverse.ToString(System.Globalization.CultureInfo.InvariantCulture) }, transaction, cancellationToken: token));
            ids.Add(await connection.ExecuteScalarAsync<long>(new CommandDefinition(AgentStoreSql.InsertProposal, new
            {
                RunId = runId,
                PackageId = packageId,
                proposal.BibleBookId,
                proposal.Chapter,
                proposal.Verse,
                proposal.Language,
                Links = proposal.LinksJson,
                Lemmas = proposal.LemmasJson,
                Parts = proposal.PartsJson,
                Roles = proposal.RolesJson,
                Flags = proposal.FlagsJson,
                proposal.Note,
            }, transaction, cancellationToken: token)));
        }

        transaction.Commit();

        return new AgentProposalStoreResult { ProposalIds = ids, Superseded = superseded };
    }
}

public static class AgentStoreSql
{
    // queued: no start, no end; running: started now; everything else (the debug endpoint writes "completed"): started and finished now.
    public const string InsertRun = """
                                    INSERT INTO "unshackled-word"."AgentRuns" ("CreatedByUserId", "AiUserId", "Profile", "Model", "PromptVersion", "BibleBookId", "FromHebRefId", "ToHebRefId", "Mode", "Status", "StatusNote", "EstimatedTokens", "EstimatedCostUsd", "MaxTokens", "MaxCostUsd", "ApiTokenId", "IncludeConfirmed", "TextForm", "StartedAt", "FinishedAt")
                                    VALUES (@CreatedByUserId, @AiUserId, @Profile, @Model, @PromptVersion, @BibleBookId, @FromHebRefId, @ToHebRefId, @Mode, @Status, @StatusNote, @EstimatedTokens, @EstimatedCostUsd, @MaxTokens, @MaxCostUsd, @ApiTokenId, @IncludeConfirmed, @TextForm
                                          , CASE WHEN @Status = 'queued' THEN NULL ELSE now() END
                                          , CASE WHEN @Status IN ('queued', 'running') THEN NULL ELSE now() END)
                                    RETURNING "Id"
                                    """;

    public const string InsertPackage = """
                                        INSERT INTO "unshackled-word"."AgentPackages" ("Id", "RunId", "FromHebRefId", "ToHebRefId", "WordIdsHash", "LocalIdMap", "ExpiresAt")
                                        VALUES (@Id, @RunId, @FromHebRefId, @ToHebRefId, @WordIdsHash, @LocalIdMap::jsonb, @ExpiresAt)
                                        """;

    public const string GetPackage = """
                                     SELECT p."Id"
                                          , p."RunId"
                                          , p."WordIdsHash"
                                          , p."LocalIdMap"::text "LocalIdMap"
                                          , p."Status"
                                          , p."Attempts"
                                          , p."ExpiresAt"
                                     FROM "unshackled-word"."AgentPackages" p
                                     WHERE 1=1
                                         AND p."Id" = @PackageId
                                     """;

    public const string SetPackageStatus = """
                                           UPDATE "unshackled-word"."AgentPackages"
                                           SET "Status" = @Status
                                             , "Attempts" = "Attempts" + CASE WHEN @CountAttempt THEN 1 ELSE 0 END
                                           WHERE 1=1
                                               AND "Id" = @PackageId
                                           """;

    /// <summary>Distinct verses by the key of the open-proposal index (an LXX verse with subverses or two text forms counts each unit).</summary>
    public const string CountProposedVerses = """
                                              SELECT count(DISTINCT ("Chapter", "Verse", "Language", COALESCE("Flags" ->> 'textForm', ''), COALESCE("Flags" ->> 'subverse', '0')))::int
                                              FROM "unshackled-word"."AgentVerseProposals"
                                              WHERE 1=1
                                                  AND "PackageId" = @PackageId
                                              """;

    /// <summary>
    /// At most one open proposal per verse (unique index <c>AgentVerseProposals_Open_UQ</c>, migration 0051): the older one is marked superseded
    /// before the new one is inserted. The key is the index's own: book, chapter, verse, language and, for LXX proposals, <c>Flags.textForm</c> and
    /// <c>Flags.subverse</c> (mapping proposals have an array in "Flags", so both read as the defaults). @Subverse is text, like the index.
    /// </summary>
    public const string SupersedeOpen = """
                                        UPDATE "unshackled-word"."AgentVerseProposals"
                                        SET "Status" = 'superseded'
                                        WHERE 1=1
                                            AND "BibleBookId" = @BibleBookId
                                            AND "Chapter" = @Chapter
                                            AND "Verse" = @Verse
                                            AND "Language" = @Language
                                            AND COALESCE("Flags" ->> 'textForm', '') = @TextForm
                                            AND COALESCE("Flags" ->> 'subverse', '0') = @Subverse
                                            AND "Status" = 'open'
                                        """;

    public const string InsertProposal = """
                                         INSERT INTO "unshackled-word"."AgentVerseProposals" ("RunId", "PackageId", "BibleBookId", "Chapter", "Verse", "Language", "Links", "Lemmas", "Parts", "Roles", "Flags", "Note")
                                         VALUES (@RunId, @PackageId, @BibleBookId, @Chapter, @Verse, @Language, @Links::jsonb, @Lemmas::jsonb, @Parts::jsonb, @Roles::jsonb, @Flags::jsonb, @Note)
                                         RETURNING "Id"
                                         """;
}
