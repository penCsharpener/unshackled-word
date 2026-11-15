using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Revision;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Infrastructure.Repositories;

/// <summary>
/// The tsquery arrives as text built by <c>VerseSearchQueryBuilder</c> (every lexeme single-quoted and escaped) and is cast with
/// <c>CAST(@Query AS tsquery)</c>; user text is never concatenated into the SQL and never goes through to_tsquery with its own operators.
/// </summary>
public sealed class VerseSearchRepository : IVerseSearchRepository
{
    private readonly IDbReader _dbReader;

    public VerseSearchRepository(IDbReader dbReader)
    {
        _dbReader = dbReader;
    }

    public async Task<IReadOnlyList<VerseSearchLexemeRow>> GetLexemesAsync(IReadOnlyList<string> words, CancellationToken token = default)
    {
        const string sql = """
                           SELECT w."Word"
                                , (ts_lexize('"unshackled-word".uw_german_stem'::regdictionary, w."Word"))[1] "Lexeme"
                           FROM unnest(@Words::text[]) AS w("Word")
                           """;

        var rows = await _dbReader.ReadAsListAsync<VerseSearchLexemeRow>(sql, new { Words = words.ToArray() });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<VerseSearchBookCountRow>> GetBookCountsAsync(string tsQuery, int level, CancellationToken token = default)
    {
        if (level != IVerseSearchRepository.BaseLevel)
        {
            const string revisedSql = $"""
                                       WITH "Effective" AS ({RevisionSql.EffectiveVerses})
                                       SELECT (v."HebRefId" / 1000000)::int "BookId"
                                            , COUNT(*)::int "Count"
                                       FROM "unshackled-word"."Elb1871Verses" v
                                           JOIN "Effective" e ON e."HebRefId" = v."HebRefId"
                                           LEFT JOIN "unshackled-word"."RevisionVerses" rv ON rv."Id" = e."RevisionVerseId"
                                       WHERE 1=1
                                           AND (v."SearchVector" @@ CAST(@Query AS tsquery) OR rv."SearchVector" @@ CAST(@Query AS tsquery))
                                       GROUP BY v."HebRefId" / 1000000
                                       ORDER BY 1
                                       """;

            var revisedRows = await _dbReader.ReadAsListAsync<VerseSearchBookCountRow>(revisedSql, new { Query = tsQuery, Level = level, FromRefId = 0, ToRefId = int.MaxValue });
            return revisedRows.ToList();
        }

        const string sql = """
                           SELECT (v."HebRefId" / 1000000)::int "BookId"
                                , COUNT(*)::int "Count"
                           FROM "unshackled-word"."Elb1871Verses" v
                           WHERE 1=1
                               AND v."SearchVector" @@ CAST(@Query AS tsquery)
                           GROUP BY v."HebRefId" / 1000000
                           ORDER BY 1
                           """;

        var rows = await _dbReader.ReadAsListAsync<VerseSearchBookCountRow>(sql, new { Query = tsQuery });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<VerseSearchHitRow>> GetHitsAsync(string tsQuery, string? literalTsQuery, int level, int firstRefId, int lastRefId, int offset, int limit, CancellationToken token = default)
    {
        if (level != IVerseSearchRepository.BaseLevel)
        {
            return await GetRevisedHitsAsync(tsQuery, literalTsQuery, level, firstRefId, lastRefId, offset, limit);
        }

        // The literal sort key is only needed while the query was widened by lemmas; without it the filter expression would be evaluated twice per row.
        const string literalFirst = """
                                    ORDER BY (v."SearchVector" @@ CAST(@Literal AS tsquery)) DESC
                                           , ts_rank_cd(v."SearchVector", CAST(@Query AS tsquery)) DESC
                                           , v."HebRefId"
                                    """;
        const string rankOnly = """
                                ORDER BY ts_rank_cd(v."SearchVector", CAST(@Query AS tsquery)) DESC
                                       , v."HebRefId"
                                """;
        const string head = """
                            SELECT v."HebRefId"
                            FROM "unshackled-word"."Elb1871Verses" v
                            WHERE 1=1
                                AND v."SearchVector" @@ CAST(@Query AS tsquery)
                                AND v."HebRefId" BETWEEN @FirstRefId AND @LastRefId
                            """;
        const string tail = "LIMIT @Limit OFFSET @Offset";

        var sql = head + "\n" + (literalTsQuery is null ? rankOnly : literalFirst) + "\n" + tail;
        var rows = await _dbReader.ReadAsListAsync<VerseSearchHitRow>(sql, new
        {
            Query = tsQuery,
            Literal = literalTsQuery,
            FirstRefId = firstRefId,
            LastRefId = lastRefId,
            Limit = limit,
            Offset = offset
        });
        return rows.ToList();
    }

    /// <summary>
    /// A verse is a hit when the Elb1871 text or the variant the level shows matches. One row per verse: the variant is shown when it matches,
    /// otherwise the Elb1871 text (a search for a word that only the base text has still finds the verse, unflagged). The variant's vector has no lemmas, so
    /// lemma widening only ever matches the base text.
    /// </summary>
    private async Task<IReadOnlyList<VerseSearchHitRow>> GetRevisedHitsAsync(string tsQuery, string? literalTsQuery, int level, int firstRefId, int lastRefId, int offset, int limit)
    {
        const string literalFirst = """
                                    ORDER BY (c."Vector" @@ CAST(@Literal AS tsquery)) DESC
                                           , ts_rank_cd(c."Vector", CAST(@Query AS tsquery)) DESC
                                           , c."HebRefId"
                                    """;
        const string rankOnly = """
                                ORDER BY ts_rank_cd(c."Vector", CAST(@Query AS tsquery)) DESC
                                       , c."HebRefId"
                                """;
        const string head = $"""
                             WITH "Effective" AS ({RevisionSql.EffectiveVerses})
                                , "Candidates" AS (SELECT v."HebRefId"
                                                        , CASE WHEN rv."SearchVector" @@ CAST(@Query AS tsquery) THEN rv."Id" END "RevisionVerseId"
                                                        , CASE WHEN rv."SearchVector" @@ CAST(@Query AS tsquery) THEN e."Level" END "Level"
                                                        , CASE WHEN rv."SearchVector" @@ CAST(@Query AS tsquery) THEN rv."SearchVector" ELSE v."SearchVector" END "Vector"
                                                   FROM "unshackled-word"."Elb1871Verses" v
                                                       JOIN "Effective" e ON e."HebRefId" = v."HebRefId"
                                                       LEFT JOIN "unshackled-word"."RevisionVerses" rv ON rv."Id" = e."RevisionVerseId"
                                                   WHERE 1=1
                                                       AND (v."SearchVector" @@ CAST(@Query AS tsquery) OR rv."SearchVector" @@ CAST(@Query AS tsquery)))
                             SELECT c."HebRefId"
                                  , c."RevisionVerseId"
                                  , c."Level"
                             FROM "Candidates" c
                             """;
        const string tail = "LIMIT @Limit OFFSET @Offset";

        var sql = head + "\n" + (literalTsQuery is null ? rankOnly : literalFirst) + "\n" + tail;
        var rows = await _dbReader.ReadAsListAsync<VerseSearchHitRow>(sql, new
        {
            Query = tsQuery,
            Literal = literalTsQuery,
            Level = level,
            FromRefId = firstRefId,
            ToRefId = lastRefId,
            Limit = limit,
            Offset = offset
        });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<VerseSearchWordRow>> GetRevisionWordsAsync(IReadOnlyList<int> revisionVerseIds, CancellationToken token = default)
    {
        const string sql = """
                           SELECT rv."HebRefId"
                                , w."PositionInVerse"
                                , w."WordInContext"
                                , tsvector_to_array(to_tsvector('"unshackled-word".uw_german'::regconfig, w."PlainWord")) "Stems"
                                , NULL::text "Lemma"
                           FROM "unshackled-word"."RevisionWords" w
                               JOIN "unshackled-word"."RevisionVerses" rv ON rv."Id" = w."RevisionVerseId"
                           WHERE 1=1
                               AND w."RevisionVerseId" = ANY(@RevisionVerseIds)
                           ORDER BY rv."HebRefId", w."PositionInVerse"
                           """;

        var rows = await _dbReader.ReadAsListAsync<VerseSearchWordRow>(sql, new { RevisionVerseIds = revisionVerseIds.ToArray() });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<VerseSearchWordRow>> GetWordsAsync(IReadOnlyList<int> hebRefIds, CancellationToken token = default)
    {
        const string sql = """
                           SELECT w."HebRefId"
                                , w."PositionInVerse"
                                , w."WordInContext"
                                , tsvector_to_array(to_tsvector('"unshackled-word".uw_german'::regconfig, w."PlainWord")) "Stems"
                                , lower(m."Lemma") "Lemma"
                           FROM "unshackled-word"."Elb1871Words" w
                               LEFT JOIN "unshackled-word"."Elb1871Morphology" m ON m."HebRefId" = w."HebRefId" AND m."PositionInVerse" = w."PositionInVerse"
                           WHERE 1=1
                               AND w."HebRefId" = ANY(@HebRefIds)
                           ORDER BY w."HebRefId", w."PositionInVerse"
                           """;

        var rows = await _dbReader.ReadAsListAsync<VerseSearchWordRow>(sql, new { HebRefIds = hebRefIds.ToArray() });
        return rows.ToList();
    }
}
