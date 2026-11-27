using System.Data;
using Dapper;
using UnshackledWord.Domain.Models.Vocab;

namespace UnshackledWord.Infrastructure.Seed.Vocab;

/// <summary>What the root files would do (or did) to the database; the numbers behind the import preview (roadmap 0030.7f).</summary>
public sealed class VocabRootSeedCounts
{
    public int RootsNew { get; set; }
    public int RootsChanged { get; set; }
    public int WordRootsNew { get; set; }
    public int WordRootsChanged { get; set; }

    /// <summary>Word rows a local <c>manual</c> row keeps against a non-manual seed row.</summary>
    public int ManualKept { get; set; }

    /// <summary>Seed rows that could not be applied (root key held by another root, word row whose root is not in the files or the database).</summary>
    public int Skipped { get; set; }

    public int Unchanged { get; set; }
}

/// <summary>
/// Roots part of the vocab seed import (roadmap 0030.7). Roots are matched by <c>(Track, RootNormalized, Homograph)</c> in two passes (rows, then parents),
/// word rows by <c>(Lang, StrongsKey)</c>. A local <c>manual</c> word row is never overwritten by a non-manual seed row. A seed root without a parent
/// leaves a local parent alone. No user ids and no run ids come from the seed; written rows have none.
/// </summary>
public static class VocabRootSeedSync
{
    private const string SelectRoots = """
        SELECT r."Id", r."Track", r."Root", r."RootNormalized", r."Homograph"::int AS "Homograph", r."StrongsKey", r."ParentRootId"
        FROM "unshackled-word"."VocabRoots" r
        """;

    private const string SelectWordRoots = """
        SELECT s."Lang", s."StrongsKey", s."VocabRootId", s."Prefixes", array_to_string(s."PrefixKeys", ';') AS "PrefixKeys", s."Origin"
        FROM "unshackled-word"."VocabStrongsRoots" s
        """;

    private const string InsertRoot = """
        INSERT INTO "unshackled-word"."VocabRoots" ("Track", "Root", "RootNormalized", "Homograph", "StrongsKey")
        VALUES (@Track, @Root, @RootNormalized, @Homograph, @StrongsKey)
        RETURNING "Id"
        """;

    private const string UpdateRoot = """
        UPDATE "unshackled-word"."VocabRoots"
        SET "Root" = @Root
          , "StrongsKey" = COALESCE(@StrongsKey, "StrongsKey")
          , "UpdatedAt" = now()
        WHERE "Id" = @Id
        """;

    private const string UpdateParent = """
        UPDATE "unshackled-word"."VocabRoots"
        SET "ParentRootId" = @ParentRootId
          , "UpdatedAt" = now()
        WHERE "Id" = @Id
        """;

    private const string UpsertWordRoot = """
        INSERT INTO "unshackled-word"."VocabStrongsRoots" ("Lang", "StrongsKey", "VocabRootId", "Prefixes", "PrefixKeys", "Origin")
        VALUES (@Lang, @StrongsKey, @VocabRootId, @Prefixes, CASE WHEN @PrefixKeys IS NULL THEN NULL ELSE string_to_array(@PrefixKeys, ';')::varchar(10)[] END, @Origin)
        ON CONFLICT ("Lang", "StrongsKey") DO UPDATE
            SET "VocabRootId" = EXCLUDED."VocabRootId"
              , "Prefixes" = EXCLUDED."Prefixes"
              , "PrefixKeys" = EXCLUDED."PrefixKeys"
              , "Origin" = EXCLUDED."Origin"
              , "AgentRunId" = NULL
              , "UpdatedAt" = now()
              , "UpdatedByUserId" = NULL
        """;

