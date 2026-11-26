using System.Data;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Dapper;
using UnshackledWord.Application.Features.Seed.Anchoring;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Lxx;

namespace UnshackledWord.Infrastructure.Seed.Lxx;

/// <summary>
/// Reads and applies the LXX tag seed (roadmap 0028.8): confirmed <c>LxxWordTags</c> and <c>LxxHebrewLinks</c> as <c>lxx/tags.csv</c> and <c>lxx/links.csv</c>. Every
/// method runs on a given connection and transaction so the database tests can roll back; <see cref="LxxSeedService"/> adds the files.
/// Rules: proposals never leave the database and are never deleted by an import; the files carry no Greek text and no user; imported rows have <c>UserId</c> NULL.
/// </summary>
public static partial class LxxSeed
{
    public const string Kind = "lxx";

    /// <summary>Counts of the import report (tags and links added together unless named).</summary>
    public const string Inserted = "inserted";

    public const string Updated = "updated";
    public const string Unchanged = "unchanged";
    public const string Deleted = "deleted";
    public const string Skipped = "skipped";

    /// <summary>Of the skipped lines: the LXX word is gone or its hash changed (an LXX text import with other words).</summary>
    public const string AnchorMismatch = "anchorMismatch";

    public const string TagsCount = "tags";
    public const string LinksCount = "links";
    public const string VersesCount = "verses";
    public const string SkippedList = "skippedRows";

    /// <summary>Source of an imported tag whose line has none (or an unknown one).</summary>
    public const string DefaultSource = "human";

    private static readonly string[] Sources = ["nt-form", "ai", "human"];
    private static readonly string[] TextForms = ["", "og", "th"];

    [GeneratedRegex("^G[0-9]{4,5}[A-Z]?$")]
    private static partial Regex StrongsPattern();

    [GeneratedRegex("^[0-9a-f]{8}$")]
    private static partial Regex HashPattern();

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    // ---- files -------------------------------------------------------------------------------------------------------------------

    public static (byte[] Bytes, string Sha256) SerializeTags(IEnumerable<LxxTagSeedLine> lines) => SeedCsv.Serialize<LxxTagSeedLine, LxxTagSeedLineMap>(lines);

    public static (byte[] Bytes, string Sha256) SerializeLinks(IEnumerable<LxxLinkSeedLine> lines) => SeedCsv.Serialize<LxxLinkSeedLine, LxxLinkSeedLineMap>(lines);

    public static List<LxxTagSeedLine> ParseTags(byte[] bytes) => SeedCsv.Read<LxxTagSeedLine, LxxTagSeedLineMap>(bytes);

    public static List<LxxLinkSeedLine> ParseLinks(byte[] bytes) => SeedCsv.Read<LxxLinkSeedLine, LxxLinkSeedLineMap>(bytes);

    // ---- export ------------------------------------------------------------------------------------------------------------------

    private sealed class TagRow
    {
        public long RahlfsRef { get; set; }
        public string TextForm { get; set; } = string.Empty;
        public int Position { get; set; }
        public string SrcHash { get; set; } = string.Empty;
        public string Strongs { get; set; } = string.Empty;
        public string Morphology { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
    }

    private sealed class LinkRow
    {
        public long RahlfsRef { get; set; }
        public string TextForm { get; set; } = string.Empty;
        public int Position { get; set; }
        public string SrcHash { get; set; } = string.Empty;
        public int? HebLxxRefId { get; set; }
        public int? HebPosition { get; set; }
        public string? Hebrew { get; set; }
    }

