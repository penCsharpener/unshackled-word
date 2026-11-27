using System.Data;
using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Vocab;
using UnshackledWord.Application.Features.Vocab.Roots;
using UnshackledWord.Domain.Models.Vocab;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Infrastructure.Vocab;

/// <summary>An entry that waits for a root: no row for its main key (or a prefill row with the check option).</summary>
public sealed class VocabRootCandidate
{
    public int Id { get; set; }
    public string Lemma { get; set; } = string.Empty;
    public string? Pos { get; set; }
    public int Rank { get; set; }
    public string MainKey { get; set; } = string.Empty;
    public string? Gloss { get; set; }
    public string? Lexicon { get; set; }
    public string? ExistingOrigin { get; set; }
    public string? ExistingPrefixes { get; set; }
    public string[]? ExistingPrefixKeys { get; set; }
    public string? ExistingRoot { get; set; }
    public string? ExistingRootKey { get; set; }

    public VocabRootInput ToInput(string? problem = null) => new(
        Id,
        Lemma,
        MainKey,
        Pos,
        Gloss,
        Lexicon,
        ExistingOrigin is null ? null : new VocabRootExisting(ExistingOrigin, ExistingPrefixes, ExistingPrefixKeys ?? [], ExistingRoot ?? string.Empty, ExistingRootKey),
        problem);
}

/// <summary>Reads for the root agent run (roadmap 0030.4) and the run report; the root writes are <see cref="VocabRootRunWriter"/>.</summary>
public sealed class VocabRootRunRepository
{
    private const string SelectCandidates = """
        WITH main_keys AS
        (
            SELECT DISTINCT ON (vs."VocabEntryId")
                   vs."VocabEntryId"
                 , vs."Lang"::text AS "Lang"
                 , vs."StrongsKey" AS "MainKey"
            FROM "unshackled-word"."VocabStrongs" vs
            ORDER BY vs."VocabEntryId", vs."Occurrences" DESC, vs."StrongsKey"
        )
        SELECT e."Id"
             , e."Lemma"
             , e."Pos"
             , e."FrequencyRank" AS "Rank"
             , mk."MainKey"
             , (SELECT COALESCE(m."GermanOverride", m."German")
                FROM "unshackled-word"."VocabMeanings" m
                WHERE m."VocabEntryId" = e."Id" AND m."IsAccepted"
                ORDER BY m."Rank", m."Id"
                LIMIT 1) AS "Gloss"
             , (SELECT l."Lexicon"
                FROM "unshackled-word"."StepStrongsLexicon" l
                WHERE 1=1
                    AND l."Number" = regexp_replace(mk."MainKey", '\D', '', 'g')::int
                    AND upper(COALESCE(l."Extra", '')) = regexp_replace(mk."MainKey", '^[GH]\d+', '')
                    AND (l."LanguageId" = 2) = (mk."Lang" = 'G')
                ORDER BY l."Id"
                LIMIT 1) AS "Lexicon"
             , sr."Origin" AS "ExistingOrigin"
             , sr."Prefixes" AS "ExistingPrefixes"
             , sr."PrefixKeys" AS "ExistingPrefixKeys"
             , r."Root" AS "ExistingRoot"
             , r."StrongsKey" AS "ExistingRootKey"
        FROM main_keys mk
            JOIN "unshackled-word"."VocabEntries" e ON e."Id" = mk."VocabEntryId"
            LEFT JOIN "unshackled-word"."VocabStrongsRoots" sr ON sr."Lang" = mk."Lang" AND sr."StrongsKey" = mk."MainKey"
            LEFT JOIN "unshackled-word"."VocabRoots" r ON r."Id" = sr."VocabRootId"
        WHERE 1=1
            AND e."Track" = @Track
            AND e."RetiredAt" IS NULL
            AND NOT e."IsMorpheme"
            AND NOT e."IsProperName"
            AND e."FrequencyRank" >= @FromRank
            AND (@ToRank::int4 IS NULL OR e."FrequencyRank" <= @ToRank)
            AND (sr."StrongsKey" IS NULL OR (@CheckPrefill AND sr."Origin" = 'prefill'))
        ORDER BY e."FrequencyRank", e."Id"
        LIMIT @Limit
        """;

    private const string SelectHebrewVerbs = """
        SELECT vs."StrongsKey", e."Lemma"
        FROM "unshackled-word"."VocabStrongs" vs
            JOIN "unshackled-word"."VocabEntries" e ON e."Id" = vs."VocabEntryId"
        WHERE 1=1
            AND vs."Lang" = 'H'
            AND e."Pos" IN ('H:V', 'A:V')
            AND e."RetiredAt" IS NULL
        """;

