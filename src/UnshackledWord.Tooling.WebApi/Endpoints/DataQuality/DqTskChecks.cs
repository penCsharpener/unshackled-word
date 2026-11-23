using System.Data;
using Dapper;
using UnshackledWord.Application.Features.CrossReferences;
using UnshackledWord.Domain.Models.DataQuality;
using UnshackledWord.Domain.WebApi.DataQuality;

namespace UnshackledWord.Tooling.WebApi.Endpoints.DataQuality;

/// <summary>
/// TSK curation checks (roadmap 0024 follow-up, 07-risks.md), computed live: <c>TskCurations</c> is small and not part of <c>DqWordFacts</c>.
/// T1: a curation whose key points nowhere (<c>Origin = 'tsk'</c> without a <c>Tsk</c> row, e.g. after a counting-map change; <c>'added'</c> whose source
/// verse is gone from <c>Elb1871Verses</c>), error. T2: a word link whose Elb1871 words at the stored positions no longer match the quote
/// (<see cref="CrossReferenceWordLink.IsDrifted"/>, the rule of the read side) or whose positions are gone, warning.
/// Facets apply to the source verse's book.
/// </summary>
internal static class DqTskChecks
{
    public const string OrphanCheck = "T1";
    public const string DriftCheck = "T2";

    public static readonly string[] Codes = [OrphanCheck, DriftCheck];

    public static bool IsTskCheck(string code) => Codes.Contains(code);

    public sealed class Row
    {
        public int Id { get; init; }
        public string Origin { get; init; } = "tsk";
        public int SourceHebRefId { get; init; }
        public int TargetStartHebRefId { get; init; }
        public int? TargetEndHebRefId { get; init; }
        public int? StartPosition { get; init; }
        public int? EndPosition { get; init; }
        public string? QuotedText { get; init; }
        public bool NoTskRow { get; init; }
        public bool NoSourceVerse { get; init; }
        public string? CurrentQuote { get; init; }
        public int CurrentWordCount { get; init; }

        public bool IsOrphan => NoTskRow || NoSourceVerse;

        public bool IsDrifted => StartPosition is { } from && EndPosition is { } to && QuotedText is not null
            && CrossReferenceWordLink.IsDrifted(from, to, QuotedText, CurrentWordCount, CurrentQuote);
    }

    private static string FacetPredicate(DqFacets facets) => string.Concat(
        facets.Testament == "ot" ? " AND c.\"SourceHebRefId\" < 40000000" : facets.Testament == "nt" ? " AND c.\"SourceHebRefId\" >= 40000000" : string.Empty,
        facets.BookId is { } id ? $" AND c.\"SourceHebRefId\" / 1000000 = {id}" : string.Empty);

    private static async Task<List<Row>> LoadAsync(IDbConnection connection, IDbTransaction? transaction, DqFacets facets, CancellationToken ct)
    {
        var sql = $"""
            SELECT c."Id"
                 , c."Origin"
                 , c."SourceHebRefId"
                 , c."TargetStartHebRefId"
                 , c."TargetEndHebRefId"
                 , c."StartPosition"
                 , c."EndPosition"
                 , c."QuotedText"
                 , (c."Origin" = 'tsk' AND NOT EXISTS (
                        SELECT 1
                        FROM "unshackled-word"."Tsk" t
                        WHERE 1=1
                            AND t."SourceHebRefId" = c."SourceHebRefId"
                            AND t."TargetStartHebRefId" = c."TargetStartHebRefId"
                            AND t."TargetEndHebRefId" IS NOT DISTINCT FROM c."TargetEndHebRefId"
                    )) AS "NoTskRow"
                 , (c."Origin" = 'added' AND NOT EXISTS (
                        SELECT 1
                        FROM "unshackled-word"."Elb1871Verses" v
                        WHERE v."HebRefId" = c."SourceHebRefId"
                    )) AS "NoSourceVerse"
                 , cw."CurrentQuote"
                 , COALESCE(cw."CurrentWordCount", 0) AS "CurrentWordCount"
            FROM "unshackled-word"."TskCurations" c
            LEFT JOIN LATERAL (
                SELECT string_agg(w."WordInContext", ' ' ORDER BY w."PositionInVerse") AS "CurrentQuote"
                     , count(*)::int4 AS "CurrentWordCount"
                FROM "unshackled-word"."Elb1871Words" w
                WHERE 1=1
                    AND w."HebRefId" = c."SourceHebRefId"
                    AND w."PositionInVerse" BETWEEN c."StartPosition" AND c."EndPosition"
            ) cw ON c."StartPosition" IS NOT NULL
            WHERE 1=1{FacetPredicate(facets)}
            ORDER BY c."SourceHebRefId", c."TargetStartHebRefId", c."Id"
            """;
        return [.. await connection.QueryAsync<Row>(new CommandDefinition(sql, transaction: transaction, cancellationToken: ct))];
    }