    /// <summary>Every confirmed tag as seed line in reading order of the Rahlfs key (reference, text form, position).</summary>
    public static async Task<List<LxxTagSeedLine>> ReadTagsAsync(IDbConnection connection, IDbTransaction? transaction, CancellationToken token = default)
    {
        const string sql = """
                           SELECT v."RahlfsRefId" "RahlfsRef"
                                , v."TextForm"
                                , w."Position"
                                , w."SrcHash"
                                , t."Strongs"
                                , t."Morphology"
                                , t."Source"
                           FROM "unshackled-word"."LxxWordTags" t
                           JOIN "unshackled-word"."LxxWords" w ON w."Id" = t."LxxWordId"
                           JOIN "unshackled-word"."LxxVerses" v ON v."Id" = w."LxxVerseId"
                           WHERE 1=1
                               AND t."Status" = 'confirmed'
                               AND v."RahlfsRefId" IS NOT NULL
                           ORDER BY v."RahlfsRefId"
                                  , v."TextForm"
                                  , w."Position"
                           """;

        var rows = await connection.QueryAsync<TagRow>(new CommandDefinition(sql, null, transaction, cancellationToken: token));

        return rows.Select(r => new LxxTagSeedLine
        {
            RahlfsRef = r.RahlfsRef, TextForm = r.TextForm, Position = r.Position, SrcHash = r.SrcHash, Strongs = r.Strongs, Morphology = r.Morphology, Source = r.Source,
        }).ToList();
    }

    /// <summary>
    /// Every confirmed link as seed line; the Hebrew side is hashed from the STEP word. Links whose Hebrew word no longer exists (a stale id) cannot be anchored
    /// and are counted in <paramref name="unanchored"/>.
    /// </summary>
    public static async Task<(List<LxxLinkSeedLine> Lines, int Unanchored)> ReadLinksAsync(IDbConnection connection, IDbTransaction? transaction, CancellationToken token = default)
    {
        const string sql = """
                           SELECT v."RahlfsRefId" "RahlfsRef"
                                , v."TextForm"
                                , w."Position"
                                , w."SrcHash"
                                , shw."LxxRefId" "HebLxxRefId"
                                , shw."PositionInVerse" "HebPosition"
                                , shw."Hebrew"
                           FROM "unshackled-word"."LxxHebrewLinks" k
                           JOIN "unshackled-word"."LxxWords" w ON w."Id" = k."LxxWordId"
                           JOIN "unshackled-word"."LxxVerses" v ON v."Id" = w."LxxVerseId"
                           LEFT JOIN "unshackled-word"."StepHebrewWords" shw ON shw."Id" = k."StepHebrewWordId"
                           WHERE 1=1
                               AND k."Status" = 'confirmed'
                               AND v."RahlfsRefId" IS NOT NULL
                           ORDER BY v."RahlfsRefId"
                                  , v."TextForm"
                                  , w."Position"
                                  , shw."LxxRefId"
                                  , shw."PositionInVerse"
                           """;

        var rows = (await connection.QueryAsync<LinkRow>(new CommandDefinition(sql, null, transaction, cancellationToken: token))).ToList();
        var anchored = rows.Where(r => r.HebLxxRefId is not null).ToList();
        var lines = anchored
            .Select(r => new LxxLinkSeedLine
            {
                RahlfsRef = r.RahlfsRef, TextForm = r.TextForm, Position = r.Position, SrcHash = r.SrcHash, HebLxxRefId = r.HebLxxRefId!.Value,
                HebPosition = r.HebPosition!.Value, HebSrcHash = SourceKey.Hash(r.Hebrew),
            })
            .Distinct(LinkLineComparer.Instance)
            .OrderBy(l => l.RahlfsRef).ThenBy(l => l.TextForm, StringComparer.Ordinal).ThenBy(l => l.Position)
            .ThenBy(l => l.HebLxxRefId).ThenBy(l => l.HebPosition).ThenBy(l => l.HebSrcHash, StringComparer.Ordinal)
            .ToList();

        return (lines, rows.Count - anchored.Count);
    }

    private sealed class LinkLineComparer : IEqualityComparer<LxxLinkSeedLine>
    {
        public static readonly LinkLineComparer Instance = new();

        public bool Equals(LxxLinkSeedLine? x, LxxLinkSeedLine? y) => x is not null && y is not null && Key(x) == Key(y);

        public int GetHashCode(LxxLinkSeedLine obj) => Key(obj).GetHashCode();

