using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Vocab;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Domain.WebApi.Vocab;

namespace UnshackledWord.Infrastructure.Vocab;

/// <summary>
/// Read side of the vocab editor (roadmap 0022.4): one entry with everything the editor page needs, and the filtered editor list.
/// Renderings and sample verses come from <see cref="VocabEntryRepository"/>, the same logic the public entry endpoint uses.
/// </summary>
public sealed class VocabEditorRepository
{
    private const string SelectEntry = """
        SELECT e."Id"
             , e."Track"::text                     AS "Track"
             , e."FrequencyRank"                   AS "Rank"
             , e."Lemma"
             , e."Transliteration"
             , e."DictionaryForm"
             , e."DictionaryFormOrigin"
             , e."Pos"
             , e."Occurrences"
             , e."IsProperName"
             , e."IsMorpheme"
             , (e."AudioObjectKey" IS NOT NULL)    AS "HasAudio"
             , e."AudioObjectKey"
             , e."ReviewedAt"
             , e."ReviewedByUserId"
             , e."RetiredAt"
             , e."ContentVersion"
             , e."UpdatedAt"
             , e."UpdatedByUserId"
        FROM "unshackled-word"."VocabEntries" e
        WHERE 1=1
            AND e."Id" = @Id
        """;

    private const string SelectMeanings = """
        SELECT m."Id"
             , m."German"
             , m."Rank"
             , m."IsAccepted"
             , m."Origin"
             , m."IsPinned"
             , m."Count"
             , m."Share"
        FROM "unshackled-word"."VocabMeanings" m
        WHERE 1=1
            AND m."VocabEntryId" = @Id
        ORDER BY m."Rank", m."Id"
        """;

    // The lexicon line a key stands for, found with the same key rule as the generator.
    private const string SelectKeys = """
        SELECT vs."StrongsKey"
             , vs."Occurrences"
             , l."OriginalWord"    AS "Lemma"
             , l."Transliteration"
        FROM "unshackled-word"."VocabStrongs" vs
            LEFT JOIN LATERAL (
                SELECT x."OriginalWord", x."Transliteration"
                FROM "unshackled-word"."StepStrongsLexicon" x
                WHERE 1=1
                    AND x."OriginalWord" <> ''
                    AND x."Number" = substring(vs."StrongsKey" FROM '^[GH](\d+)')::int
                    AND (vs."Lang"::text || CASE WHEN x."Number" >= 10000 THEN x."Number"::text ELSE lpad(x."Number"::text, 4, '0') END || upper(COALESCE(x."Extra", ''))) = vs."StrongsKey"
                ORDER BY x."Id"
                LIMIT 1
            ) l ON true
        WHERE 1=1
            AND vs."VocabEntryId" = @Id
        ORDER BY vs."Occurrences" DESC, vs."StrongsKey"
        """;

    private const string SelectLearners = """
        SELECT count(*)::int
        FROM "unshackled-word"."VocabCards" c
        WHERE 1=1
            AND c."VocabEntryId" = @Id
        """;

    private const string SelectCourseTrack = """
        SELECT c."Track"::text
        FROM "unshackled-word"."VocabCourses" c
        WHERE 1=1
            AND c."Code" = @Code
        """;

    // @Normalized: VocabText.Normalize(q), @Like: LIKE-escaped lower-case q
    private const string Filter = """
        FROM "unshackled-word"."VocabEntries" e
        WHERE 1=1
            AND (@Track::text IS NULL OR e."Track" = @Track::text)
            AND (e."RetiredAt" IS NOT NULL) = @Retired
            AND (NOT @Unreviewed OR e."ReviewedAt" IS NULL)
            AND (NOT @NoAudio OR e."AudioObjectKey" IS NULL)
            AND (NOT @FormMissing OR e."DictionaryForm" IS NULL)
            AND (NOT @FormFromAgent OR e."DictionaryFormOrigin" = 'agent')
            AND (NOT @MultiKey OR (SELECT count(*) FROM "unshackled-word"."VocabStrongs" vs WHERE vs."VocabEntryId" = e."Id") > 1)
            AND (@RankFrom::int IS NULL OR e."FrequencyRank" >= @RankFrom::int)
            AND (@RankTo::int IS NULL OR e."FrequencyRank" <= @RankTo::int)
            AND (@Normalized::text IS NULL
                 OR e."LemmaNormalized" LIKE @NormalizedLike
                 OR lower(e."Transliteration") LIKE @Like
                 OR EXISTS (SELECT 1
                            FROM "unshackled-word"."VocabMeanings" m
                            WHERE m."VocabEntryId" = e."Id" AND m."IsAccepted" AND lower(m."German") LIKE @LikeContains))
        """;

    private const string SelectCount = "SELECT count(*)::int " + Filter;

    private const string SelectItems = """
        SELECT e."Id"
             , e."Track"::text                     AS "Track"
             , e."FrequencyRank"                   AS "Rank"
             , e."Lemma"
             , e."Transliteration"
             , e."DictionaryForm"
             , e."DictionaryFormOrigin"
             , e."Occurrences"
             , (e."AudioObjectKey" IS NOT NULL)    AS "HasAudio"
             , e."AudioObjectKey"
             , (e."ReviewedAt" IS NOT NULL)        AS "Reviewed"
             , (e."RetiredAt" IS NOT NULL)         AS "Retired"
             , e."ContentVersion"
             , (SELECT count(*)::int FROM "unshackled-word"."VocabCards" c WHERE c."VocabEntryId" = e."Id") AS "Learners"
        """ + "\n" + Filter + """

        ORDER BY e."Track", e."FrequencyRank" NULLS LAST, e."Id"
        OFFSET @Skip LIMIT @Take
        """;

