using System.Data;
using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Vocab;
using UnshackledWord.Domain.Models.Vocab;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Infrastructure.Vocab;

/// <summary>
/// Loads the vocab entries and the Greek lexicon, lets <see cref="VocabRootPrefill"/> decide and writes the roots (roadmap 0030.2).
/// Only keys without a root row or with <c>Origin = 'prefill'</c> are written, and only when the result differs, so a second run writes
/// nothing. One transaction. Used by the maintainer job (<c>POST api/maintainer/vocab/roots/prefill</c>) and the SeedDb verbs
/// <c>vocab-roots-prefill</c> and <c>vocab-generate</c>.
/// </summary>
public sealed class VocabRootPrefillService
{
    public const string Kind = "vocab";
    public const string Action = "rootPrefill";

    private const int TimeoutSeconds = 600;
    private const string Prefill = "prefill";

    private const string SelectMainKeys = """
        WITH main_keys AS
        (
            SELECT DISTINCT ON (vs."VocabEntryId")
                   vs."VocabEntryId"
                 , vs."Lang"::text AS "Lang"
                 , vs."StrongsKey" AS "MainKey"
            FROM "unshackled-word"."VocabStrongs" vs
            ORDER BY vs."VocabEntryId", vs."Occurrences" DESC, vs."StrongsKey"
        )
        """;

    private const string SelectGreekEntries = SelectMainKeys + """

        SELECT mk."MainKey"
             , l."Lexicon"
             , sr."Origin"
             , sr."VocabRootId"
             , sr."Prefixes"
             , sr."PrefixKeys"
        FROM main_keys mk
            JOIN "unshackled-word"."VocabEntries" e ON e."Id" = mk."VocabEntryId"
            JOIN "unshackled-word"."StepStrongsLexicon" l ON l."LanguageId" = 2
                AND 'G' || CASE WHEN l."Number" >= 10000 THEN l."Number"::text ELSE lpad(l."Number"::text, 4, '0') END
                    || upper(COALESCE(l."Extra", '')) = mk."MainKey"
            LEFT JOIN "unshackled-word"."VocabStrongsRoots" sr ON sr."Lang" = mk."Lang" AND sr."StrongsKey" = mk."MainKey"
        WHERE 1=1
            AND e."Track" = 'G'
            AND e."RetiredAt" IS NULL
            AND NOT e."IsMorpheme"
            AND NOT e."IsProperName"
        """;

    // The key is the main key of the lemma's vocab entry (STEP splits βάλλω into G0906G..K, the entry has one main key), so a lemma
    // with several STEP variants is one candidate and not an ambiguity.
    private const string SelectGreekLexicon = SelectMainKeys + """

        SELECT COALESCE(mk."MainKey", lk."Key") AS "Key"
             , l."OriginalWord"
             , e."Lemma"                        AS "VocabLemma"
             , (e."Id" IS NOT NULL AND e."RetiredAt" IS NULL) AS "InVocab"
        FROM "unshackled-word"."StepStrongsLexicon" l
            CROSS JOIN LATERAL
            (
                SELECT 'G' || CASE WHEN l."Number" >= 10000 THEN l."Number"::text ELSE lpad(l."Number"::text, 4, '0') END
                           || upper(COALESCE(l."Extra", '')) AS "Key"
            ) lk
            LEFT JOIN "unshackled-word"."VocabStrongs" vs ON vs."Lang" = 'G' AND vs."StrongsKey" = lk."Key"
            LEFT JOIN "unshackled-word"."VocabEntries" e ON e."Id" = vs."VocabEntryId"
            LEFT JOIN main_keys mk ON mk."VocabEntryId" = vs."VocabEntryId"
        WHERE 1=1
            AND l."LanguageId" = 2
        """;

    private const string SelectHebrewVerbs = SelectMainKeys + """

        SELECT mk."MainKey"
             , e."Lemma"
             , sr."Origin"
             , sr."VocabRootId"
        FROM main_keys mk
            JOIN "unshackled-word"."VocabEntries" e ON e."Id" = mk."VocabEntryId"
            LEFT JOIN "unshackled-word"."VocabStrongsRoots" sr ON sr."Lang" = mk."Lang" AND sr."StrongsKey" = mk."MainKey"
        WHERE 1=1
            AND e."Track" = 'H'
            AND e."Pos" IN ('H:V', 'A:V')
            AND e."RetiredAt" IS NULL
            AND NOT e."IsMorpheme"
            AND NOT e."IsProperName"
        """;

