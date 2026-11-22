using Dapper;
using UnshackledWord.Application.Abstractions;

namespace UnshackledWord.Infrastructure.Vocab;

public sealed class VocabFormCourse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Track { get; set; } = string.Empty;
}

/// <summary>An entry that waits for a dictionary form (missing or generated), with the STEP lexicon line of its main Strong's key.</summary>
public sealed class VocabFormCandidate
{
    public int Id { get; set; }
    public string Lemma { get; set; } = string.Empty;
    public string? Pos { get; set; }
    public int Rank { get; set; }
    public string? Lexicon { get; set; }
}

/// <summary>Reads for the dictionary-form agent run (roadmap 0022.13); the writes go through <see cref="VocabEntryEditor"/>.</summary>
public sealed class VocabFormRepository
{
    private const string SelectCourse = """
        SELECT c."Id", c."Code", c."Track"
        FROM "unshackled-word"."VocabCourses" c
        WHERE 1=1
            AND (c."Code" = @Code::varchar OR c."Id" = @Id::int4)
        """;

    /// <summary>
    /// Entries of the course in the rank range whose form is missing or generated (never agent or manual), in rank order. The lexicon line
    /// comes from the key of the entry with most occurrences: Strong's key = track letter, number (4 digits) and extra letter.
    /// </summary>
    private const string SelectCandidates = """
        SELECT e."Id"
             , e."Lemma"
             , e."Pos"
             , e."FrequencyRank" AS "Rank"
             , (SELECT l."Lexicon"
                FROM "unshackled-word"."VocabStrongs" vs
                    JOIN "unshackled-word"."StepStrongsLexicon" l
                        ON  l."Number" = regexp_replace(vs."StrongsKey", '\D', '', 'g')::int
                        AND upper(COALESCE(l."Extra", '')) = regexp_replace(vs."StrongsKey", '^[GH]\d+', '')
                        AND (l."LanguageId" = 2) = (vs."Lang" = 'G')
                WHERE 1=1
                    AND vs."VocabEntryId" = e."Id"
                    AND l."OriginalWord" = e."Lemma"
                ORDER BY vs."Occurrences" DESC, vs."StrongsKey", l."Id"
                LIMIT 1) AS "Lexicon"
        FROM "unshackled-word"."VocabEntries" e
        WHERE 1=1
            AND e."Track" = @Track
            AND e."RetiredAt" IS NULL
            AND e."FrequencyRank" >= @FromRank
            AND (@ToRank::int4 IS NULL OR e."FrequencyRank" <= @ToRank)
            AND (e."DictionaryForm" IS NULL OR e."DictionaryFormOrigin" = 'generated')
        ORDER BY e."FrequencyRank"
        LIMIT @Limit
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public VocabFormRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<VocabFormCourse?> GetCourseAsync(string? code, int? id, CancellationToken token = default)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(token);

        return await connection.QuerySingleOrDefaultAsync<VocabFormCourse>(new CommandDefinition(SelectCourse, new { Code = code, Id = id }, cancellationToken: token));
    }

    public async Task<IReadOnlyList<VocabFormCandidate>> SelectCandidatesAsync(string track, int fromRank, int? toRank, int limit, CancellationToken token = default)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(token);

        return (await connection.QueryAsync<VocabFormCandidate>(new CommandDefinition(SelectCandidates, new { Track = track, FromRank = fromRank, ToRank = toRank, Limit = limit }, cancellationToken: token))).AsList();
    }
}
