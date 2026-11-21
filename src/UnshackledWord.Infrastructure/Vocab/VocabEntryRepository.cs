using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.WebApi.Vocab;

namespace UnshackledWord.Infrastructure.Vocab;

/// <summary>
/// Read side of <c>GET api/vocab/entries/{id}</c> (roadmap 0022.2): the entry, its keys, accepted meanings, the German renderings from
/// <c>StatsVocabRenderings</c> summed over the keys, and a few sample verses. Samples read the live text like the view does: a verse with an
/// Elberfelder 2027 variant (<c>Revisions."Code" = 'elb2027'</c>) uses the variant's links, never its Elb1871 links.
/// </summary>
public sealed class VocabEntryRepository
{
    public const int SampleVerseCount = 3;

    private const string SelectEntry = """
        SELECT e."Id"
             , e."Track"::text                     AS "Track"
             , e."FrequencyRank"                   AS "Rank"
             , e."Lemma"
             , e."Transliteration"
             , e."DictionaryForm"
             , e."Pos"
             , e."Occurrences"
             , e."IsProperName"
             , (e."ReviewedAt" IS NOT NULL)        AS "Reviewed"
             , (e."AudioObjectKey" IS NOT NULL)    AS "HasAudio"
        FROM "unshackled-word"."VocabEntries" e
        WHERE 1=1
            AND e."Id" = @Id
            AND (@IncludeRetired OR e."RetiredAt" IS NULL)
        """;

    private const string SelectTrack = """
        SELECT e."Track"::text
        FROM "unshackled-word"."VocabEntries" e
        WHERE e."Id" = @Id
        """;

    private const string SelectKeys = """
        SELECT vs."StrongsKey"
             , vs."Occurrences"
        FROM "unshackled-word"."VocabStrongs" vs
        WHERE 1=1
            AND vs."VocabEntryId" = @Id
        ORDER BY vs."Occurrences" DESC, vs."StrongsKey"
        """;

    private const string SelectMeanings = """
        SELECT m."German"
             , m."Rank"
             , m."Count"
             , m."Share"
        FROM "unshackled-word"."VocabMeanings" m
        WHERE 1=1
            AND m."VocabEntryId" = @Id
            AND m."IsAccepted"
        ORDER BY m."Rank"
        """;

    private const string SelectRefreshedAt = """
        SELECT s."VocabRenderingsRefreshedAt"
        FROM "unshackled-word"."DqRefreshState" s
        WHERE 1=1
            AND s."Id" = 1
        """;

    // One row per (key, German key, ...) of the view; the entry's keys select their rows through the unique index of the view.
    private const string SelectRenderings = """
        WITH "EntryRows" AS
        (
            SELECT r."GermanKey"
                 , r."GermanDisplay"
                 , r."KeyIsForm"
                 , r."GermanPos"
                 , r."LinkCount"
                 , r."VerseCount"
                 , r."FromRevision"
            FROM "unshackled-word"."VocabStrongs" vs
                JOIN "unshackled-word"."StatsVocabRenderings" r ON r."Lang" = vs."Lang" AND r."StrongsKey" = vs."StrongsKey"
            WHERE 1=1
                AND vs."VocabEntryId" = @Id
        )
        , "Grouped" AS
        (
            SELECT e."GermanKey"
                 , (array_agg(e."GermanDisplay" ORDER BY e."LinkCount" DESC, e."GermanDisplay"))[1]   AS "GermanDisplay"
                 , sum(e."LinkCount")::int                                                           AS "LinkCount"
                 , sum(e."VerseCount")::int                                                          AS "VerseCount"
                 , COALESCE(sum(e."LinkCount") FILTER (WHERE e."FromRevision"), 0)::int              AS "FromRevisionCount"
                 , array_remove(array_agg(DISTINCT e."GermanPos"), NULL)                             AS "PosTags"
                 , bool_and(e."KeyIsForm")                                                           AS "KeyIsForm"
            FROM "EntryRows" e
            GROUP BY e."GermanKey"
        )
        SELECT g."GermanKey"
             , g."GermanDisplay"
             , g."LinkCount"
             , g."VerseCount"
             , g."FromRevisionCount"
             , round(g."LinkCount" * 100.0 / sum(g."LinkCount") OVER (), 1)   AS "Share"
             , g."PosTags"
             , g."KeyIsForm"
             , (sum(g."LinkCount") OVER ())::int                              AS "TotalLinks"
             , (count(*) OVER ())::int                                        AS "RenderingsTotal"
        FROM "Grouped" g
        ORDER BY g."LinkCount" DESC, g."GermanKey"
        LIMIT @Max
        """;