    private const string SelectRoots = """
        SELECT r."Id"
             , r."Track"::text AS "Track"
             , r."RootNormalized"
             , r."Homograph"
             , r."StrongsKey"
        FROM "unshackled-word"."VocabRoots" r
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public VocabRootPrefillService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<SeedStepReport> PrefillAsync(IProgress<string>? progress, CancellationToken token)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();

        var report = await PrefillAsync(connection, transaction, progress, token);
        transaction.Commit();

        return report;
    }

    /// <summary>The same run on the caller's transaction (tests roll it back).</summary>
    public static async Task<SeedStepReport> PrefillAsync(IDbConnection connection, IDbTransaction transaction, IProgress<string>? progress, CancellationToken token = default)
    {
        var report = new SeedStepReport { Kind = Kind, Action = Action };
        var roots = new RootIndex(await Query<RootRow>(connection, transaction, SelectRoots));

        progress?.Report("Splitting the Greek compounds");
        await PrefillGreekAsync(connection, transaction, roots, report, token);

        progress?.Report("Writing the Hebrew and Aramaic verb roots");
        await PrefillHebrewAsync(connection, transaction, roots, report, token);

        return report;
    }

    private static async Task PrefillGreekAsync(IDbConnection connection, IDbTransaction transaction, RootIndex roots, SeedStepReport report, CancellationToken token)
    {
        var entries = await Query<GreekEntryRow>(connection, transaction, SelectGreekEntries);
        var lexicon = await LoadLexiconAsync(connection, transaction);

        var eligible = entries.Where(e => e.Origin is null or Prefill).ToList();
        var result = VocabRootPrefill.ResolveGreek(eligible.Select(e => new VocabRootGreekEntry(e.MainKey, e.Lexicon)), lexicon);
        var existing = eligible.GroupBy(e => e.MainKey).ToDictionary(g => g.Key, g => g.First());

        int written = 0, updated = 0, unchanged = 0, rootsCreated = 0;

        foreach (var split in result.Splits)
        {
            var rootId = roots.ByKey(split.RootKey);

            if (rootId is null)
            {
                rootId = await roots.CreateAsync(connection, transaction, "G", split.RootLemma, split.RootKey, token);
                rootsCreated++;
            }

            var current = existing[split.MainKey];

            if (current.Origin is not null
                && current.VocabRootId == rootId
                && current.Prefixes == split.Prefixes
                && (current.PrefixKeys ?? []).SequenceEqual(split.PrefixKeys))
            {
                unchanged++;

                continue;
            }

            await VocabRootRepository.UpsertWordRootAsync(connection, transaction, new VocabWordRootUpsert
            {
                Lang = "G",
                StrongsKey = split.MainKey,
                VocabRootId = rootId.Value,
                Prefixes = split.Prefixes,
                PrefixKeys = split.PrefixKeys,
                Origin = Prefill
            }, token);

            if (current.Origin is null)
            {
                written++;
            }
            else
            {
                updated++;
            }
        }

        report.Counts["greekWritten"] = written;
        report.Counts["greekUpdated"] = updated;
        report.Counts["greekUnchanged"] = unchanged;
        report.Counts["greekUnresolved"] = result.Unresolved.Count;
        report.Counts["greekKeptAgentOrManual"] = entries.Count - eligible.Count;
        report.Counts["greekRootsCreated"] = rootsCreated;

        var items = result.Unresolved.Select(u => $"{u.MainKey} {u.Headword}: {u.Reason}").ToList();
        report.Lists["greekUnresolved"] = new SeedReportList { Total = items.Count, Items = items.Take(SeedStepReport.MaxListItems).ToList() };
    }

    private static async Task PrefillHebrewAsync(IDbConnection connection, IDbTransaction transaction, RootIndex roots, SeedStepReport report, CancellationToken token)
    {
        var verbs = await Query<HebrewVerbRow>(connection, transaction, SelectHebrewVerbs);
        var existing = verbs.GroupBy(v => v.MainKey).ToDictionary(g => g.Key, g => g.First());

        // the numbering looks at every verb, so a kept agent or manual row does not shift the homographs of the others
        var resolved = VocabRootPrefill.ResolveHebrew(verbs.Select(v => new VocabRootHebrewVerb(v.MainKey, v.Lemma)));

        int written = 0, updated = 0, unchanged = 0, conflicts = 0, rootsCreated = 0;

        foreach (var root in resolved)
        {
            var current = existing[root.MainKey];

            if (current.Origin is not null and not Prefill)
            {
                continue;
            }

            var rootId = roots.ByKey(root.MainKey);

            if (rootId is null)
            {
                if (roots.IsTaken("H", root.Root, root.Homograph))
                {
                    conflicts++;

                    continue;
                }

                rootId = await roots.CreateAsync(connection, transaction, "H", root.Root, root.MainKey, token, root.Homograph);
                rootsCreated++;
            }

            if (current.Origin is not null && current.VocabRootId == rootId)
            {
                unchanged++;

                continue;
            }

            await VocabRootRepository.UpsertWordRootAsync(connection, transaction, new VocabWordRootUpsert
            {
                Lang = "H",
                StrongsKey = root.MainKey,
                VocabRootId = rootId.Value,
                Origin = Prefill
            }, token);

            if (current.Origin is null)
            {
                written++;
            }
            else
            {
                updated++;
            }
        }

        report.Counts["hebrewWritten"] = written;
        report.Counts["hebrewUpdated"] = updated;
        report.Counts["hebrewUnchanged"] = unchanged;
        report.Counts["hebrewSlotTaken"] = conflicts;
        report.Counts["hebrewKeptAgentOrManual"] = verbs.Count(v => v.Origin is not null and not Prefill);
        report.Counts["hebrewRootsCreated"] = rootsCreated;
    }

    internal static async Task<List<VocabRootLexiconLemma>> LoadLexiconAsync(IDbConnection connection, IDbTransaction? transaction)
    {
        var rows = await Query<LexiconRow>(connection, transaction, SelectGreekLexicon);
        var lemmas = new List<VocabRootLexiconLemma>();

        foreach (var row in rows)
        {
            var display = string.IsNullOrWhiteSpace(row.VocabLemma) ? row.OriginalWord : row.VocabLemma;
            var forms = new[] { row.OriginalWord, row.VocabLemma }
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Select(f => VocabText.Normalize(f))
                .Distinct();

            foreach (var normalized in forms)
            {
                lemmas.Add(new VocabRootLexiconLemma(row.Key, normalized, display!, row.InVocab));
            }
        }

        return lemmas;
    }

    private static async Task<List<T>> Query<T>(IDbConnection connection, IDbTransaction? transaction, string sql) =>
        (await connection.QueryAsync<T>(sql, transaction: transaction, commandTimeout: TimeoutSeconds)).ToList();

    /// <summary>Roots by Strong's key and the taken (track, normalized root, homograph) slots, kept current while the run creates roots.</summary>
    private sealed class RootIndex
    {
        private readonly Dictionary<string, int> _byKey;
        private readonly HashSet<(string Track, string Normalized, short Homograph)> _slots;

        public RootIndex(IEnumerable<RootRow> rows)
        {
            var list = rows.ToList();
            _byKey = list.Where(r => r.StrongsKey is not null).ToDictionary(r => r.StrongsKey!, r => r.Id);
            _slots = list.Select(r => (r.Track, r.RootNormalized, r.Homograph)).ToHashSet();
        }

        public int? ByKey(string key) => _byKey.TryGetValue(key, out var id) ? id : null;

        public bool IsTaken(string track, string root, short homograph) => _slots.Contains((track, VocabText.Normalize(root), homograph));

        /// <summary>Creates the root in the lowest free homograph slot at or above <paramref name="homograph"/>.</summary>
        public async Task<int> CreateAsync(IDbConnection connection, IDbTransaction transaction, string track, string root, string key, CancellationToken token, short homograph = 1)
        {
            var normalized = VocabText.Normalize(root);

            while (_slots.Contains((track, normalized, homograph)))
            {
                homograph++;
            }

            var id = await VocabRootRepository.UpsertRootAsync(connection, transaction, new VocabRootUpsert
            {
                Track = track,
                Root = root,
                Homograph = homograph,
                StrongsKey = key
            }, token);

            _byKey[key] = id;
            _slots.Add((track, normalized, homograph));

            return id;
        }
    }

    private sealed class RootRow
    {
        public int Id { get; init; }
        public string Track { get; init; } = string.Empty;
        public string RootNormalized { get; init; } = string.Empty;
        public short Homograph { get; init; }
        public string? StrongsKey { get; init; }
    }

    private sealed class GreekEntryRow
    {
        public string MainKey { get; init; } = string.Empty;
        public string? Lexicon { get; init; }
        public string? Origin { get; init; }
        public int? VocabRootId { get; init; }
        public string? Prefixes { get; init; }
        public string[]? PrefixKeys { get; init; }
    }

    private sealed class HebrewVerbRow
    {
        public string MainKey { get; init; } = string.Empty;
        public string Lemma { get; init; } = string.Empty;
        public string? Origin { get; init; }
        public int? VocabRootId { get; init; }
    }

    private sealed class LexiconRow
    {
        public string Key { get; init; } = string.Empty;
        public string OriginalWord { get; init; } = string.Empty;
        public string? VocabLemma { get; init; }
        public bool InVocab { get; init; }
    }
}
