using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.WebApi.Vocab;

namespace UnshackledWord.Infrastructure.Vocab;

/// <summary>Read side of the public vocab endpoints (roadmap 0022.1): the courses and the entries of a course in rank order.</summary>
public sealed class VocabCourseRepository
{
    private const string SelectCourses = """
        SELECT c."Code"
             , c."Track"
             , c."NameDe"
             , c."NameEn"
             , count(e."Id")::int                      AS "TotalEntries"
             , COALESCE(sum(e."Occurrences"), 0)::int  AS "TotalOccurrences"
        FROM "unshackled-word"."VocabCourses" c
            LEFT JOIN "unshackled-word"."VocabEntries" e ON e."Track" = c."Track" AND e."FrequencyRank" IS NOT NULL
        GROUP BY c."Id", c."Code", c."Track", c."NameDe", c."NameEn"
        ORDER BY c."Id"
        """;

    private const string SelectCourseTrack = """
        SELECT c."Track"
        FROM "unshackled-word"."VocabCourses" c
        WHERE c."Code" = @Code
        """;

    // @Normalized: VocabText.Normalize(q), @Like: LIKE-escaped lower-case q
    private const string Filter = """
        FROM "unshackled-word"."VocabEntries" e
        WHERE 1=1
            AND e."Track" = @Track
            AND e."FrequencyRank" IS NOT NULL
            AND (@Normalized::text IS NULL
                 OR e."LemmaNormalized" LIKE @NormalizedLike
                 OR lower(e."Transliteration") LIKE @Like
                 OR EXISTS (SELECT 1
                            FROM "unshackled-word"."VocabMeanings" m
                            WHERE m."VocabEntryId" = e."Id" AND m."IsAccepted" AND lower(m."German") LIKE @LikeContains))
        """;

    private const string SelectCount = "SELECT count(*)::int " + Filter;

    private const string SelectEntries = """
        SELECT e."Id"
             , e."FrequencyRank"                   AS "Rank"
             , e."Lemma"
             , e."Transliteration"
             , e."DictionaryForm"
             , e."Occurrences"
             , (e."AudioObjectKey" IS NOT NULL)    AS "HasAudio"
             , (e."ReviewedAt" IS NOT NULL)        AS "Reviewed"
        """ + "\n" + Filter + """

        ORDER BY e."FrequencyRank"
        OFFSET @Skip LIMIT @Take
        """;

    private const string SelectMeanings = """
        SELECT m."VocabEntryId" AS "EntryId"
             , m."German"
             , m."Rank"
        FROM "unshackled-word"."VocabMeanings" m
        WHERE 1=1
            AND m."VocabEntryId" = ANY(@Ids)
            AND m."IsAccepted"
        ORDER BY m."VocabEntryId", m."Rank"
        """;

    private const string SelectKeys = """
        SELECT vs."VocabEntryId" AS "EntryId"
             , vs."StrongsKey"
        FROM "unshackled-word"."VocabStrongs" vs
        WHERE vs."VocabEntryId" = ANY(@Ids)
        ORDER BY vs."VocabEntryId", vs."StrongsKey"
        """;

    private readonly IDbReader _dbReader;

    public VocabCourseRepository(IDbReader dbReader)
    {
        _dbReader = dbReader;
    }

    public async Task<List<VocabCourseResponse>> GetCoursesAsync()
    {
        return (await _dbReader.ReadAsListAsync<VocabCourseResponse>(SelectCourses)).ToList();
    }

    /// <summary>Null when the course code is unknown.</summary>
    public async Task<GetVocabCourseEntriesResponse?> GetEntriesAsync(string code, int skip, int take, string? normalized, string? lowerQuery)
    {
        var track = await _dbReader.ReadFirstOrDefaultAsync<string>(SelectCourseTrack, new { Code = code });

        if (track is null)
        {
            return null;
        }

        var parameters = new
        {
            Track = track,
            Normalized = normalized,
            NormalizedLike = Escape(normalized) + "%",
            Like = Escape(lowerQuery) + "%",
            LikeContains = "%" + Escape(lowerQuery) + "%",
            Skip = skip,
            Take = take
        };

        var total = await _dbReader.ExecuteScalarAsync<int>(SelectCount, parameters);
        var items = (await _dbReader.ReadAsListAsync<VocabCourseEntryResponse>(SelectEntries, parameters)).ToList();

        if (items.Count > 0)
        {
            var ids = items.Select(i => i.Id).ToArray();
            var meanings = (await _dbReader.ReadAsListAsync<MeaningRow>(SelectMeanings, new { Ids = ids })).ToLookup(m => m.EntryId);
            var keys = (await _dbReader.ReadAsListAsync<KeyRow>(SelectKeys, new { Ids = ids })).ToLookup(k => k.EntryId);

            foreach (var item in items)
            {
                item.Meanings = meanings[item.Id].Select(m => new VocabMeaningResponse { German = m.German, Rank = m.Rank }).ToList();
                item.StrongsKeys = keys[item.Id].Select(k => k.StrongsKey).ToList();
            }
        }

        return new GetVocabCourseEntriesResponse { Code = code, Total = total, Skip = skip, Take = take, Items = items };
    }

    private static string Escape(string? text) =>
        (text ?? string.Empty).Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private sealed class MeaningRow
    {
        public int EntryId { get; init; }
        public string German { get; init; } = string.Empty;
        public int Rank { get; init; }
    }

    private sealed class KeyRow
    {
        public int EntryId { get; init; }
        public string StrongsKey { get; init; } = string.Empty;
    }
}
