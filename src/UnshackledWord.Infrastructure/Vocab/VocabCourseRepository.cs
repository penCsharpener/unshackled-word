using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Vocab;
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
                            WHERE m."VocabEntryId" = e."Id" AND m."IsAccepted" AND lower(COALESCE(m."GermanOverride", m."German")) LIKE @LikeContains))
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
             , e."AudioObjectKey"
             , (e."ReviewedAt" IS NOT NULL)        AS "Reviewed"
        """ + "\n" + Filter + """

        ORDER BY e."FrequencyRank"
        OFFSET @Skip LIMIT @Take
        """;

    private const string SelectMeanings = """
        SELECT m."VocabEntryId" AS "EntryId"
             , COALESCE(m."GermanOverride", m."German") AS "German"
             , m."Rank"
             , m."Count"
             , m."Share"
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

    private readonly IObjectStorage? _storage;

    public VocabCourseRepository(IDbReader dbReader, IObjectStorage? storage = null)
    {
        _dbReader = dbReader;
        _storage = storage;
    }

    private const string SelectUserCards = """
        SELECT e."Track"::text AS "Track"
             , e."Occurrences"
             , c."Stability"
             , (c."Due" <= now() AND EXISTS (SELECT 1 FROM "unshackled-word"."VocabMeanings" m WHERE m."VocabEntryId" = e."Id" AND m."IsAccepted")) AS "IsDue"
        FROM "unshackled-word"."VocabCards" c
            JOIN "unshackled-word"."VocabEntries" e ON e."Id" = c."VocabEntryId"
        WHERE 1=1
            AND c."UserId" = @UserId
            AND c."State" <> 'new'
            AND e."RetiredAt" IS NULL
            AND e."FrequencyRank" IS NOT NULL
        """;

    // same rules as the session's new-word candidates
    private const string SelectUserNewAvailable = """
        SELECT e."Track"::text AS "Track"
             , count(*)::int AS "Count"
        FROM "unshackled-word"."VocabEntries" e
            LEFT JOIN "unshackled-word"."VocabCards" c ON c."VocabEntryId" = e."Id" AND c."UserId" = @UserId
        WHERE 1=1
            AND e."FrequencyRank" IS NOT NULL
            AND NOT e."IsMorpheme"
            AND e."RetiredAt" IS NULL
            AND EXISTS (SELECT 1 FROM "unshackled-word"."VocabMeanings" m WHERE m."VocabEntryId" = e."Id" AND m."IsAccepted")
            AND (c."UserId" IS NULL OR c."State" = 'new')
        GROUP BY e."Track"
        """;

    private sealed class UserCardRow
    {
        public string Track { get; init; } = string.Empty;
        public int Occurrences { get; init; }
        public double Stability { get; init; }
        public bool IsDue { get; init; }
    }

    private sealed class NewCountRow
    {
        public string Track { get; init; } = string.Empty;
        public int Count { get; init; }
    }

    /// <summary>The courses; with a <paramref name="userId"/> also known, due, new available and coverage per course.</summary>
    public async Task<List<VocabCourseResponse>> GetCoursesAsync(int? userId = null)
    {
        var courses = (await _dbReader.ReadAsListAsync<VocabCourseResponse>(SelectCourses)).ToList();

        if (userId is null)
        {
            return courses;
        }

        var cards = (await _dbReader.ReadAsListAsync<UserCardRow>(SelectUserCards, new { UserId = userId })).ToList();
        var newCounts = (await _dbReader.ReadAsListAsync<NewCountRow>(SelectUserNewAvailable, new { UserId = userId })).ToDictionary(x => x.Track, x => x.Count);

        foreach (var course in courses)
        {
            var own = cards.Where(x => x.Track == course.Track).ToList();
            var known = own.Where(x => VocabScheduler.StrengthFromStability(x.Stability) >= 3).ToList();
            course.Known = known.Count;
            course.Due = own.Count(x => x.IsDue);
            course.NewAvailable = newCounts.GetValueOrDefault(course.Track);
            course.Coverage = course.TotalOccurrences == 0 ? 0 : Math.Round(100.0 * known.Sum(x => (long)x.Occurrences) / course.TotalOccurrences, 1);
        }

        return courses;
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
                item.AudioUrl = VocabAudio.UrlFor(_storage, item.AudioObjectKey);
                item.Meanings = meanings[item.Id].Select(m => new VocabMeaningResponse { German = m.German, Rank = m.Rank, Count = m.Count, Share = m.Share }).ToList();
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
        public int? Count { get; init; }
        public decimal? Share { get; init; }
    }

    private sealed class KeyRow
    {
        public int EntryId { get; init; }
        public string StrongsKey { get; init; } = string.Empty;
    }
}
