using Dapper;
using UnshackledWord.Application.Abstractions;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.Search.SourceWords;

/// <summary>One verse that contains a form of a Strong's number.</summary>
public sealed class SourceWordRefRow
{
    public string Strongs { get; init; } = string.Empty;
    public string Form { get; init; } = string.Empty;
    public int BibleBookId { get; init; }
    public int Chapter { get; init; }
    public int Verse { get; init; }
}

public interface ISourceWordRefs
{
    /// <summary>For every (Strong's key, printed form) up to <paramref name="perForm"/> verses in canonical order.</summary>
    Task<IReadOnlyList<SourceWordRefRow>> ReadAsync(IReadOnlyList<(string Strongs, string Form)> forms, int perForm, CancellationToken ct);
}

public sealed class SourceWordRefs : ISourceWordRefs
{
    private readonly IDbConnectionFactory _connectionFactory;

    public SourceWordRefs(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<SourceWordRefRow>> ReadAsync(IReadOnlyList<(string Strongs, string Form)> forms, int perForm, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        return [.. await connection.QueryAsync<SourceWordRefRow>(new CommandDefinition("""
            SELECT x."Strongs"        AS "Strongs"
                 , x."Form"           AS "Form"
                 , r."BibleBookId"    AS "BibleBookId"
                 , r."Chapter"        AS "Chapter"
                 , r."Verse"          AS "Verse"
            FROM unnest(@strongs, @forms) AS x("Strongs", "Form")
                CROSS JOIN LATERAL (SELECT DISTINCT f."BibleBookId", f."Chapter", f."Verse"
                                    FROM "unshackled-word"."DqWordFacts" f
                                    WHERE f."StrongsBase" = x."Strongs"
                                      AND f."SourceWord" = x."Form"
                                      AND f."BibleBookId" IS NOT NULL
                                    ORDER BY f."BibleBookId", f."Chapter", f."Verse"
                                    LIMIT @perForm) r
            """, new
        {
            strongs = forms.Select(f => f.Strongs).ToArray(),
            forms = forms.Select(f => f.Form).ToArray(),
            perForm,
        }, cancellationToken: ct))];
    }
}
