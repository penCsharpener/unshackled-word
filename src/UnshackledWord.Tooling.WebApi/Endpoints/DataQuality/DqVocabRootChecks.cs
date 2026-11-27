using System.Data;
using Dapper;
using UnshackledWord.Domain.Models.DataQuality;
using UnshackledWord.Domain.WebApi.DataQuality;

namespace UnshackledWord.Tooling.WebApi.Endpoints.DataQuality;

/// <summary>
/// Vocab root checks (roadmap 0030.7), computed live from <c>VocabRoots</c>/<c>VocabStrongsRoots</c>/<c>VocabStrongs</c>; not part of <c>DqWordFacts</c>.
/// V1: a vocab entry whose Strong's keys lead to different roots (the list shows the root of the main key and the one that differs), warning.
/// V2: a root whose Strong's key is missing in <c>StepStrongsLexicon</c>, error. V3: a root whose own key's word row points at another root, warning.
/// The testament/book facets do not apply (no verse reference).
/// </summary>
internal static class DqVocabRootChecks
{
    public const string KeysDisagreeCheck = "V1";
    public const string MissingKeyCheck = "V2";
    public const string OwnKeyElsewhereCheck = "V3";

    public static readonly string[] Codes = [KeysDisagreeCheck, MissingKeyCheck, OwnKeyElsewhereCheck];

    public static bool IsVocabRootCheck(string code) => Codes.Contains(code);

    public sealed class Row
    {
        public string Lang { get; init; } = string.Empty;
        public string Lemma { get; init; } = string.Empty;
        public string Key { get; init; } = string.Empty;
        public string Detail { get; init; } = string.Empty;
    }

    private const string Keys = """
        SELECT e."Track" AS "Lang"
             , e."Lemma"
             , (array_agg(vs."StrongsKey" ORDER BY vs."Occurrences" DESC, vs."StrongsKey"))[1] AS "Key"
             , string_agg(vs."StrongsKey" || ' → ' || r."Root", '; ' ORDER BY vs."Occurrences" DESC, vs."StrongsKey") AS "Detail"
        FROM "unshackled-word"."VocabEntries" e
            JOIN "unshackled-word"."VocabStrongs" vs ON vs."VocabEntryId" = e."Id"
            JOIN "unshackled-word"."VocabStrongsRoots" sr ON sr."Lang" = vs."Lang" AND sr."StrongsKey" = vs."StrongsKey"
            JOIN "unshackled-word"."VocabRoots" r ON r."Id" = sr."VocabRootId"
        WHERE 1=1
            AND e."RetiredAt" IS NULL
        GROUP BY e."Id", e."Track", e."Lemma"
        HAVING count(DISTINCT sr."VocabRootId") > 1
        """;

    private const string Missing = """
        SELECT r."Track" AS "Lang"
             , r."Root" AS "Lemma"
             , r."StrongsKey" AS "Key"
             , 'Wurzel „' || r."Root" || '“ mit Schlüssel ' || r."StrongsKey" || ' fehlt im Strong''s-Lexikon' AS "Detail"
        FROM "unshackled-word"."VocabRoots" r
        WHERE 1=1
            AND r."StrongsKey" IS NOT NULL
            AND NOT EXISTS (
                SELECT 1
                FROM "unshackled-word"."StepStrongsLexicon" l
                WHERE 1=1
                    AND (l."LanguageId" = 2) = (r."Track" = 'G')
                    AND l."Number" = NULLIF(regexp_replace(r."StrongsKey", '\D', '', 'g'), '')::int
            )
        """;

    private const string Elsewhere = """
        SELECT r."Track" AS "Lang"
             , r."Root" AS "Lemma"
             , r."StrongsKey" AS "Key"
             , 'Der Eintrag von ' || r."StrongsKey" || ' („' || r."Root" || '“) gehört laut Zeile zur Wurzel „' || o."Root" || '“' AS "Detail"
        FROM "unshackled-word"."VocabRoots" r
            JOIN "unshackled-word"."VocabStrongsRoots" sr ON sr."Lang" = r."Track" AND sr."StrongsKey" = r."StrongsKey"
            JOIN "unshackled-word"."VocabRoots" o ON o."Id" = sr."VocabRootId"
        WHERE 1=1
            AND sr."VocabRootId" <> r."Id"
        """;

    private static string Sql(string code) => code switch
    {
        KeysDisagreeCheck => Keys,
        MissingKeyCheck => Missing,
        OwnKeyElsewhereCheck => Elsewhere,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Not a vocab root check.")
    };

    public static async Task<CheckCount> CountAsync(IDbConnection connection, IDbTransaction? transaction, string code, DqFacets facets, CancellationToken ct)
    {
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT count(*)::int4 FROM ({Sql(code)}) x", transaction: transaction, cancellationToken: ct));

        return new CheckCount(count, code == MissingKeyCheck ? count : 0, null);
    }

    public static async Task<(int Total, List<Finding> Items)> FindingsAsync(
        IDbConnection connection, IDbTransaction? transaction, string code, DqFacets facets, int page, int pageSize, CancellationToken ct)
    {
        var total = (await CountAsync(connection, transaction, code, facets, ct)).Count;
        var rows = await connection.QueryAsync<Row>(new CommandDefinition(
            $"""
            SELECT * FROM ({Sql(code)}) x
            ORDER BY x."Lang", x."Lemma" COLLATE "C", x."Key" COLLATE "C"
            LIMIT @pageSize OFFSET @offset
            """, new { pageSize, offset = (page - 1) * pageSize }, transaction, cancellationToken: ct));

        return (total, [.. rows.Select(r => ToFinding(code, r))]);
    }

    public static Finding ToFinding(string code, Row r) => new()
    {
        Check = code,
        Severity = code == MissingKeyCheck ? UnmappedRules.Error : UnmappedRules.Warning,
        Lang = r.Lang,
        Original = r.Lemma,
        Strongs = r.Key,
        Detail = r.Detail,
    };
}