        private static (long, string, int, string, int, int, string) Key(LxxLinkSeedLine l) =>
            (l.RahlfsRef, l.TextForm, l.Position, l.SrcHash, l.HebLxxRefId, l.HebPosition, l.HebSrcHash);
    }

    // ---- import ------------------------------------------------------------------------------------------------------------------

    private readonly record struct WordKey(long RahlfsRef, string TextForm, int Position);

    private sealed class WordRow
    {
        public int Id { get; set; }
        public int LxxVerseId { get; set; }
        public long RahlfsRef { get; set; }
        public string TextForm { get; set; } = string.Empty;
        public int Position { get; set; }
        public string SrcHash { get; set; } = string.Empty;
    }

    private sealed class ExistingTag
    {
        public int LxxWordId { get; set; }
        public int LxxVerseId { get; set; }
        public int Position { get; set; }
        public string Lemma { get; set; } = string.Empty;
        public string Strongs { get; set; } = string.Empty;
        public string Morphology { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
    }

    private sealed class ExistingLink
    {
        public int LxxWordId { get; set; }
        public int LxxVerseId { get; set; }
        public int Position { get; set; }
        public int StepHebrewWordId { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    private sealed class HebrewWordRow
    {
        public int Id { get; set; }
        public int Ref { get; set; }
        public int Pos { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    private sealed record Tag(string Lemma, string Strongs, string Morphology, string Source);

    private sealed record AuditWord(int Position, string? Lemma, string? Strongs, string? Morphology, string? Status, string? Source, List<int> HebrewWordIds);

    private sealed record AuditState(List<AuditWord> Words);

    /// <summary>
    /// Applies the files to the confirmed layer. A line is anchored to its LXX word by (Rahlfs reference, text form, position) plus the stored hash; a line whose word
    /// is missing or whose hash differs is skipped and counted (<see cref="AnchorMismatch"/>), never guessed. The Hebrew side goes through <see cref="SourceWordResolver"/>.
    /// Merge (<paramref name="mirror"/> false): missing tags and links are inserted, a differing tag (or a proposed one) becomes the file's confirmed tag, proposed links
    /// the file confirms become confirmed; nothing is deleted. Exact (<paramref name="mirror"/> true): additionally every confirmed tag and link the file does not
    /// have is deleted. Proposals the file does not mention stay untouched in both modes. Rows get <c>UserId</c> NULL; every changed verse gets one
    /// <c>LxxTagAudit</c> row with <c>Action = import</c> (before/after of the touched words).
    /// </summary>
    public static async Task<SeedStepReport> ImportAsync(IDbConnection connection, IDbTransaction transaction, IReadOnlyList<LxxTagSeedLine> tagLines,
        IReadOnlyList<LxxLinkSeedLine> linkLines, bool mirror, IProgress<string>? progress, CancellationToken token = default)
    {
        progress?.Report($"Importing {tagLines.Count} LXX tags and {linkLines.Count} links{(mirror ? " (exactly as the files)" : string.Empty)}");

        var skipped = new List<string>();
        var anchorMismatch = 0;

        // ---- the LXX words the lines point at
        var rahlfsIds = tagLines.Select(l => l.RahlfsRef).Concat(linkLines.Select(l => l.RahlfsRef)).Distinct().ToArray();
        var words = rahlfsIds.Length == 0
            ? []
            : (await connection.QueryAsync<WordRow>(new CommandDefinition("""
                SELECT w."Id"
                     , w."LxxVerseId"
                     , v."RahlfsRefId" "RahlfsRef"
                     , v."TextForm"
                     , w."Position"
                     , w."SrcHash"
                FROM "unshackled-word"."LxxWords" w
                JOIN "unshackled-word"."LxxVerses" v ON v."Id" = w."LxxVerseId"
                WHERE 1=1
                    AND v."RahlfsRefId" = ANY(@Ids)
                """, new { Ids = rahlfsIds }, transaction, cancellationToken: token))).ToDictionary(w => new WordKey(w.RahlfsRef, w.TextForm, w.Position));

        WordRow? ResolveWord(long rahlfsRef, string textForm, int position, string hash, string label, out string? problem)
        {
            if (!words.TryGetValue(new WordKey(rahlfsRef, textForm, position), out var word))
            {
                anchorMismatch++;
                problem = $"{label}: no LXX word at this position";
                return null;
            }

            if (!string.Equals(word.SrcHash, hash, StringComparison.Ordinal))
            {
                anchorMismatch++;
                problem = $"{label}: the word's hash changed ({hash} → {word.SrcHash})";
                return null;
            }

            problem = null;
            return word;
        }

        // ---- desired tags (the lemma is TBESG's for the Strong's number: the file has no Greek text)
        var lemmas = new Dictionary<string, string>(StringComparer.Ordinal);
        if (tagLines.Count > 0)
        {
            foreach (var (strongs, lemma, _) in await connection.QueryAsync<(string Strongs, string Lemma, string? Gloss)>(
                         new CommandDefinition(LxxAgentSql.Lexicon, null, transaction, cancellationToken: token)))
            {
                if (!string.IsNullOrWhiteSpace(lemma))
                {
                    lemmas.TryAdd(strongs, lemma.Trim().Normalize(NormalizationForm.FormC));
                }
            }
        }

        var desiredTags = new Dictionary<int, Tag>();
        var wordById = new Dictionary<int, WordRow>();
        foreach (var line in tagLines)
        {
            var label = Label(line.RahlfsRef, line.TextForm, line.Position);
            var invalid = ValidateTag(line);
            if (invalid is not null)
            {
                skipped.Add($"{label}: {invalid}");
                continue;
            }

            if (!lemmas.TryGetValue(line.Strongs, out var tagLemma))
            {
                skipped.Add($"{label}: Strong's number {line.Strongs} is not in TBESG");
                continue;
            }

            var word = ResolveWord(line.RahlfsRef, line.TextForm, line.Position, line.SrcHash, label, out var problem);
            if (word is null)
            {
                skipped.Add(problem!);
                continue;
            }

            if (desiredTags.ContainsKey(word.Id))
            {
                skipped.Add($"{label}: duplicate tag line in the file");
                continue;
            }

            var source = Sources.Contains(line.Source) ? line.Source! : DefaultSource;
            desiredTags[word.Id] = new Tag(tagLemma, line.Strongs, line.Morphology.Trim(), source);
            wordById[word.Id] = word;
        }

        // ---- desired links (Hebrew side re-resolved)
        var hebrewKeys = linkLines.Where(l => IsValidLinkShape(l)).Select(l => new SourceKey(l.HebLxxRefId, l.HebPosition, l.HebSrcHash)).Distinct().ToList();
        var hebrewRows = hebrewKeys.Count == 0
            ? []
            : (await connection.QueryAsync<HebrewWordRow>(new CommandDefinition("""
                SELECT s."Id"
                     , s."LxxRefId" "Ref"
                     , s."PositionInVerse" "Pos"
                     , s."Hebrew" "Text"
                FROM "unshackled-word"."StepHebrewWords" s
                WHERE 1=1
                    AND s."LxxRefId" = ANY(@Refs)
                ORDER BY s."LxxRefId"
                       , s."PositionInVerse"
                       , s."Id"
                """, new { Refs = hebrewKeys.Select(k => k.LxxRefId).Distinct().ToArray() }, transaction, cancellationToken: token))).ToList();
        var stepTokens = hebrewRows.Select(r => new AnchorToken(r.Ref, r.Pos, SourceKey.Hash(r.Text))).ToList();
        var stepIds = new Dictionary<AnchorToken, int>();
        foreach (var (anchor, row) in stepTokens.Zip(hebrewRows))
        {
            stepIds.TryAdd(anchor, row.Id);
        }

        var resolver = SourceWordResolver.Create(hebrewKeys, stepTokens);

        var desiredLinks = new HashSet<(int WordId, int HebrewId)>();
        foreach (var line in linkLines)
        {
            var label = $"{Label(line.RahlfsRef, line.TextForm, line.Position)} → {line.HebLxxRefId}:{line.HebPosition}";
            if (!IsValidLinkShape(line))
            {
                skipped.Add($"{label}: invalid line");
                continue;
            }

            var word = ResolveWord(line.RahlfsRef, line.TextForm, line.Position, line.SrcHash, label, out var problem);
            if (word is null)
            {
                skipped.Add(problem!);
                continue;
            }

            var match = resolver.Resolve(new SourceKey(line.HebLxxRefId, line.HebPosition, line.HebSrcHash));
            if (match.New is not { } target || !stepIds.TryGetValue(target, out var hebrewId))
            {
                skipped.Add($"{label}: the Hebrew word was not found");
                continue;
            }

            if (!desiredLinks.Add((word.Id, hebrewId)))
            {
                skipped.Add($"{label}: duplicate link line in the file");
                continue;
            }

            wordById[word.Id] = word;
        }

        // ---- what the database has: every confirmed row (for the mirror) and any row of a word the file touches
        var touchedIds = desiredTags.Keys.Concat(desiredLinks.Select(l => l.WordId)).Distinct().ToArray();
        var existingTags = (await connection.QueryAsync<ExistingTag>(new CommandDefinition("""
            SELECT t."LxxWordId"
                 , w."LxxVerseId"
                 , w."Position"
                 , t."Lemma"
                 , t."Strongs"
                 , t."Morphology"
                 , t."Status"
                 , t."Source"
            FROM "unshackled-word"."LxxWordTags" t
            JOIN "unshackled-word"."LxxWords" w ON w."Id" = t."LxxWordId"
            WHERE 1=1
                AND (t."Status" = 'confirmed' OR t."LxxWordId" = ANY(@Ids))
            """, new { Ids = touchedIds }, transaction, cancellationToken: token))).ToDictionary(t => t.LxxWordId);
        var existingLinks = (await connection.QueryAsync<ExistingLink>(new CommandDefinition("""
            SELECT k."LxxWordId"
                 , w."LxxVerseId"
                 , w."Position"
                 , k."StepHebrewWordId"
                 , k."Status"
            FROM "unshackled-word"."LxxHebrewLinks" k
            JOIN "unshackled-word"."LxxWords" w ON w."Id" = k."LxxWordId"
            WHERE 1=1
                AND (k."Status" = 'confirmed' OR k."LxxWordId" = ANY(@Ids))
            """, new { Ids = touchedIds }, transaction, cancellationToken: token))).ToDictionary(k => (k.LxxWordId, k.StepHebrewWordId));

        // ---- the plan
        int tagsInserted = 0, tagsUpdated = 0, tagsUnchanged = 0, tagsDeleted = 0, linksInserted = 0, linksUpdated = 0, linksUnchanged = 0, linksDeleted = 0;
        var tagUpserts = new List<(int WordId, Tag Tag)>();
        var tagDeletes = new List<int>();
        var linkUpserts = new List<(int WordId, int HebrewId)>();
        var linkDeletes = new List<(int WordId, int HebrewId)>();

        foreach (var (wordId, tag) in desiredTags)
        {
            if (!existingTags.TryGetValue(wordId, out var current))
            {
                tagsInserted++;
                tagUpserts.Add((wordId, tag));
            }
            else if (current.Status == "confirmed" && current.Lemma == tag.Lemma && current.Strongs == tag.Strongs && current.Morphology == tag.Morphology
                     && current.Source == tag.Source)
            {
                tagsUnchanged++;
            }
            else
            {
                tagsUpdated++;
                tagUpserts.Add((wordId, tag));
            }
        }

        foreach (var key in desiredLinks.OrderBy(l => l.WordId).ThenBy(l => l.HebrewId))
        {
            if (!existingLinks.TryGetValue(key, out var current))
            {
                linksInserted++;
                linkUpserts.Add(key);
            }
            else if (current.Status == "confirmed")
            {
                linksUnchanged++;
            }
            else
            {
                linksUpdated++;
                linkUpserts.Add(key);
            }
        }

        if (mirror)
        {
            foreach (var current in existingTags.Values.Where(t => t.Status == "confirmed" && !desiredTags.ContainsKey(t.LxxWordId)).OrderBy(t => t.LxxWordId))
            {
                tagsDeleted++;
                tagDeletes.Add(current.LxxWordId);
            }

            foreach (var current in existingLinks.Values.Where(k => k.Status == "confirmed" && !desiredLinks.Contains((k.LxxWordId, k.StepHebrewWordId)))
                         .OrderBy(k => k.LxxWordId).ThenBy(k => k.StepHebrewWordId))
            {
                linksDeleted++;
                linkDeletes.Add((current.LxxWordId, current.StepHebrewWordId));
            }
        }

        var audits = BuildAudits(tagUpserts, tagDeletes, linkUpserts, linkDeletes, existingTags, existingLinks, wordById);

        // ---- writes
        if (tagUpserts.Count > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO "unshackled-word"."LxxWordTags" ("LxxWordId", "Lemma", "Strongs", "Morphology", "Status", "Source", "UserId", "UpdatedAt")
                SELECT p."LxxWordId", p."Lemma", p."Strongs", p."Morphology", 'confirmed', p."Source", NULL, now()
                FROM unnest(@Ids, @Lemmas, @Strongs, @Morphologies, @Sources) AS p ("LxxWordId", "Lemma", "Strongs", "Morphology", "Source")
                ON CONFLICT ("LxxWordId") DO UPDATE
                SET "Lemma" = EXCLUDED."Lemma"
                  , "Strongs" = EXCLUDED."Strongs"
                  , "Morphology" = EXCLUDED."Morphology"
                  , "Status" = 'confirmed'
                  , "Source" = EXCLUDED."Source"
                  , "UserId" = NULL
                  , "UpdatedAt" = now()
                """, new
            {
                Ids = tagUpserts.Select(t => t.WordId).ToArray(),
                Lemmas = tagUpserts.Select(t => t.Tag.Lemma).ToArray(),
                Strongs = tagUpserts.Select(t => t.Tag.Strongs).ToArray(),
                Morphologies = tagUpserts.Select(t => t.Tag.Morphology).ToArray(),
                Sources = tagUpserts.Select(t => t.Tag.Source).ToArray(),
            }, transaction, cancellationToken: token));
        }

        if (tagDeletes.Count > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                DELETE FROM "unshackled-word"."LxxWordTags" t
                WHERE 1=1
                    AND t."Status" = 'confirmed'
                    AND t."LxxWordId" = ANY(@Ids)
                """, new { Ids = tagDeletes.ToArray() }, transaction, cancellationToken: token));
        }

        if (linkUpserts.Count > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO "unshackled-word"."LxxHebrewLinks" ("LxxWordId", "StepHebrewWordId", "Status", "UserId", "UpdatedAt")
                SELECT p."LxxWordId", p."StepHebrewWordId", 'confirmed', NULL, now()
                FROM unnest(@WordIds, @HebrewIds) AS p ("LxxWordId", "StepHebrewWordId")
                ON CONFLICT ("LxxWordId", "StepHebrewWordId") DO UPDATE
                SET "Status" = 'confirmed'
                  , "UserId" = NULL
                  , "UpdatedAt" = now()
                """, new { WordIds = linkUpserts.Select(l => l.WordId).ToArray(), HebrewIds = linkUpserts.Select(l => l.HebrewId).ToArray() }, transaction,
                cancellationToken: token));
        }

        if (linkDeletes.Count > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                DELETE FROM "unshackled-word"."LxxHebrewLinks" k
                USING unnest(@WordIds, @HebrewIds) AS p ("LxxWordId", "StepHebrewWordId")
                WHERE 1=1
                    AND k."Status" = 'confirmed'
                    AND k."LxxWordId" = p."LxxWordId"
                    AND k."StepHebrewWordId" = p."StepHebrewWordId"
                """, new { WordIds = linkDeletes.Select(l => l.WordId).ToArray(), HebrewIds = linkDeletes.Select(l => l.HebrewId).ToArray() }, transaction,
                cancellationToken: token));
        }

