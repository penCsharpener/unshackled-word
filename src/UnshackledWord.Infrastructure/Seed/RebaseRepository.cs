using Dapper;
using UnshackledWord.Application.Abstractions;

namespace UnshackledWord.Infrastructure.Seed;

public sealed class RebaseCounts
{
    public int Words { get; init; }
    public int Morphology { get; init; }
    public int Mappings { get; init; }
}

/// <summary>Database access of the <c>rebase</c> verb: the guards before it writes and the one transaction that empties what is re-imported.</summary>
public sealed class RebaseRepository
{
    private readonly IDbReader _dbReader;
    private readonly IDbConnectionFactory _connectionFactory;

    public RebaseRepository(IDbReader dbReader, IDbConnectionFactory connectionFactory)
    {
        _dbReader = dbReader;
        _connectionFactory = connectionFactory;
    }

    private const string SelectCounts = """
        select
             (select count(*) from "unshackled-word"."Elb1871Words")        as "Words"
            ,(select count(*) from "unshackled-word"."Elb1871Morphology")   as "Morphology"
            ,(select count(*) from "unshackled-word"."Elb1871GreekMapping") + (select count(*) from "unshackled-word"."Elb1871HebrewMapping") as "Mappings"
        """;

    private const string SelectMappingV2Table = """
        select
             to_regclass('"unshackled-word"."MappingLinks"') is not null
        """;

    private const string CountMappingV2Rows = """
        select
             count(*)
        from "unshackled-word"."MappingLinks"
        """;

    private const string Clear = """
        delete from "unshackled-word"."Elb1871Morphology";
        delete from "unshackled-word"."Elb1871GreekMapping";
        delete from "unshackled-word"."Elb1871HebrewMapping";
        delete from "unshackled-word"."SeedVerseState" where "Kind" = 'mappings';
        delete from "unshackled-word"."Elb1871Words";
        delete from "unshackled-word"."Elb1871Verses";
        """;

    public async Task<RebaseCounts> ReadCountsAsync()
    {
        return (await _dbReader.ReadFirstOrDefaultAsync<RebaseCounts>(SelectCounts))!;
    }

    /// <summary>True when Mapping v2 (roadmap 0005, table <c>MappingLinks</c>) exists and holds rows; there the registry sync is the path, not a rebase.</summary>
    public async Task<bool> MappingV2HoldsRowsAsync()
    {
        if (!await _dbReader.ExecuteScalarAsync<bool>(SelectMappingV2Table))
        {
            return false;
        }

        return await _dbReader.ExecuteScalarAsync<int>(CountMappingV2Rows) > 0;
    }

    /// <summary>Empties words, verses, morphology and the legacy mapping tables (with their seed state) in one transaction.</summary>
    public async Task ClearAsync(CancellationToken token)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(Clear, transaction: transaction, commandTimeout: 900);
        transaction.Commit();
    }
}
