using System.Data;
using Dapper;
using UnshackledWord.Application.Features.Vocab;
using UnshackledWord.Application.Features.Vocab.Session;

namespace UnshackledWord.Infrastructure.Vocab;

/// <summary>SQL of the study session engine (roadmap 0022.8). Static methods on an open connection and transaction, like the editor, so tests can roll back.</summary>
public static class VocabSessionRepository
{
    public sealed class CourseRow
    {
        public int Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Track { get; init; } = string.Empty;
    }

    public sealed class SessionRow
    {
        public int Id { get; init; }
        public int UserId { get; init; }
        public int CourseId { get; init; }
        public DateTime? FinishedAt { get; init; }
        public string Plan { get; init; } = string.Empty;
    }

    public sealed class DueCardRow
    {
        public int EntryId { get; init; }
        public string State { get; init; } = string.Empty;
        public double Stability { get; init; }
        public double Difficulty { get; init; }
        public DateTime Due { get; init; }
        public int Reps { get; init; }
        public int Lapses { get; init; }
        public DateTime? LastReview { get; init; }
    }

    public sealed class EntryRow
    {
        public int Id { get; init; }
        public string Track { get; init; } = string.Empty;
        public string Lemma { get; init; } = string.Empty;
        public string? DictionaryForm { get; init; }
        public string? Transliteration { get; init; }
        public string? Pos { get; init; }
        public int? Rank { get; init; }
        public int ContentVersion { get; init; }
        public string? AudioObjectKey { get; init; }
        public bool Retired { get; init; }
    }

    public sealed class MeaningRow
    {
        public int EntryId { get; init; }
        public string German { get; init; } = string.Empty;
        public int Rank { get; init; }
        public int? Count { get; init; }
        public decimal? Share { get; init; }
    }

    public sealed class DistractorRow
    {
        public int EntryId { get; init; }
        public string? Pos { get; init; }
        public int Rank { get; init; }
        public string TopMeaning { get; init; } = string.Empty;
    }

    public sealed record ReviewLogEntry(
        int UserId, int EntryId, int SessionId, string QuizType, bool IsCorrect, string? Given, int? ResponseMs, int? Rating,
        string? StateBefore, double? StabilityBefore, double? DifficultyBefore, double? StabilityAfter, double? DifficultyAfter, DateTime ReviewedAt);

    private const string SelectCourse = """
        SELECT c."Id"
             , c."Code"
             , c."Track"::text AS "Track"
        FROM "unshackled-word"."VocabCourses" c
        WHERE c."Code" = @Code
        """;

    private const string SelectSessionById = """
        SELECT s."Id"
             , s."UserId"
             , s."CourseId"
             , s."FinishedAt"
             , s."Plan"::text AS "Plan"
        FROM "unshackled-word"."VocabSessions" s
        WHERE s."Id" = @Id
            AND s."UserId" = @UserId
        FOR UPDATE
        """;

    private const string SelectOpenSession = """
        SELECT s."Id"
             , s."UserId"
             , s."CourseId"
             , s."FinishedAt"
             , s."Plan"::text AS "Plan"
        FROM "unshackled-word"."VocabSessions" s
        WHERE 1=1
            AND s."UserId" = @UserId
            AND s."CourseId" = @CourseId
            AND s."FinishedAt" IS NULL
        FOR UPDATE
        """;

    private const string InsertSession = """
        INSERT INTO "unshackled-word"."VocabSessions" ("UserId", "CourseId", "StartedAt", "Plan")
        VALUES (@UserId, @CourseId, @Now, @Plan::jsonb)
        RETURNING "Id"
        """;

    private const string UpdateSession = """
        UPDATE "unshackled-word"."VocabSessions"
        SET "Plan" = @Plan::jsonb
          , "FinishedAt" = @FinishedAt
        WHERE "Id" = @Id
        """;

    // Studiable = ranked, not a morpheme, not retired, with an accepted meaning. A card in state "new" (introduction served, never quizzed,
    // e.g. an abandoned session) counts as new again, so it cannot get stuck.
    private const string SelectNewCandidates = """
        SELECT e."Id"
        FROM "unshackled-word"."VocabEntries" e
            LEFT JOIN "unshackled-word"."VocabCards" c ON c."VocabEntryId" = e."Id" AND c."UserId" = @UserId
        WHERE 1=1
            AND e."Track" = @Track::char(1)
            AND e."FrequencyRank" IS NOT NULL
            AND NOT e."IsMorpheme"
            AND e."RetiredAt" IS NULL
            AND EXISTS (SELECT 1 FROM "unshackled-word"."VocabMeanings" m WHERE m."VocabEntryId" = e."Id" AND m."IsAccepted")
            AND (c."UserId" IS NULL OR c."State" = 'new')
        ORDER BY e."FrequencyRank"
        LIMIT @Take
        """;

