using System.Data;
using System.Text.Json;
using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Vocab;
using UnshackledWord.Application.Features.Vocab.Roots;
using UnshackledWord.Domain.Models.Vocab;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Infrastructure.Vocab;

/// <summary>Result of <see cref="VocabRootEditor.SetParentAsync(int, int?, string?, int?, CancellationToken)"/>.</summary>
public sealed record VocabRootParentOutcome(VocabEditStatus Status, string? Message = null);

/// <summary>
/// Manual root edits of the vocab editor (roadmap 0030.5). <see cref="SetEntryRootAsync(IDbConnection, IDbTransaction, int, SetVocabEntryRootRequest, int?)"/>
/// writes the root of the entry's main key with origin <c>manual</c> after the rules of the agent run (<see cref="VocabRootAnswerValidator"/>) and logs
/// a <c>VocabEntryChanges</c> row of kind <c>root</c> at the current content version: a root is extra information, it never bumps
/// <c>ContentVersion</c> and never reaches the learners' change list. <see cref="SetParentAsync(IDbConnection, IDbTransaction, int, int?, string?, int?)"/>
/// sets the parent of a root (same track, no cycle, at most <see cref="MaxParentLinks"/> links). The static methods run on the caller's connection
/// and transaction (tests roll it back); the instance methods open and commit their own.
/// </summary>
public sealed class VocabRootEditor
{
    /// <summary>The longest parent chain: a family reads at most 4 levels below its head (see <c>VocabRootRepository.FamilyCondition</c>).</summary>
    public const int MaxParentLinks = 4;

    private const int WalkLimit = 10;

    private const string SelectEntry = """
        SELECT e."Id"
             , e."Track"::text AS "Track"
             , e."Lemma"
             , e."ContentVersion"
             , (SELECT vs."StrongsKey"
                FROM "unshackled-word"."VocabStrongs" vs
                WHERE vs."VocabEntryId" = e."Id"
                ORDER BY vs."Occurrences" DESC, vs."StrongsKey"
                LIMIT 1) AS "MainKey"
        FROM "unshackled-word"."VocabEntries" e
        WHERE e."Id" = @Id
        """;

    private const string SelectCurrent = """
        SELECT er."VocabRootId" AS "RootId", er."Root", er."RootStrongsKey" AS "RootKey", er."Prefixes", er."PrefixKeys", er."Origin"
        FROM "unshackled-word"."VocabEntryRoots" er
        WHERE er."VocabEntryId" = @Id
        """;

    private const string SelectRootById = """
        SELECT r."Id", r."Track"::text AS "Track", r."Root", r."RootNormalized", r."Homograph", r."StrongsKey", r."ParentRootId"
        FROM "unshackled-word"."VocabRoots" r
        WHERE r."Id" = @Id
        """;

    private const string SelectRootByKey = """
        SELECT r."Id", r."Track"::text AS "Track", r."Root", r."RootNormalized", r."Homograph", r."StrongsKey", r."ParentRootId"
        FROM "unshackled-word"."VocabRoots" r
        WHERE r."StrongsKey" = @Key
        """;

    private const string SelectRootsByText = """
        SELECT r."Id", r."Track"::text AS "Track", r."Root", r."RootNormalized", r."Homograph", r."StrongsKey", r."ParentRootId"
        FROM "unshackled-word"."VocabRoots" r
        WHERE r."Track" = @Track AND r."RootNormalized" = @Normalized
        ORDER BY r."Homograph"
        """;

    private const string SelectLemmaOfKey = """
        SELECT e."Lemma"
        FROM "unshackled-word"."VocabStrongs" vs
            JOIN "unshackled-word"."VocabEntries" e ON e."Id" = vs."VocabEntryId"
        WHERE vs."StrongsKey" = @Key
        ORDER BY e."RetiredAt" NULLS FIRST, e."Id"
        LIMIT 1
        """;

