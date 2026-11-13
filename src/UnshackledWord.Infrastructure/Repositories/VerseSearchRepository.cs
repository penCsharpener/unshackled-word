using UnshackledWord.Application.Abstractions;
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

    public async Task<IReadOnlyList<VerseSearchBookCountRow>> GetBookCountsAsync(string tsQuery, CancellationToken token = default)
    {
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

    public async Task<IReadOnlyList<VerseSearchHitRow>> GetHitsAsync(string tsQuery, string? literalTsQuery, int firstRefId, int lastRefId, int offset, int limit, CancellationToken token = default)
    {
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