    private const string SelectDueCards = """
        SELECT c."VocabEntryId" AS "EntryId"
             , c."State"
             , c."Stability"
             , c."Difficulty"
             , c."Due"
             , c."Reps"
             , c."Lapses"
             , c."LastReview"
        FROM "unshackled-word"."VocabCards" c
            JOIN "unshackled-word"."VocabEntries" e ON e."Id" = c."VocabEntryId"
        WHERE 1=1
            AND c."UserId" = @UserId
            AND c."State" <> 'new'
            AND c."Due" <= @Now
            AND e."Track" = @Track::char(1)
            AND e."RetiredAt" IS NULL
            AND EXISTS (SELECT 1 FROM "unshackled-word"."VocabMeanings" m WHERE m."VocabEntryId" = e."Id" AND m."IsAccepted")
        """;

    private const string SelectCard = """
        SELECT c."VocabEntryId" AS "EntryId"
             , c."State"
             , c."Stability"
             , c."Difficulty"
             , c."Due"
             , c."Reps"
             , c."Lapses"
             , c."LastReview"
        FROM "unshackled-word"."VocabCards" c
        WHERE 1=1
            AND c."UserId" = @UserId
            AND c."VocabEntryId" = @EntryId
        FOR UPDATE
        """;

    private const string InsertCard = """
        INSERT INTO "unshackled-word"."VocabCards" ("UserId", "VocabEntryId", "State", "Stability", "Difficulty", "Due", "Reps", "Lapses", "LastReview", "CreatedAt", "SeenContentVersion")
        VALUES (@UserId, @EntryId, 'new', 0, 0, @Now, 0, 0, NULL, @Now, @ContentVersion)
        ON CONFLICT ("UserId", "VocabEntryId") DO NOTHING
        """;

    private const string UpdateCard = """
        UPDATE "unshackled-word"."VocabCards"
        SET "State" = @State
          , "Stability" = @Stability
          , "Difficulty" = @Difficulty
          , "Due" = @Due
          , "Reps" = @Reps
          , "Lapses" = @Lapses
          , "LastReview" = @LastReview
        WHERE 1=1
            AND "UserId" = @UserId
            AND "VocabEntryId" = @EntryId
        """;

    private const string InsertReviewLog = """
        INSERT INTO "unshackled-word"."VocabReviewLog"
            ("UserId", "VocabEntryId", "SessionId", "QuizType", "IsCorrect", "Given", "ResponseMs", "Rating", "StateBefore", "StabilityBefore", "DifficultyBefore", "StabilityAfter", "DifficultyAfter", "ReviewedAt")
        VALUES
            (@UserId, @EntryId, @SessionId, @QuizType, @IsCorrect, @Given, @ResponseMs, @Rating, @StateBefore, @StabilityBefore, @DifficultyBefore, @StabilityAfter, @DifficultyAfter, @ReviewedAt)
        """;

    private const string SelectEntries = """
        SELECT e."Id"
             , e."Track"::text AS "Track"
             , e."Lemma"
             , e."DictionaryForm"
             , e."Transliteration"
             , e."Pos"
             , e."FrequencyRank" AS "Rank"
             , e."ContentVersion"
             , e."AudioObjectKey"
             , (e."RetiredAt" IS NOT NULL) AS "Retired"
        FROM "unshackled-word"."VocabEntries" e
        WHERE e."Id" = ANY(@Ids)
        """;

    private const string SelectAcceptedMeanings = """
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

    // Distractor candidates: ranked entries of the track with an accepted meaning; the shown text is the top-ranked accepted meaning only.
    private const string DistractorSelect = """
        SELECT e."Id"            AS "EntryId"
             , e."Pos"
             , e."FrequencyRank" AS "Rank"
             , m."German"        AS "TopMeaning"
        FROM "unshackled-word"."VocabEntries" e
            CROSS JOIN LATERAL (SELECT m."German"
                                FROM "unshackled-word"."VocabMeanings" m
                                WHERE m."VocabEntryId" = e."Id" AND m."IsAccepted"
                                ORDER BY m."Rank"
                                LIMIT 1) m
        WHERE 1=1
            AND e."Track" = @Track::char(1)
            AND e."FrequencyRank" IS NOT NULL
            AND NOT e."IsMorpheme"
            AND e."RetiredAt" IS NULL
            AND e."Id" <> @Id
        """;

    private const string DistractorWave = DistractorSelect + """

            AND e."Id" = ANY(@WaveIds)
        """;

    private const string DistractorNearby = DistractorSelect + """

            AND e."Pos" IS NOT DISTINCT FROM @Pos
            AND e."FrequencyRank" BETWEEN @RankFrom AND @RankTo
        """;

    private const string DistractorSamePos = DistractorSelect + """

            AND e."Pos" IS NOT DISTINCT FROM @Pos
        ORDER BY random()
        LIMIT @Take
        """;

    private const string DistractorAny = DistractorSelect + """