    // the parents of a root upwards, the root itself first; capped, so existing bad data cannot loop
    private const string SelectUp = """
        WITH RECURSIVE up ("Id", "Root", "ParentRootId", "Depth") AS
        (
            SELECT r."Id", r."Root", r."ParentRootId", 0
            FROM "unshackled-word"."VocabRoots" r
            WHERE r."Id" = @Id
            UNION ALL
            SELECT p."Id", p."Root", p."ParentRootId", u."Depth" + 1
            FROM "unshackled-word"."VocabRoots" p
                JOIN up u ON p."Id" = u."ParentRootId"
            WHERE u."Depth" < @Limit
        )
        SELECT up."Id", up."Root"
        FROM up
        ORDER BY up."Depth"
        """;

    private const string SelectDownDepth = """
        WITH RECURSIVE down ("Id", "Depth") AS
        (
            SELECT c."Id", 1
            FROM "unshackled-word"."VocabRoots" c
            WHERE c."ParentRootId" = @Id
            UNION ALL
            SELECT c."Id", d."Depth" + 1
            FROM "unshackled-word"."VocabRoots" c
                JOIN down d ON c."ParentRootId" = d."Id"
            WHERE d."Depth" < @Limit
        )
        SELECT COALESCE(max(down."Depth"), 0)::int
        FROM down
        """;

    private const string UpdateParent = """
        UPDATE "unshackled-word"."VocabRoots"
        SET "ParentRootId" = @ParentRootId
          , "UpdatedAt" = now()
          , "UpdatedByUserId" = @UserId
        WHERE "Id" = @Id
        """;