    public static async Task<VocabRootSeedCounts> ApplyAsync(IDbConnection c, IDbTransaction t, VocabSeedData seed)
    {
        var counts = new VocabRootSeedCounts();

        if (seed.Roots.Count == 0 && seed.WordRoots.Count == 0)
        {
            return counts;
        }

        var roots = (await c.QueryAsync<DbRoot>(SelectRoots, transaction: t)).ToList();
        var byIdentity = roots.ToDictionary(r => (r.Track, r.RootNormalized, r.Homograph));
        var keyOwner = roots.Where(r => r.StrongsKey is not null).ToDictionary(r => r.StrongsKey!, StringComparer.Ordinal);

        // pass 1: rows
        foreach (var line in seed.Roots)
        {
            var normalized = VocabText.Normalize(line.Root);
            var identity = (line.Track, normalized, line.Homograph);

            if (line.Track == "G" && line.StrongsKey is null)
            {
                // a Greek root is identified by its key (VocabRoots_Greek_Key_CK)
                counts.Skipped++;

                continue;
            }

            if (line.StrongsKey is not null && keyOwner.TryGetValue(line.StrongsKey, out var holder) && (holder.Track, holder.RootNormalized, holder.Homograph) != identity)
            {
                counts.Skipped++;

                continue;
            }

            if (!byIdentity.TryGetValue(identity, out var existing))
            {
                var id = await c.QuerySingleAsync<int>(InsertRoot, new { line.Track, line.Root, RootNormalized = normalized, line.Homograph, line.StrongsKey }, t);
                var created = new DbRoot { Id = id, Track = line.Track, Root = line.Root, RootNormalized = normalized, Homograph = line.Homograph, StrongsKey = line.StrongsKey };
                byIdentity[identity] = created;

                if (line.StrongsKey is not null)
                {
                    keyOwner[line.StrongsKey] = created;
                }

                counts.RootsNew++;
            }
            else if (existing.Root != line.Root || (line.StrongsKey is not null && existing.StrongsKey != line.StrongsKey))
            {
                // a seed root without a key leaves a local key alone (a Hebrew root may have got its verb key here)
                await c.ExecuteAsync(UpdateRoot, new { existing.Id, line.Root, line.StrongsKey }, t);
                existing.Root = line.Root;

                // keep the key lookup current: the old key is free again, the new one is taken (a later line must not insert it twice)
                if (line.StrongsKey is not null && existing.StrongsKey != line.StrongsKey)
                {
                    if (existing.StrongsKey is not null)
                    {
                        keyOwner.Remove(existing.StrongsKey);
                    }

                    keyOwner[line.StrongsKey] = existing;
                    existing.StrongsKey = line.StrongsKey;
                }
                counts.RootsChanged++;
            }
        }

        // pass 2: parents
        foreach (var line in seed.Roots.Where(l => l.ParentRoot is not null))
        {
            if (!byIdentity.TryGetValue((line.Track, VocabText.Normalize(line.Root), line.Homograph), out var child)
                || !byIdentity.TryGetValue((line.Track, VocabText.Normalize(line.ParentRoot), line.ParentHomograph ?? 1), out var parent)
                || parent.Id == child.Id)
            {
                continue;
            }

            if (child.ParentRootId != parent.Id)
            {
                await c.ExecuteAsync(UpdateParent, new { child.Id, ParentRootId = parent.Id }, t);
                child.ParentRootId = parent.Id;
                counts.RootsChanged++;
            }
        }

        // word rows
        var words = (await c.QueryAsync<DbWordRoot>(SelectWordRoots, transaction: t)).ToDictionary(w => (w.Lang, w.StrongsKey));

        foreach (var line in seed.WordRoots)
        {
            if (!byIdentity.TryGetValue((line.RootTrack, VocabText.Normalize(line.Root), line.RootHomograph), out var root))
            {
                counts.Skipped++;

                continue;
            }

            if (words.TryGetValue((line.Lang, line.StrongsKey), out var current))
            {
                if (current.Origin == "manual" && line.Origin != "manual")
                {
                    counts.ManualKept++;

                    continue;
                }

                if (current.VocabRootId == root.Id && current.Prefixes == line.Prefixes && current.PrefixKeys == line.PrefixKeys && current.Origin == line.Origin)
                {
                    counts.Unchanged++;

                    continue;
                }

                counts.WordRootsChanged++;
            }
            else
            {
                counts.WordRootsNew++;
            }

            await c.ExecuteAsync(UpsertWordRoot, new { line.Lang, line.StrongsKey, VocabRootId = root.Id, line.Prefixes, line.PrefixKeys, line.Origin }, t);
        }

        return counts;
    }

    private sealed class DbRoot
    {
        public int Id { get; init; }
        public string Track { get; init; } = string.Empty;
        public string Root { get; set; } = string.Empty;
        public string RootNormalized { get; init; } = string.Empty;
        public int Homograph { get; init; }
        public string? StrongsKey { get; set; }
        public int? ParentRootId { get; set; }
    }

    private sealed class DbWordRoot
    {
        public string Lang { get; init; } = string.Empty;
        public string StrongsKey { get; init; } = string.Empty;
        public int VocabRootId { get; init; }
        public string? Prefixes { get; init; }
        public string? PrefixKeys { get; init; }
        public string Origin { get; init; } = string.Empty;
    }
}