    // The live Elberfelder 2027 variant of a verse.
    private const string VariantsCte = """
        "Variants" AS
        (
            SELECT rv."Id"       AS "RevisionVerseId"
                 , rv."HebRefId" AS "HebRefId"
            FROM "unshackled-word"."RevisionVerses" rv
                JOIN "unshackled-word"."Revisions" r ON r."Id" = rv."RevisionId"
            WHERE 1=1
                AND r."Code" = 'elb2027'
        )
        """;

    // Step words that carry one of the entry's keys (@Keys), same key rule as the vocab generator. Greek: the disambiguated Strong's.
    private const string GreekCandidates = """
        "Cand" AS MATERIALIZED
        (
            SELECT s."Id"
                 , s."PositionInVerse"
            FROM "unshackled-word"."StepGreekWords" s
            WHERE 1=1
                AND upper(s."DisambiguatedStrongs") = ANY(@Keys)
        )
        """;

    // Hebrew: the braced (main) part of DisambiguatedStrongs, e.g. 'H9003/{H7225G}' -> H7225G. The generator also looks at the morphemes of
    // ExpandedStrongTags; that function is too slow for a sample query, so prefix morphemes (H9001-H9049, not ranked) find no samples.
    // @Likes pre-filter the scan ('%H7225G%').
    private const string HebrewCandidates = """
        "Cand" AS MATERIALIZED
        (
            SELECT s."Id"
                 , s."PositionInVerse"
            FROM "unshackled-word"."StepHebrewWords" s
            WHERE 1=1
                AND s."DisambiguatedStrongs" LIKE ANY(@Likes)
                AND upper(CASE WHEN position('{' IN s."DisambiguatedStrongs") > 0
                               THEN split_part(split_part(s."DisambiguatedStrongs", '{', 2), '}', 1)
                               ELSE s."DisambiguatedStrongs" END) = ANY(@Keys)
        )
        """;

    // Human = a user that is not the AI user ("ai" role); seed rows have no user. Shorter verses first.
    private static string SelectSampleHits(string candidates, string mappingTable, string lang) => $"""
        WITH {VariantsCte}
        , {candidates}
        , "Hits" AS
        (
            SELECT m."HebRefId"                                      AS "HebRefId"
                 , w."PositionInVerse"                               AS "GermanPosition"
                 , c."PositionInVerse"                               AS "OriginalPosition"
                 , false                                             AS "FromRevision"
                 , (m."CreatedByUserId" IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM "unshackled-word"."UserRoles" ur WHERE ur."UserId" = m."CreatedByUserId" AND ur."Role" = 'ai')) AS "Human"
                 , length(v."VerseText")                             AS "Len"
            FROM "Cand" c
                JOIN "unshackled-word"."{mappingTable}" m ON m."StepWordId" = c."Id"
                JOIN "unshackled-word"."Elb1871Words" w ON w."Id" = m."ElbWordId"
                JOIN "unshackled-word"."Elb1871Verses" v ON v."HebRefId" = m."HebRefId"
            WHERE 1=1
                AND m."Role" = 'head'
                AND NOT COALESCE(m."IsAddedWord", false)
                AND NOT EXISTS (SELECT 1 FROM "Variants" va WHERE va."HebRefId" = m."HebRefId")
            UNION ALL
            SELECT va."HebRefId"
                 , rw."PositionInVerse"
                 , c."PositionInVerse"
                 , true
                 , (rm."CreatedByUserId" IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM "unshackled-word"."UserRoles" ur WHERE ur."UserId" = rm."CreatedByUserId" AND ur."Role" = 'ai'))
                 , length(rv."VerseText")
            FROM "Cand" c
                JOIN "unshackled-word"."RevisionMapping" rm ON rm."StepWordId" = c."Id" AND rm."Lang" = '{lang}'
                JOIN "unshackled-word"."RevisionWords" rw ON rw."Id" = rm."RevisionWordId"
                JOIN "Variants" va ON va."RevisionVerseId" = rw."RevisionVerseId"
                JOIN "unshackled-word"."RevisionVerses" rv ON rv."Id" = va."RevisionVerseId"
            WHERE 1=1
                AND rm."Role" = 'head'
                AND NOT COALESCE(rm."IsAddedWord", false)
        )
        , "Best" AS
        (
            SELECT h."HebRefId"
            FROM "Hits" h
            GROUP BY h."HebRefId"
            ORDER BY bool_or(h."Human") DESC, min(h."Len"), h."HebRefId"
            LIMIT @Take
        )
        SELECT h."HebRefId"
             , h."GermanPosition"
             , h."OriginalPosition"
             , h."FromRevision"
             , h."Human"
        FROM "Hits" h
        WHERE 1=1
            AND h."HebRefId" IN (SELECT b."HebRefId" FROM "Best" b)
        """;

