using UnshackledWord.Application.Abstractions;

namespace UnshackledWord.Infrastructure.Seed;

/// <summary>One row of <c>SeedImports</c> as the maintainer page shows it.</summary>
public sealed class SeedImportRow
{
    public int Id { get; init; }
    public string Kind { get; init; } = string.Empty;
    public string SeedVersion { get; init; } = string.Empty;
    public DateTimeOffset? ExportedAt { get; init; }
    public DateTimeOffset ImportedAt { get; init; }
    public int Rows { get; init; }
    public int Exact { get; init; }
    public int Shifted { get; init; }
    public int Modified { get; init; }
    public int Orphaned { get; init; }
    public int? VersesUpdated { get; init; }
    public int? VersesKeptLocal { get; init; }
}

/// <summary>Read-only view of <c>SeedImports</c> (newest first).</summary>
public sealed class SeedImportsRepository
{
    private const string Select = """
        SELECT s."Id"
             , s."Kind"
             , s."SeedVersion"
             , s."ExportedAt"
             , s."ImportedAt"
             , s."Rows"
             , s."Exact"
             , s."Shifted"
             , s."Modified"
             , s."Orphaned"
             , s."VersesUpdated"
             , s."VersesKeptLocal"
        FROM "unshackled-word"."SeedImports" s
        ORDER BY s."ImportedAt" DESC, s."Id" DESC
        LIMIT @Limit
        """;

    private readonly IDbReader _dbReader;

    public SeedImportsRepository(IDbReader dbReader)
    {
        _dbReader = dbReader;
    }

    public async Task<IReadOnlyList<SeedImportRow>> ReadLatestAsync(int limit)
    {
        return (await _dbReader.ReadAsListAsync<SeedImportRow>(Select, new { Limit = limit })).ToList();
    }
}
