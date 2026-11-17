using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Grammar;

namespace UnshackledWord.Infrastructure.Repositories;

public sealed class SttsTagRepository : ISttsTagRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public SttsTagRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private const string SelectAll = """
        SELECT s."Tag"         AS "Tag"
             , s."LabelDe"     AS "LabelDe"
             , s."LabelEn"     AS "LabelEn"
             , s."Upos"        AS "Upos"
             , s."AllowedUpos" AS "AllowedUpos"
        FROM "unshackled-word"."SttsTags" s
        ORDER BY s."SortOrder"
        """;

    public async Task<IReadOnlyList<SttsTag>> GetAllAsync(CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        return (await connection.QueryAsync<SttsTag>(new CommandDefinition(SelectAll, cancellationToken: ct))).ToList();
    }
}
