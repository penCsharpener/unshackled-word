using UnshackledWord.Application.Abstractions;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Revision;

/// <summary>An active variant with the numbers the overview lists need.</summary>
public sealed class RevisionOverviewVariantRow
{
    public int RevisionVerseId { get; set; }
    public int HebRefId { get; set; }
    public int Level { get; set; }
    public int Version { get; set; }
    public string BaseTextHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? MappingReviewedAt { get; set; }
    public int UnlinkedGermanWords { get; set; }
    public int UnlinkedOriginalWords { get; set; }
}

public sealed class RevisionOverviewBaseWordRow
{
    public int HebRefId { get; set; }
    public int PositionInVerse { get; set; }
    public string? PlainWord { get; set; }
}

public interface IRevisionOverviewRepository
{
    /// <summary>Every active variant (one row per verse and level) with its unlinked word counts.</summary>
    Task<IReadOnlyList<RevisionOverviewVariantRow>> GetActiveVariantsAsync();

    /// <summary>Elb1871 words of the given verses, ordered by verse and position (input of the base text hash).</summary>
    Task<IReadOnlyList<RevisionOverviewBaseWordRow>> GetBaseWordsAsync(int[] hebRefIds);

    Task<int> GetTotalVersesAsync();
}

public sealed class RevisionOverviewRepository : IRevisionOverviewRepository
{
    private readonly IDbReader _reader;

    public RevisionOverviewRepository(IDbReader reader)
    {
        _reader = reader;
    }

    public async Task<IReadOnlyList<RevisionOverviewVariantRow>> GetActiveVariantsAsync() =>
        (await _reader.ReadAsListAsync<RevisionOverviewVariantRow>(RevisionOverviewSql.ActiveVariants)).ToList();

    public async Task<IReadOnlyList<RevisionOverviewBaseWordRow>> GetBaseWordsAsync(int[] hebRefIds) =>
        (await _reader.ReadAsListAsync<RevisionOverviewBaseWordRow>(RevisionOverviewSql.BaseWords, new { HebRefIds = hebRefIds })).ToList();

    public async Task<int> GetTotalVersesAsync() =>
        await _reader.ExecuteScalarAsync<int?>(RevisionOverviewSql.TotalVerses) ?? 0;
}

/// <summary>SQL of <see cref="RevisionOverviewRepository"/>, public so tests can assert its shape.</summary>
public static class RevisionOverviewSql
{
    /// <summary>
    /// "Without a link" = no <c>RevisionMapping</c> row at all for a German word (an added-word row counts as linked) and no row
    /// pointing at an original word of the verse. Hebrew for books 1-39, Greek otherwise; both id ranges start at 1, so every
    /// mapping join carries <c>Lang</c>.
    /// </summary>
    public const string ActiveVariants = """
                                         SELECT rv."Id"                AS "RevisionVerseId"
                                              , rv."HebRefId"          AS "HebRefId"
                                              , rev."Level"            AS "Level"
                                              , rv."Version"           AS "Version"
                                              , rv."BaseTextHash"      AS "BaseTextHash"
                                              , rv."UpdatedAt"         AS "CreatedAt"
                                              , rv."MappingReviewedAt" AS "MappingReviewedAt"
                                              , (SELECT count(*)
                                                 FROM "unshackled-word"."RevisionWords" rw
                                                 WHERE rw."RevisionVerseId" = rv."Id"
                                                     AND NOT EXISTS (SELECT 1
                                                                     FROM "unshackled-word"."RevisionMapping" rm
                                                                     WHERE rm."RevisionWordId" = rw."Id")) AS "UnlinkedGermanWords"
                                              , CASE
                                                    WHEN rv."HebRefId" < 40000000 THEN (SELECT count(*)
                                                                                         FROM "unshackled-word"."StepHebrewWords" shw
                                                                                         WHERE shw."LxxRefId" = rv."HebRefId"
                                                                                             AND NOT EXISTS (SELECT 1
                                                                                                             FROM "unshackled-word"."RevisionMapping" rm
                                                                                                                 JOIN "unshackled-word"."RevisionWords" rw ON rw."Id" = rm."RevisionWordId"
                                                                                                             WHERE rw."RevisionVerseId" = rv."Id"
                                                                                                                 AND rm."Lang" = 'H'
                                                                                                                 AND rm."StepWordId" = shw."Id"))
                                                    ELSE (SELECT count(*)
                                                          FROM "unshackled-word"."StepGreekWords" sgw
                                                          WHERE sgw."LxxRefId" = rv."HebRefId"
                                                              AND NOT EXISTS (SELECT 1
                                                                              FROM "unshackled-word"."RevisionMapping" rm
                                                                                  JOIN "unshackled-word"."RevisionWords" rw ON rw."Id" = rm."RevisionWordId"
                                                                              WHERE rw."RevisionVerseId" = rv."Id"
                                                                                  AND rm."Lang" = 'G'
                                                                                  AND rm."StepWordId" = sgw."Id"))
                                                END AS "UnlinkedOriginalWords"
                                         FROM "unshackled-word"."RevisionVerses" rv
                                             JOIN "unshackled-word"."Revisions" rev ON rev."Id" = rv."RevisionId"
                                         ORDER BY rev."Level"
                                                , rv."HebRefId"
                                         """;

    public const string BaseWords = """
                                    SELECT ew."HebRefId"        AS "HebRefId"
                                         , ew."PositionInVerse" AS "PositionInVerse"
                                         , ew."PlainWord"       AS "PlainWord"
                                    FROM "unshackled-word"."Elb1871Words" ew
                                    WHERE ew."HebRefId" = ANY (@HebRefIds)
                                    ORDER BY ew."HebRefId"
                                           , ew."PositionInVerse"
                                           , ew."Id"
                                    """;

    public const string TotalVerses = """
                                      SELECT count(*)
                                      FROM "unshackled-word"."Elb1871Verses" v
                                      """;
}
