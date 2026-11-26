using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Lxx;

namespace UnshackledWord.Infrastructure.Lxx;

/// <summary>
/// Dapper reads for the LXX occurrences of a Strong's number (roadmap 0028.9). Counting uses <c>LxxWordTags ("Strongs", "Status")</c> only; a page
/// is three small queries on one connection (tag rows with their verse and Elb1871 text, the words of those verses).
/// </summary>
public sealed class LxxStrongsOccurrenceRepository : ILxxStrongsOccurrenceRepository
{
    private readonly IDbConnectionFactory _factory;

    public LxxStrongsOccurrenceRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<LxxStrongsCounts> CountAsync(IReadOnlyList<string> keys, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        return await connection.QuerySingleAsync<LxxStrongsCounts>(new CommandDefinition(LxxStrongsOccurrenceSql.Count, new { Keys = keys.ToArray() }, cancellationToken: token));
    }

    public async Task<IReadOnlyList<LxxStrongsOccurrenceRow>> GetConfirmedPageAsync(IReadOnlyList<string> keys, int skip, int take, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        var page = (await connection.QueryAsync<LxxStrongsOccurrenceRow>(new CommandDefinition(LxxStrongsOccurrenceSql.Page,
            new { Keys = keys.ToArray(), Skip = skip, Take = take }, cancellationToken: token))).ToList();
        if (page.Count == 0)
        {
            return page;
        }

        var verseIds = page.Select(r => r.LxxVerseId).Distinct().ToArray();
        var words = (await connection.QueryAsync<(int LxxVerseId, int Position, string Greek)>(new CommandDefinition(LxxStrongsOccurrenceSql.Words,
            new { VerseIds = verseIds }, cancellationToken: token))).ToLookup(w => w.LxxVerseId);
        foreach (var row in page)
        {
            row.Words.AddRange(words[row.LxxVerseId].Select(w => new LxxStrongsVerseWord(w.Position, w.Greek)));
        }

        return page;
    }
}

/// <summary>The SQL of <see cref="LxxStrongsOccurrenceRepository"/>, public so the database tests can run it inside a rolled-back transaction.</summary>
public static class LxxStrongsOccurrenceSql
{
    /// <summary>Parameter Keys. Columns Confirmed, Proposed (positional record constructor).</summary>
    public const string Count = """
                                SELECT (count(*) FILTER (WHERE t."Status" = 'confirmed'))::int AS "Confirmed"
                                     , (count(*) FILTER (WHERE t."Status" = 'proposed'))::int  AS "Proposed"
                                FROM "unshackled-word"."LxxWordTags" t
                                WHERE 1=1
                                    AND t."Strongs" = ANY(@Keys)
                                """;

    /// <summary>Parameters Keys, Skip, Take. Confirmed tags only; the Elb verse is the mapped one, else the anchor of an addition.</summary>
    public const string Page = """
                               SELECT w."Id"       AS "LxxWordId"
                                    , l."Id"       AS "LxxVerseId"
                                    , w."Position"
                                    , l."BibleBookId"
                                    , l."TextForm"
                                    , l."Chapter"
                                    , l."Verse"
                                    , l."Subverse"
                                    , COALESCE(l."HebRefId", l."AfterHebRefId") AS "ElbHebRefId"
                                    , l."HebRefId" IS NULL                      AS "ElbIsAnchor"
                                    , ev."VerseText" AS "ElbText"
                               FROM "unshackled-word"."LxxWordTags" t
                               JOIN "unshackled-word"."LxxWords" w ON w."Id" = t."LxxWordId"
                               JOIN "unshackled-word"."LxxVerses" l ON l."Id" = w."LxxVerseId"
                               LEFT JOIN "unshackled-word"."Elb1871Verses" ev ON ev."HebRefId" = COALESCE(l."HebRefId", l."AfterHebRefId")
                               WHERE 1=1
                                   AND t."Strongs" = ANY(@Keys)
                                   AND t."Status" = 'confirmed'
                               ORDER BY l."SortOrder", w."Position"
                               OFFSET @Skip LIMIT @Take
                               """;

    /// <summary>Parameter VerseIds.</summary>
    public const string Words = """
                                SELECT w."LxxVerseId"
                                     , w."Position"
                                     , w."Greek"
                                FROM "unshackled-word"."LxxWords" w
                                WHERE 1=1
                                    AND w."LxxVerseId" = ANY(@VerseIds)
                                ORDER BY w."LxxVerseId", w."Position"
                                """;
}
