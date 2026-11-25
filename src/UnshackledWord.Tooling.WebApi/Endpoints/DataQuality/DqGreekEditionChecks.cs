using System.Data;
using Dapper;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.Models.DataQuality;
using UnshackledWord.Domain.WebApi.DataQuality;

namespace UnshackledWord.Tooling.WebApi.Endpoints.DataQuality;

/// <summary>
/// Greek edition checks (roadmap 0025.3), computed live from <c>GreekEditions</c>/<c>StepGreekWordEditions</c>; not part of <c>DqWordFacts</c>.
/// G1: a catalog row with <c>Kind = 'unknown'</c> (token of <c>StepGreekWords."Editions"</c> that <c>RefreshStepGreekWordEditions</c> did not find in the
/// catalog): one finding per code with the number of words and the first three references, warning. G2: a word whose non-empty <c>Editions</c> string
/// produced no row in <c>StepGreekWordEditions</c> (no usable token), one finding per word, error. The testament/book facets do not apply (the data is
/// Greek NT only and tiny).
/// </summary>
internal static class DqGreekEditionChecks
{
    public const string UnknownCheck = "G1";
    public const string NoEditionCheck = "G2";

    public static readonly string[] Codes = [UnknownCheck, NoEditionCheck];

    public static bool IsGreekEditionCheck(string code) => Codes.Contains(code);

    public sealed class UnknownRow
    {
        public string Code { get; init; } = string.Empty;
        public int WordCount { get; init; }
        public int[] FirstRefs { get; init; } = [];
    }

    public sealed class NoEditionRow
    {
        public int StepWordId { get; init; }
        public int LxxRefId { get; init; }
        public string Greek { get; init; } = string.Empty;
        public string Gloss { get; init; } = string.Empty;
        public string Editions { get; init; } = string.Empty;
    }

    private const string NoEditionWhere = """
        WHERE 1=1
            AND btrim(w."Editions") <> ''
            AND NOT EXISTS (
                SELECT 1
                FROM "unshackled-word"."StepGreekWordEditions" e
                WHERE e."StepWordId" = w."Id"
            )
        """;

    private static async Task<List<UnknownRow>> LoadUnknownAsync(IDbConnection connection, IDbTransaction? transaction, CancellationToken ct)
    {
        const string sql = """
            SELECT ge."Code"
                 , count(e."StepWordId")::int4 AS "WordCount"
                 , COALESCE(
                       (
                           SELECT array_agg(f."LxxRefId" ORDER BY f."LxxRefId", f."PositionInVerse")
                           FROM (
                               SELECT DISTINCT ON (w."LxxRefId") w."LxxRefId", w."PositionInVerse"
                               FROM "unshackled-word"."StepGreekWordEditions" e2
                                   JOIN "unshackled-word"."StepGreekWords" w ON w."Id" = e2."StepWordId"
                               WHERE 1=1
                                   AND e2."EditionId" = ge."Id"
                               ORDER BY w."LxxRefId", w."PositionInVerse"
                               LIMIT 3
                           ) f
                       ), ARRAY[]::int4[]) AS "FirstRefs"
            FROM "unshackled-word"."GreekEditions" ge
                LEFT JOIN "unshackled-word"."StepGreekWordEditions" e ON e."EditionId" = ge."Id"
            WHERE 1=1
                AND ge."Kind" = 'unknown'
            GROUP BY ge."Id", ge."Code"
            ORDER BY ge."Code"
            """;
        return [.. await connection.QueryAsync<UnknownRow>(new CommandDefinition(sql, transaction: transaction, cancellationToken: ct))];
    }

    public static async Task<CheckCount> CountAsync(IDbConnection connection, IDbTransaction? transaction, string code, DqFacets facets, CancellationToken ct)
    {
        if (code == UnknownCheck)
        {
            var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                """
                SELECT count(*)::int4
                FROM "unshackled-word"."GreekEditions" ge
                WHERE 1=1
                    AND ge."Kind" = 'unknown'
                """, transaction: transaction, cancellationToken: ct));
            return new CheckCount(count, 0, null);
        }

        var words = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"""
            SELECT count(*)::int4
            FROM "unshackled-word"."StepGreekWords" w
            {NoEditionWhere}
            """, transaction: transaction, cancellationToken: ct));
        return new CheckCount(words, words, null);
    }

    public static async Task<(int Total, List<Finding> Items)> FindingsAsync(
        IDbConnection connection, IDbTransaction? transaction, string code, DqFacets facets, int page, int pageSize, CancellationToken ct)
    {
        if (code == UnknownCheck)
        {
            var rows = await LoadUnknownAsync(connection, transaction, ct);
            return (rows.Count, [.. rows.Skip((page - 1) * pageSize).Take(pageSize).Select(ToFinding)]);
        }

        var total = (await CountAsync(connection, transaction, code, facets, ct)).Count;
        var sql = $"""
            SELECT w."Id"          AS "StepWordId"
                 , w."LxxRefId"
                 , w."Greek"
                 , w."Gloss"
                 , w."Editions"
            FROM "unshackled-word"."StepGreekWords" w
            {NoEditionWhere}
            ORDER BY w."LxxRefId", w."PositionInVerse", w."Id"
            LIMIT @pageSize OFFSET @offset
            """;
        var words = await connection.QueryAsync<NoEditionRow>(new CommandDefinition(
            sql, new { pageSize, offset = (page - 1) * pageSize }, transaction, cancellationToken: ct));
        return (total, [.. words.Select(ToFinding)]);
    }

    public static Finding ToFinding(UnknownRow r)
    {
        var first = r.FirstRefs.Length > 0 ? r.FirstRefs[0] : 0;
        var (book, chapter, verse) = Split(first);
        var refs = string.Join("; ", r.FirstRefs.Select(id => GermanReferenceFormatter.Format(id)));
        return new Finding
        {
            Check = UnknownCheck,
            Severity = UnmappedRules.Warning,
            BibleBookId = book,
            Chapter = chapter,
            Verse = verse,
            Lang = "G",
            German = r.Code,
            Detail = r.WordCount == 0
                ? $"Unbekannter Textausgaben-Code „{r.Code}“ ohne Wörter"
                : $"Unbekannter Textausgaben-Code „{r.Code}“: {r.WordCount} Wörter, zuerst {refs}",
        };
    }

    public static Finding ToFinding(NoEditionRow r)
    {
        var (book, chapter, verse) = Split(r.LxxRefId);
        return new Finding
        {
            Check = NoEditionCheck,
            Severity = UnmappedRules.Error,
            BibleBookId = book,
            Chapter = chapter,
            Verse = verse,
            StepWordId = r.StepWordId,
            Lang = "G",
            Original = r.Greek,
            Gloss = r.Gloss,
            Detail = $"Textausgaben „{r.Editions}“ ergeben keinen Eintrag",
        };
    }

    private static (int Book, int Chapter, int Verse) Split(int refId) => (refId / 1_000_000, refId / 1_000 % 1_000, refId % 1_000);
}
