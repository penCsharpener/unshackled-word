using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Dbo.Revision;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Revision;

/// <summary>Reads the "Revisions" table (literalness presets).</summary>
public interface IRevisionLevelRepository
{
    Task<IReadOnlyList<RevisionLevelDbo>> GetAllAsync();

    /// <summary>Stored variant versions (active and ended) per level; levels without any are absent.</summary>
    Task<IReadOnlyDictionary<int, int>> GetVariantCountsAsync();

    Task InsertAsync(RevisionLevelDbo level);

    /// <summary>Updates the preset <paramref name="currentLevel"/>; with <c>IsDefault</c> set the default flag moves to it in the same transaction.</summary>
    Task UpdateAsync(int currentLevel, RevisionLevelDbo level);
}

public sealed class RevisionLevelRepository : IRevisionLevelRepository
{
    private const string SelectAll = $"""
        SELECT l."Id"            AS "Id"
             , l."Level"         AS "Level"
             , l."Code"          AS "Code"
             , l."NameDe"        AS "NameDe"
             , l."NameEn"        AS "NameEn"
             , l."DescriptionDe" AS "DescriptionDe"
             , l."DescriptionEn" AS "DescriptionEn"
             , l."IsBase"        AS "IsBase"
             , l."IsDefault"     AS "IsDefault"
        FROM {RevisionLevelDbo.DboName} l
        ORDER BY l."Level"
               , l."Id"
        """;

    private const string SelectVariantCounts = $"""
        SELECT rev."Level" AS "Level"
             , count(*)    AS "Count"
        FROM "unshackled-word"."RevisionVerses" rv
            JOIN {RevisionLevelDbo.DboName} rev ON rev."Id" = rv."RevisionId"
        WHERE NOT rev."IsBase"
        GROUP BY rev."Level"
        """;

    private const string Insert = $"""
        INSERT INTO {RevisionLevelDbo.DboName} ("Level", "Code", "NameDe", "NameEn", "DescriptionDe", "DescriptionEn", "IsBase", "IsDefault")
        VALUES (@Level, @Code, @NameDe, @NameEn, @DescriptionDe, @DescriptionEn, false, @IsDefault)
        """;

    private const string ClearDefault = $"""
        UPDATE {RevisionLevelDbo.DboName}
        SET "IsDefault" = false
        WHERE "IsDefault"
            AND "Id" IS DISTINCT FROM (SELECT r."Id" FROM {RevisionLevelDbo.DboName} r WHERE r."Level" = @CurrentLevel AND NOT r."IsBase")
        """;

    private const string Update = $"""
        UPDATE {RevisionLevelDbo.DboName}
        SET "Level"         = @Level
          , "Code"          = @Code
          , "NameDe"        = @NameDe
          , "NameEn"        = @NameEn
          , "DescriptionDe" = @DescriptionDe
          , "DescriptionEn" = @DescriptionEn
          , "IsDefault"     = @IsDefault
        WHERE "Level" = @CurrentLevel
            AND NOT "IsBase"
        """;

    private readonly IDbReader _reader;
    private readonly IDbConnectionFactory _factory;

    public RevisionLevelRepository(IDbReader reader, IDbConnectionFactory factory)
    {
        _reader = reader;
        _factory = factory;
    }

    public async Task<IReadOnlyDictionary<int, int>> GetVariantCountsAsync() =>
        (await _reader.ReadAsListAsync<LevelCount>(SelectVariantCounts)).ToDictionary(c => c.Level, c => c.Count);

    public async Task InsertAsync(RevisionLevelDbo level)
    {
        using var connection = await _factory.CreateDbConnectionAsync();
        await connection.ExecuteAsync(Insert, level);
    }

    public async Task UpdateAsync(int currentLevel, RevisionLevelDbo level)
    {
        using var connection = await _factory.CreateDbConnectionAsync();
        using var transaction = connection.BeginTransaction();
        var args = new
        {
            CurrentLevel = currentLevel,
            level.Level,
            level.Code,
            level.NameDe,
            level.NameEn,
            level.DescriptionDe,
            level.DescriptionEn,
            level.IsDefault,
        };

        if (level.IsDefault)
        {
            await connection.ExecuteAsync(ClearDefault, args, transaction);
        }

        await connection.ExecuteAsync(Update, args, transaction);
        transaction.Commit();
    }

    private sealed class LevelCount
    {
        public int Level { get; set; }
        public int Count { get; set; }
    }

    public async Task<IReadOnlyList<RevisionLevelDbo>> GetAllAsync() =>
        (await _reader.ReadAsListAsync<RevisionLevelDbo>(SelectAll)).ToList();
}