    private static IEnumerable<Row> Select(string code, IEnumerable<Row> rows) =>
        code == OrphanCheck ? rows.Where(r => r.IsOrphan) : rows.Where(r => r.IsDrifted);

    public static async Task<CheckCount> CountAsync(IDbConnection connection, IDbTransaction? transaction, string code, DqFacets facets, CancellationToken ct)
    {
        var rows = await LoadAsync(connection, transaction, facets, ct);
        var hits = Select(code, rows).ToList();
        return code == OrphanCheck
            ? new CheckCount(hits.Count, hits.Count, new KeyCount { Key = "added", Count = hits.Count(r => r.NoSourceVerse) })
            : new CheckCount(hits.Count, 0, null);
    }

    public static async Task<(int Total, List<Finding> Items)> FindingsAsync(
        IDbConnection connection, IDbTransaction? transaction, string code, DqFacets facets, int page, int pageSize, CancellationToken ct)
    {
        var hits = Select(code, await LoadAsync(connection, transaction, facets, ct)).ToList();
        return (hits.Count, [.. hits.Skip((page - 1) * pageSize).Take(pageSize).Select(r => ToFinding(code, r))]);
    }

    private static string Range(int from, int to) => from == to ? from.ToString() : $"{from}–{to}";

    public static Finding ToFinding(string code, Row r)
    {
        var (book, chapter, verse) = Split(r.SourceHebRefId);
        var (targetBook, targetChapter, targetVerse) = Split(r.TargetStartHebRefId);
        var detail = code == OrphanCheck
            ? r.NoSourceVerse
                ? "Hinzugefügter Verweis: der Quellvers existiert nicht mehr in Elb1871"
                : "Kuratierter TSK-Verweis ohne TSK-Zeile (Schlüssel passt zu keinem Eintrag)"
            : r.CurrentWordCount == 0 || r.CurrentWordCount != r.EndPosition - r.StartPosition + 1
                ? $"Wort {Range(r.StartPosition!.Value, r.EndPosition!.Value)}: Position nicht mehr vorhanden"
                : $"Wort {Range(r.StartPosition!.Value, r.EndPosition!.Value)}: jetzt „{r.CurrentQuote}“";
        return new Finding
        {
            Check = code,
            Severity = code == OrphanCheck ? UnmappedRules.Error : UnmappedRules.Warning,
            BibleBookId = book,
            Chapter = chapter,
            Verse = verse,
            German = r.QuotedText,
            Detail = detail,
            TargetBibleBookId = targetBook,
            TargetChapter = targetChapter,
            TargetVerse = targetVerse,
            TargetEndVerse = r.TargetEndHebRefId is { } end ? end % 1000 : null,
            QuotedText = r.QuotedText,
            StartPosition = r.StartPosition,
            EndPosition = r.EndPosition,
            CurrentText = code == DriftCheck ? r.CurrentQuote : null,
        };
    }

    private static (int Book, int Chapter, int Verse) Split(int hebRefId) => (hebRefId / 1_000_000, hebRefId / 1_000 % 1_000, hebRefId % 1_000);
}