        foreach (var audit in audits)
        {
            await connection.ExecuteAsync(new CommandDefinition(LxxReviewSql.InsertAudit,
                new { audit.LxxVerseId, Action = "import", audit.Before, audit.After, UserId = (int?)null }, transaction, cancellationToken: token));
        }

        var inserted = tagsInserted + linksInserted;
        var updated = tagsUpdated + linksUpdated;
        var unchanged = tagsUnchanged + linksUnchanged;
        var deleted = tagsDeleted + linksDeleted;

        progress?.Report($"LXX seed: {inserted} inserted, {updated} updated, {unchanged} unchanged, {deleted} deleted, {skipped.Count} skipped ({anchorMismatch} anchor mismatches)");

        return new SeedStepReport { Kind = Kind, Action = "import" }
            .WithCount(TagsCount, tagLines.Count)
            .WithCount(LinksCount, linkLines.Count)
            .WithCount(Inserted, inserted)
            .WithCount(Updated, updated)
            .WithCount(Unchanged, unchanged)
            .WithCount(Deleted, deleted)
            .WithCount(Skipped, skipped.Count)
            .WithCount(AnchorMismatch, anchorMismatch)
            .WithCount(VersesCount, audits.Count)
            .WithCount("tagsDeleted", tagsDeleted)
            .WithCount("linksDeleted", linksDeleted)
            .AddList(SkippedList, skipped, s => s);
    }

