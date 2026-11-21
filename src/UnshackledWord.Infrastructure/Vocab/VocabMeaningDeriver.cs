using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Domain.Models.Vocab;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Infrastructure.Vocab;

/// <summary>Which entries a derivation covers: all, a course, or a rank range within a course.</summary>
public sealed record VocabMeaningScope(string? Course = null, int? RankFrom = null, int? RankTo = null);

/// <summary>
/// Derives <c>VocabMeanings</c> from <c>StatsVocabRenderings</c> (roadmap 0022.3, docs/roadmaps/0022-vocab-trainer/03-vocab-model.md
/// "Deriving meanings"). The selection itself is the pure <see cref="VocabMeaningSelector"/>; this class loads the data, writes the plan
/// (insert / update / delete of replaceable rows), bumps <c>ContentVersion</c> and writes one <c>meanings</c> change row when an entry's
/// accepted set changed. Each batch of entries is one transaction. Used by the maintainer job
/// (<c>POST api/maintainer/vocab/meanings/derive</c>) and the SeedDb verb <c>vocab-derive</c>.
/// </summary>
public sealed class VocabMeaningDeriver
{
    public const string Kind = "vocab";
    public const string Action = "meanings";
    public const string ChangeKind = "meanings";

    private const int TimeoutSeconds = 900;
    private const int BatchSize = 500;
    private const int MaxGermanLength = 80;

    private const string SelectCourseTrack = """
        SELECT c."Track"
        FROM "unshackled-word"."VocabCourses" c
        WHERE 1=1
            AND c."Code" = @Code
        """;

    private const string SelectEntries = """
        SELECT e."Id"
             , e."Lemma"
             , e."Pos"
        FROM "unshackled-word"."VocabEntries" e
        WHERE 1=1
            AND e."RetiredAt" IS NULL
            AND (@Track::char(1) IS NULL OR e."Track" = @Track::char(1))
            AND (@RankFrom::int IS NULL OR e."FrequencyRank" >= @RankFrom::int)
            AND (@RankTo::int IS NULL OR e."FrequencyRank" <= @RankTo::int)
        ORDER BY e."Id"
        """;

    private const string SelectRenderings = """
        SELECT vs."VocabEntryId"                                                                    AS "EntryId"
             , r."GermanKey"
             , (array_agg(r."GermanDisplay" ORDER BY r."LinkCount" DESC, r."GermanDisplay"))[1]     AS "German"
             , r."GermanPos"
             , sum(r."LinkCount")::int                                                              AS "LinkCount"
        FROM "unshackled-word"."VocabStrongs" vs
        INNER JOIN "unshackled-word"."StatsVocabRenderings" r ON 1=1
            AND r."Lang" = vs."Lang"
            AND r."StrongsKey" = vs."StrongsKey"
        WHERE 1=1
            AND vs."VocabEntryId" = ANY(@Ids)
        GROUP BY vs."VocabEntryId", r."GermanKey", r."GermanPos"
        """;

    private const string SelectMeanings = """
        SELECT m."VocabEntryId" AS "EntryId"
             , m."Id"
             , m."German"
             , m."GermanKey"
             , m."Rank"
             , m."Count"
             , m."Share"
             , m."IsAccepted"
             , m."Origin"
             , m."IsPinned"
        FROM "unshackled-word"."VocabMeanings" m
        WHERE 1=1
            AND m."VocabEntryId" = ANY(@Ids)
        """;

    private const string InsertMeaning = """
        INSERT INTO "unshackled-word"."VocabMeanings"
            ("VocabEntryId", "German", "GermanKey", "Rank", "Count", "Share", "IsAccepted", "Origin", "IsPinned")
        VALUES (@EntryId, @German, @GermanKey, @Rank, @Count, @Share, true, 'derived', false)
        """;

    private const string UpdateMeaning = """
        UPDATE "unshackled-word"."VocabMeanings"
        SET "GermanKey" = @GermanKey
          , "Rank" = @Rank
          , "Count" = @Count
          , "Share" = @Share
          , "IsAccepted" = true
        WHERE 1=1
            AND "Id" = @Id
        """;