        ORDER BY random()
        LIMIT @Take
        """;

    public static async Task<CourseRow?> GetCourseAsync(IDbConnection c, IDbTransaction t, string code) =>
        await c.QueryFirstOrDefaultAsync<CourseRow>(SelectCourse, new { Code = code }, t);

    public static async Task<SessionRow?> GetSessionAsync(IDbConnection c, IDbTransaction t, int userId, int id) =>
        await c.QueryFirstOrDefaultAsync<SessionRow>(SelectSessionById, new { Id = id, UserId = userId }, t);

    public static async Task<SessionRow?> GetOpenSessionAsync(IDbConnection c, IDbTransaction t, int userId, int courseId) =>
        await c.QueryFirstOrDefaultAsync<SessionRow>(SelectOpenSession, new { UserId = userId, CourseId = courseId }, t);

    public static async Task<int> InsertSessionAsync(IDbConnection c, IDbTransaction t, int userId, int courseId, DateTime now, string planJson) =>
        await c.QuerySingleAsync<int>(InsertSession, new { UserId = userId, CourseId = courseId, Now = now, Plan = planJson }, t);

    public static async Task SaveSessionAsync(IDbConnection c, IDbTransaction t, int id, string planJson, DateTime? finishedAt) =>
        await c.ExecuteAsync(UpdateSession, new { Id = id, Plan = planJson, FinishedAt = finishedAt }, t);

    public static async Task<List<int>> GetNewCandidatesAsync(IDbConnection c, IDbTransaction t, int userId, string track, int take) =>
        (await c.QueryAsync<int>(SelectNewCandidates, new { UserId = userId, Track = track, Take = take }, t)).ToList();

    public static async Task<List<DueCardRow>> GetDueCardsAsync(IDbConnection c, IDbTransaction t, int userId, string track, DateTime now) =>
        (await c.QueryAsync<DueCardRow>(SelectDueCards, new { UserId = userId, Track = track, Now = now }, t)).ToList();

    public static async Task<DueCardRow?> GetCardForUpdateAsync(IDbConnection c, IDbTransaction t, int userId, int entryId) =>
        await c.QueryFirstOrDefaultAsync<DueCardRow>(SelectCard, new { UserId = userId, EntryId = entryId }, t);

    /// <summary>Creates the card of an introduced entry (state new, due now); an existing card is left alone.</summary>
    public static async Task EnsureCardAsync(IDbConnection c, IDbTransaction t, int userId, int entryId, int contentVersion, DateTime now) =>
        await c.ExecuteAsync(InsertCard, new { UserId = userId, EntryId = entryId, ContentVersion = contentVersion, Now = now }, t);

    public static async Task UpdateCardAsync(IDbConnection c, IDbTransaction t, int userId, int entryId, VocabCard card) =>
        await c.ExecuteAsync(UpdateCard, new
        {
            UserId = userId,
            EntryId = entryId,
            State = card.StateName,
            card.Stability,
            card.Difficulty,
            Due = card.Due.UtcDateTime,
            card.Reps,
            card.Lapses,
            LastReview = card.LastReview?.UtcDateTime,
        }, t);

    public static async Task InsertReviewLogAsync(IDbConnection c, IDbTransaction t, ReviewLogEntry e) =>
        await c.ExecuteAsync(InsertReviewLog, e, t);

    public static async Task<Dictionary<int, EntryRow>> GetEntriesAsync(IDbConnection c, IDbTransaction t, IEnumerable<int> ids) =>
        (await c.QueryAsync<EntryRow>(SelectEntries, new { Ids = ids.ToArray() }, t)).ToDictionary(e => e.Id);

    public static async Task<ILookup<int, MeaningRow>> GetAcceptedMeaningsAsync(IDbConnection c, IDbTransaction t, IEnumerable<int> ids) =>
        (await c.QueryAsync<MeaningRow>(SelectAcceptedMeanings, new { Ids = ids.ToArray() }, t)).ToLookup(m => m.EntryId);

    /// <summary>A superset of the distractor candidates of one prompt entry (the wave, the rank band, a random sample of the same POS and of the course); the picker applies the tiers and the rule.</summary>
    public static async Task<List<VocabDistractorCandidate>> GetDistractorPoolAsync(
        IDbConnection c, IDbTransaction t, string track, int entryId, string? pos, int rank, IReadOnlyCollection<int> waveIds)
    {
        const int sample = 60;
        var rows = new List<DistractorRow>();
        rows.AddRange(await c.QueryAsync<DistractorRow>(DistractorWave, new { Track = track, Id = entryId, WaveIds = waveIds.ToArray() }, t));
        rows.AddRange(await c.QueryAsync<DistractorRow>(DistractorNearby, new { Track = track, Id = entryId, Pos = pos, RankFrom = rank - VocabDistractorPicker.NearbyRanks, RankTo = rank + VocabDistractorPicker.NearbyRanks }, t));
        rows.AddRange(await c.QueryAsync<DistractorRow>(DistractorSamePos, new { Track = track, Id = entryId, Pos = pos, Take = sample }, t));
        rows.AddRange(await c.QueryAsync<DistractorRow>(DistractorAny, new { Track = track, Id = entryId, Take = sample }, t));

        return rows
            .GroupBy(r => r.EntryId)
            .Select(g => g.First())
            .Select(r => new VocabDistractorCandidate(r.EntryId, r.Pos, r.Rank, r.TopMeaning))
            .ToList();
    }
}