    private const string SelectListMeanings = """
        SELECT m."VocabEntryId" AS "EntryId"
             , m."German"
             , m."Rank"
             , m."Count"
             , m."Share"
        FROM "unshackled-word"."VocabMeanings" m
        WHERE 1=1
            AND m."VocabEntryId" = ANY(@Ids)
            AND m."IsAccepted"
        ORDER BY m."VocabEntryId", m."Rank"
        """;

    private const string SelectListKeys = """
        SELECT vs."VocabEntryId" AS "EntryId"
             , vs."StrongsKey"
        FROM "unshackled-word"."VocabStrongs" vs
        WHERE 1=1
            AND vs."VocabEntryId" = ANY(@Ids)
        ORDER BY vs."VocabEntryId", vs."StrongsKey"
        """;

    private readonly IDbReader _dbReader;
    private readonly VocabEntryRepository _entries;

    private readonly IObjectStorage? _storage;

    public VocabEditorRepository(IDbReader dbReader, VocabEntryRepository entries, IObjectStorage? storage = null)
    {
        _dbReader = dbReader;
        _entries = entries;
        _storage = storage;
    }

    /// <summary>Null when the entry does not exist. Retired entries are returned too.</summary>
    public async Task<GetVocabEditorEntryResponse?> GetAsync(int id)
    {
        var entry = await _dbReader.ReadFirstOrDefaultAsync<VocabEditorEntryResponse>(SelectEntry, new { Id = id });

        if (entry is null)
        {
            return null;
        }

        entry.AudioUrl = VocabAudio.UrlFor(_storage, entry.AudioObjectKey);

        var response = new GetVocabEditorEntryResponse
        {
            Entry = entry,
            Meanings = (await _dbReader.ReadAsListAsync<VocabEditorMeaningResponse>(SelectMeanings, new { Id = id })).ToList(),
            StrongsKeys = (await _dbReader.ReadAsListAsync<VocabEditorKeyResponse>(SelectKeys, new { Id = id })).ToList(),
            Learners = await _dbReader.ExecuteScalarAsync<int>(SelectLearners, new { Id = id })
        };

        var reference = await _entries.GetAsync(id, includeRetired: true);

        if (reference is not null)
        {
            response.Renderings = reference.Renderings;
            response.RenderingsTotal = reference.RenderingsTotal;
            response.TotalLinks = reference.TotalLinks;
            response.RefreshedAt = reference.RefreshedAt;
            response.SampleVerses = reference.SampleVerses;
        }

        return response;
    }

    /// <summary>Null when the course code is unknown.</summary>
    public async Task<ListVocabEditorEntriesResponse?> ListAsync(ListVocabEditorEntriesRequest req)
    {
        string? track = null;

        if (req.Course is not null)
        {
            track = await _dbReader.ReadFirstOrDefaultAsync<string>(SelectCourseTrack, new { Code = req.Course });

            if (track is null)
            {
                return null;
            }
        }

        var q = req.Q?.Trim();
        var hasQuery = !string.IsNullOrEmpty(q);
        var normalized = hasQuery ? VocabText.Normalize(q) : null;
        var lower = hasQuery ? q!.ToLowerInvariant() : null;

        var parameters = new
        {
            Track = track,
            req.Retired,
            req.Unreviewed,
            req.NoAudio,
            req.FormMissing,
            req.FormFromAgent,
            req.MultiKey,
            req.RankFrom,
            req.RankTo,
            Normalized = hasQuery ? normalized ?? string.Empty : null,
            NormalizedLike = Escape(normalized) + "%",
            Like = Escape(lower) + "%",
            LikeContains = "%" + Escape(lower) + "%",
            req.Skip,
            req.Take
        };

        var response = new ListVocabEditorEntriesResponse
        {
            Skip = req.Skip,
            Take = req.Take,
            Total = await _dbReader.ExecuteScalarAsync<int>(SelectCount, parameters),
            Items = (await _dbReader.ReadAsListAsync<VocabEditorListItemResponse>(SelectItems, parameters)).ToList()
        };

        if (response.Items.Count > 0)
        {
            var ids = response.Items.Select(i => i.Id).ToArray();
            var meanings = (await _dbReader.ReadAsListAsync<ListMeaningRow>(SelectListMeanings, new { Ids = ids })).ToLookup(m => m.EntryId);
            var keys = (await _dbReader.ReadAsListAsync<ListKeyRow>(SelectListKeys, new { Ids = ids })).ToLookup(k => k.EntryId);

            foreach (var item in response.Items)
            {
                item.AudioUrl = VocabAudio.UrlFor(_storage, item.AudioObjectKey);
                item.Meanings = meanings[item.Id].Select(m => new VocabMeaningResponse { German = m.German, Rank = m.Rank, Count = m.Count, Share = m.Share }).ToList();
                item.StrongsKeys = keys[item.Id].Select(k => k.StrongsKey).ToList();
            }
        }

        return response;
    }

    private static string Escape(string? text) =>
        (text ?? string.Empty).Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private sealed class ListMeaningRow
    {
        public int EntryId { get; init; }
        public string German { get; init; } = string.Empty;
        public int Rank { get; init; }
        public int? Count { get; init; }
        public decimal? Share { get; init; }
    }

    private sealed class ListKeyRow
    {
        public int EntryId { get; init; }
        public string StrongsKey { get; init; } = string.Empty;
    }
}
