using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Dbo.Revision;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.RevisionSeed;

/// <summary>
/// Seeds "Revisions" from assets/seed/revision/levels.csv (the literalness presets, roadmap 0011.1). Runs only into an empty table:
/// once levels exist (seeded or edited by the maintainer) they are never touched again. "IsBase" is set for the code elb1871.
/// </summary>
public sealed class RevisionLevelSeedRunner : IRunner
{
    public const string CsvFile = "seed/revision/levels.csv";
    /// <summary>Code of the base revision (unrevised Elb1871); several revisions may share its level, so the flag cannot come from the level.</summary>
    public const string BaseCode = "elb1871";

    private const string CountSql = $"""
        SELECT count(*) FROM {RevisionLevelDbo.DboName}
        """;

    private const string InsertSql = $"""
        INSERT INTO {RevisionLevelDbo.DboName} ("Level", "Code", "NameDe", "NameEn", "DescriptionDe", "DescriptionEn", "IsBase", "IsDefault")
        VALUES (@Level, @Code, @NameDe, @NameEn, @DescriptionDe, @DescriptionEn, @IsBase, @IsDefault)
        """;

    private readonly IFileService _fileService;
    private readonly IDbWriter _dbWriter;
    private readonly AppSettings _options;
    private readonly ILogger<RevisionLevelSeedRunner> _logger;

    public RevisionLevelSeedRunner(IFileService fileService, IDbWriter dbWriter, IOptions<AppSettings> options, ILogger<RevisionLevelSeedRunner> logger)
    {
        _fileService = fileService;
        _dbWriter = dbWriter;
        _options = options.Value;
        _logger = logger;
    }

    public async Task Run(CancellationToken token = default)
    {
        var existing = await _dbWriter.ExecuteScalarAsync<long>(CountSql);
        if (existing > 0)
        {
            _logger.LogInformation("RevisionLevels already holds {Count} rows; level seed skipped", existing);
            return;
        }

        var path = _fileService.Combine(_options.DatabaseSeeding.SolutionAssetsPath, CsvFile);
        if (!_fileService.FileExists(path))
        {
            _logger.LogWarning("Revision level seed file not found at {Path}; RevisionLevels stays empty", path);
            return;
        }

        var rows = await ReadRowsAsync(path, token);
        foreach (var row in rows)
        {
            await _dbWriter.WriteAsync(InsertSql, new
            {
                row.Level,
                row.Code,
                row.NameDe,
                row.NameEn,
                DescriptionDe = string.IsNullOrWhiteSpace(row.DescriptionDe) ? null : row.DescriptionDe,
                DescriptionEn = string.IsNullOrWhiteSpace(row.DescriptionEn) ? null : row.DescriptionEn,
                IsBase = row.Code == BaseCode,
                row.IsDefault,
            });
        }

        _logger.LogInformation("RevisionLevels: {Count} levels inserted", rows.Count);
    }

    private static async Task<List<RevisionLevelCsvRow>> ReadRowsAsync(string path, CancellationToken token)
    {
        var settings = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true, Encoding = Encoding.UTF8 };
        using var reader = new StreamReader(path, Encoding.UTF8);
        using var csv = new CsvReader(reader, settings);
        var rows = new List<RevisionLevelCsvRow>();
        await foreach (var row in csv.GetRecordsAsync<RevisionLevelCsvRow>(token))
        {
            rows.Add(row);
        }

        return rows;
    }
}