    private static readonly string GreekSampleHits = SelectSampleHits(GreekCandidates, "Elb1871GreekMapping", "G");
    private static readonly string HebrewSampleHits = SelectSampleHits(HebrewCandidates, "Elb1871HebrewMapping", "H");

    private const string SelectGreekOriginal = """
        SELECT s."LxxRefId"      AS "HebRefId"
             , s."PositionInVerse" AS "Position"
             , s."Greek"         AS "Text"
             , s."Transliteration"
        FROM "unshackled-word"."StepGreekWords" s
        WHERE 1=1
            AND s."LxxRefId" = ANY(@Refs)
        ORDER BY s."LxxRefId", s."PositionInVerse"
        """;

    private const string SelectHebrewOriginal = """
        SELECT s."LxxRefId"      AS "HebRefId"
             , s."PositionInVerse" AS "Position"
             , s."Hebrew"        AS "Text"
             , s."Transliteration"
        FROM "unshackled-word"."StepHebrewWords" s
        WHERE 1=1
            AND s."LxxRefId" = ANY(@Refs)
        ORDER BY s."LxxRefId", s."PositionInVerse"
        """;

    private const string SelectGerman1871 = """
        SELECT w."HebRefId"
             , w."PositionInVerse" AS "Position"
             , w."WordInContext"   AS "Text"
        FROM "unshackled-word"."Elb1871Words" w
        WHERE 1=1
            AND w."HebRefId" = ANY(@Refs)
        ORDER BY w."HebRefId", w."PositionInVerse"
        """;

    private const string SelectGermanRevision = """
        SELECT rv."HebRefId"
             , rw."PositionInVerse" AS "Position"
             , rw."WordInContext"   AS "Text"
        FROM "unshackled-word"."RevisionVerses" rv
            JOIN "unshackled-word"."Revisions" r ON r."Id" = rv."RevisionId"
            JOIN "unshackled-word"."RevisionWords" rw ON rw."RevisionVerseId" = rv."Id"
        WHERE 1=1
            AND r."Code" = 'elb2027'
            AND rv."HebRefId" = ANY(@Refs)
        ORDER BY rv."HebRefId", rw."PositionInVerse"
        """;

    private readonly IDbReader _dbReader;

    public VocabEntryRepository(IDbReader dbReader)
    {
        _dbReader = dbReader;
    }

    public async Task<DateTime?> GetRenderingsRefreshedAtAsync() =>
        await _dbReader.ExecuteScalarAsync<DateTime?>(SelectRefreshedAt);

    /// <summary>Null when the entry is unknown or retired (the maintainer editor asks with <paramref name="includeRetired"/>).</summary>
    public async Task<GetVocabEntryResponse?> GetAsync(int id, bool includeRetired = false)
    {
        var entry = await _dbReader.ReadFirstOrDefaultAsync<VocabEntryDetailResponse>(SelectEntry, new { Id = id, IncludeRetired = includeRetired });

        if (entry is null)
        {
            return null;
        }

        var keys = (await _dbReader.ReadAsListAsync<VocabEntryKeyResponse>(SelectKeys, new { Id = id })).ToList();
        var meanings = (await _dbReader.ReadAsListAsync<VocabMeaningResponse>(SelectMeanings, new { Id = id })).ToList();
        var refreshedAt = await GetRenderingsRefreshedAtAsync();
        var response = new GetVocabEntryResponse { Entry = entry, StrongsKeys = keys, Meanings = meanings, RefreshedAt = refreshedAt };

        if (refreshedAt is not null)
        {
            var rows = (await _dbReader.ReadAsListAsync<RenderingRow>(SelectRenderings, new { Id = id, Max = GetVocabEntryResponse.MaxRenderings })).ToList();
            response.Renderings = rows.Select(r => new VocabRenderingResponse
            {
                GermanKey = r.GermanKey,
                GermanDisplay = r.GermanDisplay,
                LinkCount = r.LinkCount,
                VerseCount = r.VerseCount,
                FromRevisionCount = r.FromRevisionCount,
                Share = r.Share,
                PosTags = r.PosTags.ToList(),
                KeyIsForm = r.KeyIsForm
            }).ToList();
            response.RenderingsTotal = rows.Count == 0 ? 0 : rows[0].RenderingsTotal;
            response.TotalLinks = rows.Count == 0 ? 0 : rows[0].TotalLinks;
        }

        response.SampleVerses = await GetSampleVersesAsync(entry.Track, keys.Select(k => k.StrongsKey).ToArray());

        return response;
    }

