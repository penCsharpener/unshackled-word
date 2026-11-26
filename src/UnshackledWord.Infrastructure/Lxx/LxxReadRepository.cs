using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Lxx;

namespace UnshackledWord.Infrastructure.Lxx;

/// <summary>
/// Dapper reads of <c>LxxVerses</c>/<c>LxxWords</c>/<c>LxxWordTags</c> for the reader (roadmap 0028.4, tags 0028.5): the chapter as three result sets in
/// one round trip, tags and their TBESG gloss joined into the words query. Tags of every status are returned; the read services filter by role.
/// </summary>
public sealed class LxxReadRepository : ILxxReadRepository
{
    private readonly IDbConnectionFactory _factory;

    public LxxReadRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<LxxChapterRows> GetByHebRangeAsync(int fromHebRefId, int toHebRefId, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(LxxReadSql.Chapter, new { FromHebRefId = fromHebRefId, ToHebRefId = toHebRefId }, cancellationToken: token));
        return new LxxChapterRows
        {
            ElbHebRefIds = (await grid.ReadAsync<int>()).ToList(),
            Units = (await grid.ReadAsync<LxxUnitRow>()).ToList(),
            Words = (await grid.ReadAsync<LxxWordRow>()).ToList(),
        };
    }

    public async Task<LxxWordDetailRow?> GetWordAsync(int lxxWordId, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        return await connection.QuerySingleOrDefaultAsync<LxxWordDetailRow>(new CommandDefinition(LxxReadSql.Word, new { LxxWordId = lxxWordId }, cancellationToken: token));
    }
}

/// <summary>The SQL of <see cref="LxxReadRepository"/>, public so the database tests can run it inside a rolled-back transaction.</summary>
public static class LxxReadSql
{
    /// <summary>Three result sets: Elb1871 verse ids, LXX units, words with tag (any status) and TBESG gloss. Parameters FromHebRefId, ToHebRefId.</summary>
    public const string Chapter = """
                                  SELECT v."HebRefId"
                                  FROM "unshackled-word"."Elb1871Verses" v
                                  WHERE 1=1
                                      AND v."HebRefId" BETWEEN @FromHebRefId AND @ToHebRefId
                                  ORDER BY v."HebRefId";

                                  SELECT l."Id"
                                       , l."BibleBookId"
                                       , l."TextForm"
                                       , l."Chapter"
                                       , l."Verse"
                                       , l."Subverse"
                                       , l."SortOrder"
                                       , l."HebRefId"
                                       , l."AfterHebRefId"
                                       , l."MapKind"
                                  FROM "unshackled-word"."LxxVerses" l
                                  WHERE 1=1
                                      AND (l."HebRefId" BETWEEN @FromHebRefId AND @ToHebRefId
                                           OR l."AfterHebRefId" BETWEEN @FromHebRefId AND @ToHebRefId)
                                  ORDER BY l."SortOrder";

                                  SELECT w."Id"
                                       , w."LxxVerseId"
                                       , w."Position"
                                       , w."Greek"
                                       , t."Lemma"
                                       , t."Strongs"
                                       , t."Morphology"
                                       , t."Status"
                                       , t."Source"
                                       , g."Gloss"
                                  FROM "unshackled-word"."LxxVerses" l
                                  JOIN "unshackled-word"."LxxWords" w ON w."LxxVerseId" = l."Id"
                                  LEFT JOIN "unshackled-word"."LxxWordTags" t ON t."LxxWordId" = w."Id"
                                  LEFT JOIN LATERAL (
                                      SELECT ssl."Gloss"
                                      FROM "unshackled-word"."StepStrongsLexicon" ssl
                                      WHERE 1=1
                                          AND ssl."LanguageId" = 2
                                          AND ssl."Number" = substring(t."Strongs" FROM 2 FOR 4)::int4
                                      ORDER BY ssl."Extra" IS NOT DISTINCT FROM NULLIF(substring(t."Strongs" FROM 6), '') DESC
                                             , COALESCE(ssl."Extra", '') = '' DESC
                                             , ssl."Extra"
                                      LIMIT 1
                                  ) g ON TRUE
                                  WHERE 1=1
                                      AND (l."HebRefId" BETWEEN @FromHebRefId AND @ToHebRefId
                                           OR l."AfterHebRefId" BETWEEN @FromHebRefId AND @ToHebRefId)
                                  ORDER BY l."SortOrder", w."Position"
                                  """;

    /// <summary>One word with its unit and tag (any status). Parameter LxxWordId.</summary>
    public const string Word = """
                               SELECT w."Id"
                                    , w."LxxVerseId"
                                    , w."Position"
                                    , w."Greek"
                                    , t."Lemma"
                                    , t."Strongs"
                                    , t."Morphology"
                                    , t."Status"
                                    , t."Source"
                                    , l."BibleBookId"
                                    , l."TextForm"
                                    , l."Chapter"
                                    , l."Verse"
                                    , l."Subverse"
                                    , l."HebRefId"
                                    , l."AfterHebRefId"
                                    , l."MapKind"
                               FROM "unshackled-word"."LxxWords" w
                               JOIN "unshackled-word"."LxxVerses" l ON l."Id" = w."LxxVerseId"
                               LEFT JOIN "unshackled-word"."LxxWordTags" t ON t."LxxWordId" = w."Id"
                               WHERE 1=1
                                   AND w."Id" = @LxxWordId
                               """;
}