    private const string DeleteMeanings = """
        DELETE FROM "unshackled-word"."VocabMeanings"
        WHERE 1=1
            AND "Id" = ANY(@Ids)
        """;

    private const string BumpVersion = """
        UPDATE "unshackled-word"."VocabEntries"
        SET "ContentVersion" = "ContentVersion" + 1
          , "UpdatedAt" = now()
          , "UpdatedByUserId" = @UserId
        WHERE 1=1
            AND "Id" = @EntryId
        RETURNING "ContentVersion"
        """;

    private const string InsertChange = """
        INSERT INTO "unshackled-word"."VocabEntryChanges"
            ("VocabEntryId", "ContentVersion", "Kind", "Before", "After", "ChangedByUserId")
        VALUES (@EntryId, @ContentVersion, 'meanings', @Before::jsonb, @After::jsonb, @UserId)
        """;

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly VocabOptions _options;

    public VocabMeaningDeriver(IDbConnectionFactory connectionFactory, IOptions<VocabOptions> options)
    {
        _connectionFactory = connectionFactory;
        _options = options.Value;
    }

    /// <summary>True when the course code exists (the endpoint rejects unknown codes before starting a job).</summary>
    public async Task<bool> CourseExistsAsync(string code, CancellationToken token)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(token);

        return await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(SelectCourseTrack, new { Code = code }, cancellationToken: token)) is not null;
    }

    public async Task<SeedStepReport> DeriveAsync(VocabMeaningScope scope, bool dryRun, int? userId, IProgress<string>? progress, CancellationToken token)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(token);

        return await DeriveAsync(connection, null, _options, scope, dryRun, userId, progress);
    }

    /// <summary>
    /// The same run on an open connection. With a transaction everything happens inside it (tests roll it back); without one each batch of
    /// <see cref="BatchSize"/> entries gets its own. A dry run writes nothing.
    /// </summary>
    public static async Task<SeedStepReport> DeriveAsync(IDbConnection connection, IDbTransaction? transaction, VocabOptions options, VocabMeaningScope scope, bool dryRun, int? userId, IProgress<string>? progress)
    {
        var track = scope.Course is null
            ? null
            : await connection.QuerySingleOrDefaultAsync<string>(SelectCourseTrack, new { Code = scope.Course }, transaction)
                ?? throw new ArgumentException($"Unknown vocab course '{scope.Course}'.", nameof(scope));

        var entries = (await connection.QueryAsync<EntryRow>(SelectEntries, new { Track = track, scope.RankFrom, scope.RankTo }, transaction, TimeoutSeconds)).ToList();
        progress?.Report($"Deriving meanings for {entries.Count} entries{(dryRun ? " (dry run)" : string.Empty)}");

        var stats = new Stats();
        var topChanged = new List<string>();

        foreach (var batch in entries.Chunk(BatchSize))
        {
            var ids = batch.Select(e => e.Id).ToArray();
            var renderings = (await connection.QueryAsync<RenderingRow>(SelectRenderings, new { Ids = ids }, transaction, TimeoutSeconds))
                .ToLookup(r => r.EntryId);
            var meanings = (await connection.QueryAsync<MeaningRow>(SelectMeanings, new { Ids = ids }, transaction, TimeoutSeconds)).ToLookup(m => m.EntryId);

            var ownTransaction = !dryRun && transaction is null ? connection.BeginTransaction() : null;
            var tx = transaction ?? ownTransaction;

            foreach (var entry in batch)
            {
                var candidates = renderings[entry.Id].Select(r => new VocabRenderingCandidate(r.GermanKey, Truncate(r.German), r.GermanPos, r.LinkCount)).ToList();
                var existing = meanings[entry.Id].Select(m => new VocabMeaningRow(m.Id, m.German, m.GermanKey, m.Rank, m.Count, m.Share, m.IsAccepted, m.Origin, m.IsPinned)).ToList();
                var plan = VocabMeaningSelector.Plan(existing, candidates, options, VocabMeaningSelector.ClassifyEntry(entry.Pos));

                stats.Processed++;
                if (!plan.HasWrites)
                {
                    continue;
                }

                stats.Changed++;
                stats.Inserted += plan.Insert.Count;
                stats.Removed += plan.DeleteIds.Count;
                stats.Updated += plan.Update.Count;
                if (plan.BumpsVersion)
                {
                    stats.Bumped++;
                }

                if (plan.TopChanged)
                {
                    stats.TopChanged++;
                    topChanged.Add($"{entry.Id} {entry.Lemma}: {plan.OldTop ?? "-"} -> {plan.NewTop ?? "-"}");
                }

                if (!dryRun)
                {
                    await WriteAsync(connection, tx!, entry.Id, plan, userId);
                }
            }

            ownTransaction?.Commit();
            ownTransaction?.Dispose();
        }

        progress?.Report($"Meanings {(dryRun ? "would change" : "changed")} for {stats.Changed} of {stats.Processed} entries: +{stats.Inserted} -{stats.Removed} ~{stats.Updated}, {stats.Bumped} version bumps, {stats.TopChanged} top meanings");

        var report = new SeedStepReport { Kind = Kind, Action = Action };
        report.Counts["dryRun"] = dryRun ? 1 : 0;
        report.Counts["entriesProcessed"] = stats.Processed;
        report.Counts["entriesChanged"] = stats.Changed;
        report.Counts["contentVersionBumps"] = stats.Bumped;
        report.Counts["meaningsInserted"] = stats.Inserted;
        report.Counts["meaningsRemoved"] = stats.Removed;
        report.Counts["meaningsUpdated"] = stats.Updated;
        report.Counts["topMeaningChanged"] = stats.TopChanged;
        report.Lists["topMeaningChanged"] = new SeedReportList { Total = topChanged.Count, Items = topChanged.Take(SeedStepReport.MaxListItems).ToList() };

        return report;
    }

    private static async Task WriteAsync(IDbConnection connection, IDbTransaction tx, int entryId, VocabMeaningPlan plan, int? userId)
    {
        if (plan.DeleteIds.Count > 0)
        {
            await connection.ExecuteAsync(DeleteMeanings, new { Ids = plan.DeleteIds.ToArray() }, tx);
        }

        foreach (var (id, m) in plan.Update)
        {
            await connection.ExecuteAsync(UpdateMeaning, new { Id = id, m.GermanKey, m.Rank, m.Count, m.Share }, tx);
        }

        foreach (var m in plan.Insert)
        {
            await connection.ExecuteAsync(InsertMeaning, new { EntryId = entryId, m.German, m.GermanKey, m.Rank, m.Count, m.Share }, tx);
        }

        if (!plan.BumpsVersion)
        {
            return;
        }

        var version = await connection.QuerySingleAsync<int>(BumpVersion, new { EntryId = entryId, UserId = userId }, tx);
        await connection.ExecuteAsync(InsertChange, new
        {
            EntryId = entryId,
            ContentVersion = version,
            Before = JsonSerializer.Serialize(new { meanings = plan.BeforeAccepted }),
            After = JsonSerializer.Serialize(new { meanings = plan.AfterAccepted }),
            UserId = userId
        }, tx);
    }

    private static string Truncate(string value) => value.Length <= MaxGermanLength ? value : value[..MaxGermanLength];

    private sealed class Stats
    {
        public int Processed;
        public int Changed;
        public int Bumped;
        public int Inserted;
        public int Removed;
        public int Updated;
        public int TopChanged;
    }

    private sealed class EntryRow
    {
        public int Id { get; init; }
        public string Lemma { get; init; } = string.Empty;
        public string? Pos { get; init; }
    }

    private sealed class RenderingRow
    {
        public int EntryId { get; init; }
        public string GermanKey { get; init; } = string.Empty;
        public string German { get; init; } = string.Empty;
        public string? GermanPos { get; init; }
        public int LinkCount { get; init; }
    }

    private sealed class MeaningRow
    {
        public int EntryId { get; init; }
        public int Id { get; init; }
        public string German { get; init; } = string.Empty;
        public string? GermanKey { get; init; }
        public int Rank { get; init; }
        public int? Count { get; init; }
        public decimal? Share { get; init; }
        public bool IsAccepted { get; init; }
        public string Origin { get; init; } = string.Empty;
        public bool IsPinned { get; init; }
    }
}