    private sealed record AuditRow(int LxxVerseId, string? Before, string After);

    private static List<AuditRow> BuildAudits(List<(int WordId, Tag Tag)> tagUpserts, List<int> tagDeletes, List<(int WordId, int HebrewId)> linkUpserts,
        List<(int WordId, int HebrewId)> linkDeletes, Dictionary<int, ExistingTag> existingTags, Dictionary<(int, int), ExistingLink> existingLinks,
        Dictionary<int, WordRow> wordById)
    {
        var changedWords = tagUpserts.Select(t => t.WordId).Concat(tagDeletes).Concat(linkUpserts.Select(l => l.WordId)).Concat(linkDeletes.Select(l => l.WordId)).Distinct().ToList();
        if (changedWords.Count == 0)
        {
            return [];
        }

        var upsertTags = tagUpserts.ToDictionary(t => t.WordId, t => t.Tag);
        var deletedTags = tagDeletes.ToHashSet();
        var deletedLinks = linkDeletes.ToHashSet();
        var addedLinks = linkUpserts.ToHashSet();

        var places = new Dictionary<int, (int VerseId, int Position)>();
        foreach (var l in existingLinks.Values)
        {
            places[l.LxxWordId] = (l.LxxVerseId, l.Position);
        }

        foreach (var t in existingTags.Values)
        {
            places[t.LxxWordId] = (t.LxxVerseId, t.Position);
        }

        foreach (var w in wordById.Values)
        {
            places[w.Id] = (w.LxxVerseId, w.Position);
        }

        (int VerseId, int Position) Locate(int wordId) => places[wordId];

        var audits = new List<AuditRow>();
        foreach (var verse in changedWords.Select(id => (Id: id, Place: Locate(id))).GroupBy(w => w.Place.VerseId).OrderBy(g => g.Key))
        {
            var before = new List<AuditWord>();
            var after = new List<AuditWord>();

            foreach (var (wordId, place) in verse.OrderBy(w => w.Place.Position))
            {
                var oldLinks = existingLinks.Values.Where(l => l.LxxWordId == wordId).OrderBy(l => l.StepHebrewWordId).ToList();
                existingTags.TryGetValue(wordId, out var oldTag);

                if (oldTag is not null || oldLinks.Count > 0)
                {
                    before.Add(new AuditWord(place.Position, oldTag?.Lemma, oldTag?.Strongs, oldTag?.Morphology, oldTag?.Status, oldTag?.Source,
                        oldLinks.Select(l => l.StepHebrewWordId).ToList()));
                }

                var newLinks = oldLinks.Where(l => !deletedLinks.Contains((wordId, l.StepHebrewWordId))).Select(l => l.StepHebrewWordId)
                    .Concat(addedLinks.Where(l => l.WordId == wordId).Select(l => l.HebrewId)).Distinct().Order().ToList();
                AuditWord? newWord = null;
                if (upsertTags.TryGetValue(wordId, out var tag))
                {
                    newWord = new AuditWord(place.Position, tag.Lemma, tag.Strongs, tag.Morphology, "confirmed", tag.Source, newLinks);
                }
                else if (oldTag is not null && !deletedTags.Contains(wordId))
                {
                    newWord = new AuditWord(place.Position, oldTag.Lemma, oldTag.Strongs, oldTag.Morphology, oldTag.Status, oldTag.Source, newLinks);
                }
                else if (newLinks.Count > 0)
                {
                    newWord = new AuditWord(place.Position, null, null, null, null, null, newLinks);
                }

                if (newWord is not null)
                {
                    after.Add(newWord);
                }
            }

            audits.Add(new AuditRow(verse.Key, before.Count == 0 ? null : JsonSerializer.Serialize(new AuditState(before), Json),
                JsonSerializer.Serialize(new AuditState(after), Json)));
        }

        return audits;
    }