    private const string UpsertItem = """
        INSERT INTO "unshackled-word"."VocabRootRunItems" ("AgentRunId", "VocabEntryId", "StrongsKey", "Kind", "Reason", "Answer")
        VALUES (@RunId, @EntryId, @StrongsKey, @Kind, @Reason, @Answer)
        ON CONFLICT ("AgentRunId", "VocabEntryId") DO UPDATE
            SET "StrongsKey" = EXCLUDED."StrongsKey"
              , "Kind"       = EXCLUDED."Kind"
              , "Reason"     = EXCLUDED."Reason"
              , "Answer"     = EXCLUDED."Answer"
              , "CreatedAt"  = now()
        """;

    private const string DeleteItem = """
        DELETE FROM "unshackled-word"."VocabRootRunItems" WHERE "AgentRunId" = @RunId AND "VocabEntryId" = @EntryId
        """;

    private const string SelectItems = """
        SELECT i."VocabEntryId" AS "EntryId"
             , e."Lemma"
             , i."StrongsKey"
             , i."Kind"
             , i."Reason"
             , i."Answer"
        FROM "unshackled-word"."VocabRootRunItems" i
            JOIN "unshackled-word"."VocabEntries" e ON e."Id" = i."VocabEntryId"
        WHERE i."AgentRunId" = @RunId
        ORDER BY CASE i."Kind" WHEN 'rejected' THEN 0 WHEN 'flagged' THEN 1 ELSE 2 END, e."FrequencyRank", e."Id"
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public VocabRootRunRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<VocabRootCandidate>> SelectCandidatesAsync(string track, int fromRank, int? toRank, int limit, bool checkPrefill, CancellationToken token = default)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(token);

        return (await connection.QueryAsync<VocabRootCandidate>(new CommandDefinition(SelectCandidates, new { Track = track, FromRank = fromRank, ToRank = toRank, Limit = limit, CheckPrefill = checkPrefill }, commandTimeout: 120, cancellationToken: token))).ToList();
    }

    /// <summary>The lookups of the validator: the Greek lexicon (with the vocab lemmas) and the Hebrew/Aramaic verb keys with their consonants.</summary>
    public async Task<VocabRootValidationContext> LoadContextAsync(char track, CancellationToken token = default)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(token);

        return await LoadContextAsync(connection, null, track, token);
    }

    /// <summary>Same on the caller's connection and transaction (the manual root editor, roadmap 0030.5).</summary>
    public static async Task<VocabRootValidationContext> LoadContextAsync(IDbConnection connection, IDbTransaction? transaction, char track, CancellationToken token = default)
    {
        if (track == 'G')
        {
            return new VocabRootValidationContext(await VocabRootPrefillService.LoadLexiconAsync(connection, transaction));
        }

        var verbs = await connection.QueryAsync<(string StrongsKey, string Lemma)>(new CommandDefinition(SelectHebrewVerbs, transaction: transaction, cancellationToken: token));

        return new VocabRootValidationContext([], verbs.GroupBy(v => v.StrongsKey).ToDictionary(g => g.Key, g => VocabRootPrefill.Consonants(g.First().Lemma)));
    }

    public async Task SetItemAsync(int runId, int entryId, string strongsKey, string kind, string reason, string? answer, CancellationToken token = default)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(token);

        await connection.ExecuteAsync(new CommandDefinition(UpsertItem, new { RunId = runId, EntryId = entryId, StrongsKey = strongsKey, Kind = kind, Reason = reason, Answer = answer }, cancellationToken: token));
    }

    /// <summary>A written entry no longer belongs in the report of a resumed run.</summary>
    public async Task ClearItemAsync(int runId, int entryId, CancellationToken token = default)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(token);

        await connection.ExecuteAsync(new CommandDefinition(DeleteItem, new { RunId = runId, EntryId = entryId }, cancellationToken: token));
    }

    public async Task<VocabRootRunReportResponse> GetReportAsync(int runId, CancellationToken token = default)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(token);
        var items = (await connection.QueryAsync<VocabRootRunItem>(new CommandDefinition(SelectItems, new { RunId = runId }, cancellationToken: token))).ToList();

        return new VocabRootRunReportResponse
        {
            RunId = runId,
            Rejected = items.Count(i => i.Kind == "rejected"),
            Flagged = items.Count(i => i.Kind == "flagged"),
            Replaced = items.Count(i => i.Kind == "replaced"),
            Items = items,
        };
    }
}

public enum VocabRootWriteStatus
{
    /// <summary>A new row (the key had none).</summary>
    Written,

    /// <summary>A prefill row replaced by a different root.</summary>
    Replaced,

    /// <summary>A prefill row the answer agrees with; it becomes an agent row.</summary>
    Confirmed,

    /// <summary>Not written: the row is manual, or the root is ambiguous (several roots with these consonants).</summary>
    Skipped,
}

public sealed record VocabRootWriteResult(VocabRootWriteStatus Status, string? Reason = null);

/// <summary>Writes an accepted answer (roadmap 0030.4): finds or creates the root and sets the word's row with origin <c>agent</c> and the run id.</summary>
public static class VocabRootRunWriter
{
    private const string SelectRootByKey = """
        SELECT r."Id" FROM "unshackled-word"."VocabRoots" r WHERE r."StrongsKey" = @Key
        """;

    private const string SelectRootsByText = """
        SELECT r."Id", r."Homograph", r."StrongsKey"
        FROM "unshackled-word"."VocabRoots" r
        WHERE r."Track" = @Track AND r."RootNormalized" = @Normalized
        ORDER BY r."Homograph"
        """;

    private sealed record RootSlot(int Id, short Homograph, string? StrongsKey);

    public static async Task<VocabRootWriteResult> ApplyAsync(IDbConnection connection, IDbTransaction? transaction, char track, VocabRootInput entry, VocabRootPlan plan, int runId, int? aiUserId, CancellationToken token = default)
    {
        if (entry.Existing?.Origin == "manual")
        {
            return new VocabRootWriteResult(VocabRootWriteStatus.Skipped, "Row is manual.");
        }

        var trackText = track.ToString();
        var normalized = VocabText.Normalize(plan.RootText);
        var slots = (await connection.QueryAsync<RootSlot>(new CommandDefinition(SelectRootsByText, new { Track = trackText, Normalized = normalized }, transaction, cancellationToken: token))).ToList();
        int? rootId = plan.RootKey is null
            ? null
            : await connection.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(SelectRootByKey, new { Key = plan.RootKey }, transaction, cancellationToken: token));

        if (rootId is null && plan.RootKey is null)
        {
            // Hebrew root without a verb: reuse the only root with these consonants, else create it; several are the maintainer's call
            if (slots.Count > 1)
            {
                return new VocabRootWriteResult(VocabRootWriteStatus.Skipped, $"Several roots have the consonants {plan.RootText}; pick one in the editor.");
            }

            rootId = slots.Count == 1 ? slots[0].Id : null;
        }

        if (rootId is null)
        {
            var homograph = (short)1;

            while (slots.Any(s => s.Homograph == homograph))
            {
                homograph++;
            }

            rootId = await VocabRootRepository.UpsertRootAsync(connection, transaction, new VocabRootUpsert
            {
                Track = trackText,
                Root = plan.RootText,
                Homograph = homograph,
                StrongsKey = plan.RootKey,
                UserId = aiUserId,
            }, token);
        }

        var prefixes = plan.PrefixKeys.Length == 0 ? null : plan.Prefixes;

        var written = await VocabRootRepository.UpsertWordRootAsync(connection, transaction, new VocabWordRootUpsert
        {
            Lang = trackText,
            StrongsKey = entry.MainKey,
            VocabRootId = rootId.Value,
            Prefixes = prefixes,
            PrefixKeys = plan.PrefixKeys,
            Origin = "agent",
            AgentRunId = runId,
            UserId = aiUserId,
        }, token);

        if (!written)
        {
            // the maintainer saved a manual row after the run loaded its candidates: the repository kept it
            return new VocabRootWriteResult(VocabRootWriteStatus.Skipped, "Row is manual.");
        }

        if (entry.Existing is not { } existing)
        {
            return new VocabRootWriteResult(VocabRootWriteStatus.Written);
        }

        var sameRoot = plan.RootKey is not null
            ? existing.RootKey == plan.RootKey
            : VocabText.Normalize(existing.Root) == normalized;

        return sameRoot && existing.PrefixKeys.SequenceEqual(plan.PrefixKeys)
            ? new VocabRootWriteResult(VocabRootWriteStatus.Confirmed)
            : new VocabRootWriteResult(VocabRootWriteStatus.Replaced, $"Prefill: {Describe(existing.Prefixes, existing.Root, existing.RootKey)}; run: {Describe(prefixes, plan.RootText, plan.RootKey)}.");
    }

    public static string Describe(string? prefixes, string root, string? rootKey) =>
        $"{(string.IsNullOrEmpty(prefixes) ? string.Empty : prefixes + " + ")}{root}{(rootKey is null ? string.Empty : " " + rootKey)}";
}