    /// <summary>The best sample verse of an entry (human-made link first, then short; same query as <see cref="GetAsync"/>), or null when it has none. The study session shows it with the word highlighted.</summary>
    public async Task<VocabSampleVerseResponse?> GetSampleVerseAsync(int id)
    {
        var track = await _dbReader.ReadFirstOrDefaultAsync<string>(SelectTrack, new { Id = id });

        if (track is null)
        {
            return null;
        }

        var keys = (await _dbReader.ReadAsListAsync<VocabEntryKeyResponse>(SelectKeys, new { Id = id })).Select(k => k.StrongsKey).ToArray();

        return (await GetSampleVersesAsync(track, keys, 1)).FirstOrDefault();
    }

    private async Task<List<VocabSampleVerseResponse>> GetSampleVersesAsync(string track, string[] keys, int take = SampleVerseCount)
    {
        if (keys.Length == 0)
        {
            return [];
        }

        var greek = track == "G";
        var upperKeys = keys.Select(k => k.ToUpperInvariant()).ToArray();
        var likes = keys.Select(k => "%" + k.ToUpperInvariant() + "%").ToArray();
        var hits = (await _dbReader.ReadAsListAsync<HitRow>(greek ? GreekSampleHits : HebrewSampleHits, new { Keys = upperKeys, Likes = likes, Take = take })).ToList();

        if (hits.Count == 0)
        {
            return [];
        }

        var refs = hits.Select(h => h.HebRefId).Distinct().OrderBy(r => r).ToArray();
        var original = (await _dbReader.ReadAsListAsync<WordRow>(greek ? SelectGreekOriginal : SelectHebrewOriginal, new { Refs = refs })).ToLookup(w => w.HebRefId);
        var german1871 = (await _dbReader.ReadAsListAsync<WordRow>(SelectGerman1871, new { Refs = refs })).ToLookup(w => w.HebRefId);
        var germanRevision = (await _dbReader.ReadAsListAsync<WordRow>(SelectGermanRevision, new { Refs = refs })).ToLookup(w => w.HebRefId);

        var verses = new List<VocabSampleVerseResponse>();

        foreach (var verseHits in hits.GroupBy(h => h.HebRefId).OrderByDescending(g => g.Any(h => h.Human)).ThenBy(g => g.Key))
        {
            var refId = verseHits.Key;
            var fromRevision = verseHits.First().FromRevision;
            var originalHits = verseHits.Select(h => h.OriginalPosition).ToHashSet();
            var germanHits = verseHits.Select(h => h.GermanPosition).ToHashSet();

            verses.Add(new VocabSampleVerseResponse
            {
                BibleBookId = refId / 1_000_000,
                Chapter = refId / 1_000 % 1_000,
                Verse = refId % 1_000,
                FromRevision = fromRevision,
                HumanMade = verseHits.Any(h => h.Human),
                Original = original[refId].Select(w => ToWord(w, originalHits)).ToList(),
                German = (fromRevision ? germanRevision[refId] : german1871[refId]).Select(w => ToWord(w, germanHits)).ToList()
            });
        }

        return verses;
    }

    private static VocabSampleWordResponse ToWord(WordRow row, HashSet<int> hits) => new()
    {
        Position = row.Position,
        Text = row.Text,
        Transliteration = row.Transliteration,
        IsHit = hits.Contains(row.Position)
    };

    private sealed class RenderingRow
    {
        public string GermanKey { get; init; } = string.Empty;
        public string GermanDisplay { get; init; } = string.Empty;
        public int LinkCount { get; init; }
        public int VerseCount { get; init; }
        public int FromRevisionCount { get; init; }
        public decimal Share { get; init; }
        public string[] PosTags { get; init; } = [];
        public bool KeyIsForm { get; init; }
        public int TotalLinks { get; init; }
        public int RenderingsTotal { get; init; }
    }

    private sealed class HitRow
    {
        public int HebRefId { get; init; }
        public int GermanPosition { get; init; }
        public int OriginalPosition { get; init; }
        public bool FromRevision { get; init; }
        public bool Human { get; init; }
    }

    private sealed class WordRow
    {
        public int HebRefId { get; init; }
        public int Position { get; init; }
        public string Text { get; init; } = string.Empty;
        public string? Transliteration { get; init; }
    }
}