    /// <summary>How many confirmed tags and links an import "Exakt wie Datei" would delete (the import runs in the caller's transaction, which the caller rolls back).</summary>
    public static async Task<(int Tags, int Links)> CountMirrorDeletesAsync(IDbConnection connection, IDbTransaction transaction, IReadOnlyList<LxxTagSeedLine> tagLines,
        IReadOnlyList<LxxLinkSeedLine> linkLines, CancellationToken token = default)
    {
        var report = await ImportAsync(connection, transaction, tagLines, linkLines, mirror: true, progress: null, token);

        return (report.Counts["tagsDeleted"], report.Counts["linksDeleted"]);
    }

    /// <summary>Confirmed tags in the database: SeedDb applies the files only when this is 0.</summary>
    public static async Task<int> CountConfirmedTagsAsync(IDbConnection connection, IDbTransaction? transaction, CancellationToken token = default) =>
        await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT count(*)::int4
            FROM "unshackled-word"."LxxWordTags" t
            WHERE t."Status" = 'confirmed'
            """, null, transaction, cancellationToken: token));

    /// <summary>
    /// Deletes every <c>LxxHebrewLinks</c> row and returns how many there were. Only for the re-resolve after a STEP import that inserted Hebrew words: the table was
    /// empty before, so every stored <c>StepHebrewWordId</c> belongs to the old numbering (risk "Hebrew link anchors", 08-risks.md).
    /// </summary>
    public static async Task<int> DeleteAllLinksAsync(IDbConnection connection, IDbTransaction transaction, CancellationToken token = default) =>
        await connection.ExecuteAsync(new CommandDefinition("""
            DELETE FROM "unshackled-word"."LxxHebrewLinks"
            """, null, transaction, cancellationToken: token));

    private static string Label(long rahlfsRef, string textForm, int position) =>
        $"{rahlfsRef}{(textForm.Length > 0 ? "/" + textForm : string.Empty)}#{position}";

    private static bool IsValidLinkShape(LxxLinkSeedLine l) =>
        l.RahlfsRef > 0 && TextForms.Contains(l.TextForm) && l.Position >= 1 && HashPattern().IsMatch(l.SrcHash ?? string.Empty) && l.HebLxxRefId > 0 && l.HebPosition >= 0
        && HashPattern().IsMatch(l.HebSrcHash ?? string.Empty);

    private static string? ValidateTag(LxxTagSeedLine l)
    {
        if (l.RahlfsRef <= 0 || !TextForms.Contains(l.TextForm) || l.Position < 1 || !HashPattern().IsMatch(l.SrcHash ?? string.Empty))
        {
            return "invalid anchor";
        }

        if (l.Strongs is null || !StrongsPattern().IsMatch(l.Strongs))
        {
            return $"invalid Strong's number '{l.Strongs}'";
        }

        if (string.IsNullOrWhiteSpace(l.Morphology) || l.Morphology.Length > 30)
        {
            return "empty or too long morphology";
        }

        return null;
    }
}