    private const string InsertChange = """
        INSERT INTO "unshackled-word"."VocabEntryChanges"
            ("VocabEntryId", "ContentVersion", "Kind", "Before", "After", "ChangedByUserId")
        VALUES (@EntryId, @ContentVersion, 'root', @Before::jsonb, @After::jsonb, @UserId)
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public VocabRootEditor(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<VocabEditOutcome> SetEntryRootAsync(int id, SetVocabEntryRootRequest request, int? userId, CancellationToken token = default)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();
        var outcome = await SetEntryRootAsync(connection, transaction, id, request, userId);

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

    public async Task<VocabRootParentOutcome> SetParentAsync(int id, int? parentRootId, string? parentStrongsKey, int? userId, CancellationToken token = default)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();
        var outcome = await SetParentAsync(connection, transaction, id, parentRootId, parentStrongsKey, userId);

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

    public static async Task<VocabEditOutcome> SetEntryRootAsync(IDbConnection c, IDbTransaction t, int id, SetVocabEntryRootRequest request, int? userId)
    {
        var entry = await c.QueryFirstOrDefaultAsync<EntryRow>(SelectEntry, new { Id = id }, t);

        if (entry is null)
        {
            return VocabEditOutcome.NotFound($"Vocab entry {id} does not exist.");
        }

        if (entry.MainKey is null)
        {
            return VocabEditOutcome.Conflict("The entry has no Strong's key, so it has no root.");
        }

        var track = entry.Track[0];
        var picked = request.RootId is { } rootId ? await c.QueryFirstOrDefaultAsync<VocabRootRow>(SelectRootById, new { Id = rootId }, t) : null;

        if (request.RootId is not null && picked is null)
        {
            return VocabEditOutcome.NotFound($"Root {request.RootId} does not exist.");
        }

        if (picked is not null && picked.Track != entry.Track)
        {
            return VocabEditOutcome.Invalid("The root belongs to the other track.");
        }

        var context = await VocabRootRunRepository.LoadContextAsync(c, t, track);
        var input = new VocabRootInput(entry.Id, entry.Lemma, entry.MainKey, null, null, null);
        var answer = new VocabRootAnswerRow(
            entry.Id,
            request.Simple,
            request.PrefixKeys,
            picked is null ? request.RootKey : picked.StrongsKey,
            picked is null ? request.RootLetters : (track == 'H' ? picked.Root : null),
            "high",
            null);

        // the same rules as the agent run; a manual save is refused for a rule break and also when the parts do not plausibly form the word
        var verdict = VocabRootAnswerValidator.Check(track, input, answer, context, false);

        if (verdict.Plan is not { } plan)
        {
            return VocabEditOutcome.Invalid(verdict.Reason ?? "The root is not valid.");
        }

        var (resolvedId, problem) = await ResolveRootAsync(c, t, track, picked, plan, request.Homograph, userId);

        if (resolvedId is null)
        {
            return VocabEditOutcome.Invalid(problem!);
        }

        var before = await c.QueryFirstOrDefaultAsync<CurrentRow>(SelectCurrent, new { Id = id }, t);

        await VocabRootRepository.UpsertWordRootAsync(c, t, new VocabWordRootUpsert
        {
            Lang = track.ToString(),
            StrongsKey = entry.MainKey,
            VocabRootId = resolvedId.Value,
            Prefixes = plan.PrefixKeys.Length == 0 ? null : plan.Prefixes,
            PrefixKeys = plan.PrefixKeys,
            Origin = "manual",
            UserId = userId,
        });

        await c.ExecuteAsync(InsertChange, new
        {
            EntryId = entry.Id,
            entry.ContentVersion,
            Before = before is null ? null : JsonSerializer.Serialize(new { rootId = before.RootId, root = before.Root, rootKey = before.RootKey, prefixes = before.Prefixes, prefixKeys = before.PrefixKeys ?? [], origin = before.Origin }),
            After = JsonSerializer.Serialize(new { rootId = resolvedId, root = plan.RootText, rootKey = plan.RootKey, prefixes = plan.Prefixes, prefixKeys = plan.PrefixKeys, origin = "manual" }),
            UserId = userId,
        }, t);

        return VocabEditOutcome.Ok(new VocabEditResponse { EntryId = entry.Id, ContentVersion = entry.ContentVersion, Bumped = false });
    }

    public static async Task<VocabRootParentOutcome> SetParentAsync(IDbConnection c, IDbTransaction t, int id, int? parentRootId, string? parentStrongsKey, int? userId)
    {
        var root = await c.QueryFirstOrDefaultAsync<VocabRootRow>(SelectRootById, new { Id = id }, t);

        if (root is null)
        {
            return new VocabRootParentOutcome(VocabEditStatus.NotFound, $"Root {id} does not exist.");
        }

        if (parentRootId is null && string.IsNullOrWhiteSpace(parentStrongsKey))
        {
            await c.ExecuteAsync(UpdateParent, new { Id = id, ParentRootId = (int?)null, UserId = userId }, t);

            return new VocabRootParentOutcome(VocabEditStatus.Ok);
        }

        VocabRootRow? parent;

        if (parentRootId is { } pid)
        {
            parent = await c.QueryFirstOrDefaultAsync<VocabRootRow>(SelectRootById, new { Id = pid }, t);

            if (parent is null)
            {
                return new VocabRootParentOutcome(VocabEditStatus.NotFound, $"Root {pid} does not exist.");
            }
        }
        else
        {
            var key = parentStrongsKey!.Trim().ToUpperInvariant();
            parent = await c.QueryFirstOrDefaultAsync<VocabRootRow>(SelectRootByKey, new { Key = key }, t);

            if (parent is null)
            {
                // a Greek word without a root row yet: its root is the word itself
                var lemma = key.StartsWith('G') ? await c.QueryFirstOrDefaultAsync<string>(SelectLemmaOfKey, new { Key = key }, t) : null;

                if (lemma is null)
                {
                    return new VocabRootParentOutcome(VocabEditStatus.Invalid, $"No Greek vocab entry has the key {key}.");
                }

                var newId = await EnsureGreekRootAsync(c, t, key, lemma, userId);
                parent = await c.QueryFirstAsync<VocabRootRow>(SelectRootById, new { Id = newId }, t);
            }
        }

        if (parent.Track != root.Track)
        {
            return new VocabRootParentOutcome(VocabEditStatus.Invalid, "The parent root must belong to the same track.");
        }

        if (parent.Id == root.Id)
        {
            return new VocabRootParentOutcome(VocabEditStatus.Invalid, $"{root.Root} cannot be its own parent.");
        }

        var up = (await c.QueryAsync<(int Id, string Root)>(SelectUp, new { Id = parent.Id, Limit = WalkLimit }, t)).ToList();

        if (up.Any(x => x.Id == root.Id))
        {
            var path = string.Join(" → ", up.TakeWhile(x => x.Id != root.Id).Select(x => x.Root).Append(root.Root));

            return new VocabRootParentOutcome(VocabEditStatus.Invalid, $"{parent.Root} already sits below {root.Root} ({path}); making it the parent would be a cycle.");
        }

        var down = await c.QuerySingleAsync<int>(SelectDownDepth, new { Id = root.Id, Limit = WalkLimit }, t);

        // links: the root to its parent (1), the parent upwards (up.Count - 1) and the sub-roots below the root (down)
        if (up.Count + down > MaxParentLinks)
        {
            return new VocabRootParentOutcome(VocabEditStatus.Invalid, $"The chain would be {up.Count + down} levels long; a family has at most {MaxParentLinks} levels of parents.");
        }

        await c.ExecuteAsync(UpdateParent, new { Id = id, ParentRootId = (int?)parent.Id, UserId = userId }, t);

        return new VocabRootParentOutcome(VocabEditStatus.Ok);
    }

    /// <summary>The root row a plan writes to: the picked one, the Greek root of the key (created from the plan when missing) or the Hebrew root of the letters.</summary>
    private static async Task<(int? Id, string? Problem)> ResolveRootAsync(IDbConnection c, IDbTransaction t, char track, VocabRootRow? picked, VocabRootPlan plan, short? homograph, int? userId)
    {
        if (picked is not null)
        {
            return (picked.Id, null);
        }

        if (track == 'G')
        {
            return (await EnsureGreekRootAsync(c, t, plan.RootKey!, plan.RootText, userId), null);
        }

        // Hebrew: a verb key identifies its root
        if (plan.RootKey is not null && await c.QueryFirstOrDefaultAsync<VocabRootRow>(SelectRootByKey, new { Key = plan.RootKey }, t) is { } byKey)
        {
            return (byKey.Id, null);
        }

        var slots = (await c.QueryAsync<VocabRootRow>(SelectRootsByText, new { Track = "H", Normalized = VocabText.Normalize(plan.RootText) }, t)).ToList();

        if (homograph is null && slots.Count > 1)
        {
            return (null, $"Several roots have the consonants {plan.RootText}; pick one from the list or give the homograph number.");
        }

        if (homograph is null && slots.Count == 1)
        {
            return (slots[0].Id, null);
        }

        // the upsert below would move the slot's key to the new verb and leave the slot's own verb without its root
        if (homograph is { } number
            && plan.RootKey is not null
            && slots.FirstOrDefault(s => s.Homograph == number) is { StrongsKey: { } slotKey }
            && slotKey != plan.RootKey)
        {
            return (null, $"{plan.RootText} {number} is the root of {slotKey}; give a free homograph number for {plan.RootKey}.");
        }

        var id = await VocabRootRepository.UpsertRootAsync(c, t, new VocabRootUpsert
        {
            Track = "H",
            Root = plan.RootText,
            Homograph = homograph ?? 1,
            StrongsKey = plan.RootKey,
            UserId = userId,
        });

        return (id, null);
    }

    /// <summary>The root row of a Greek word's key; created with the given text (next free homograph for the same text) when none exists.</summary>
    private static async Task<int> EnsureGreekRootAsync(IDbConnection c, IDbTransaction t, string key, string text, int? userId)
    {
        if (await c.QueryFirstOrDefaultAsync<VocabRootRow>(SelectRootByKey, new { Key = key }, t) is { } existing)
        {
            return existing.Id;
        }

        var slots = (await c.QueryAsync<VocabRootRow>(SelectRootsByText, new { Track = "G", Normalized = VocabText.Normalize(text) }, t)).ToList();
        var homograph = (short)1;

        while (slots.Any(s => s.Homograph == homograph))
        {
            homograph++;
        }

        return await VocabRootRepository.UpsertRootAsync(c, t, new VocabRootUpsert
        {
            Track = "G",
            Root = text,
            Homograph = homograph,
            StrongsKey = key,
            UserId = userId,
        });
    }

    private sealed class EntryRow
    {
        public int Id { get; init; }
        public string Track { get; init; } = string.Empty;
        public string Lemma { get; init; } = string.Empty;
        public int ContentVersion { get; init; }
        public string? MainKey { get; init; }
    }

    private sealed class CurrentRow
    {
        public int RootId { get; init; }
        public string Root { get; init; } = string.Empty;
        public string? RootKey { get; init; }
        public string? Prefixes { get; init; }
        public string[]? PrefixKeys { get; init; }
        public string Origin { get; init; } = string.Empty;
    }
}
