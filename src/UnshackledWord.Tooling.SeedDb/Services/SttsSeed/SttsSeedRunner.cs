using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.SttsSeed;

/// <summary>
/// Seeds "SttsTags" from assets/seed/stts-tags.csv (the 54 STTS tags). Runs first, before any morphology import, because
/// Elb1871Morphology.Stts references the table. Idempotent: tags that already exist are left alone, missing ones are inserted.
/// </summary>
public sealed class SttsSeedRunner : IRunner
{
    public const string CsvFile = "seed/stts-tags.csv";

    private const string InsertSql = """
        INSERT INTO "unshackled-word"."SttsTags" ("Tag", "LabelDe", "LabelEn", "DescriptionDe", "DescriptionEn", "ExampleDe", "Upos", "AllowedUpos", "Alias", "IsPunctuation", "SortOrder")
        VALUES (@Tag, @LabelDe, @LabelEn, @DescriptionDe, @DescriptionEn, @ExampleDe, @Upos, @AllowedUpos, @Alias, @IsPunctuation, @SortOrder)
        ON CONFLICT ("Tag") DO NOTHING
        """;

    private readonly IFileService _fileService;
    private readonly IDbWriter _dbWriter;
    private readonly AppSettings _options;
    private readonly ILogger<SttsSeedRunner> _logger;

    public SttsSeedRunner(IFileService fileService, IDbWriter dbWriter, IOptions<AppSettings> options, ILogger<SttsSeedRunner> logger)
    {
        _fileService = fileService;
        _dbWriter = dbWriter;
        _options = options.Value;
        _logger = logger;
    }

    public async Task Run(CancellationToken token = default)
    {
        var path = _fileService.Combine(_options.DatabaseSeeding.SolutionAssetsPath, CsvFile);
        if (!_fileService.FileExists(path))
        {
            _logger.LogWarning("STTS seed file not found at {Path}; SttsTags stays as it is", path);
            return;
        }

        var rows = await ReadRowsAsync(path, token);
        var inserted = 0;
        foreach (var row in rows)
        {
            inserted += await _dbWriter.WriteAsync(InsertSql, new
            {
                row.Tag,
                row.LabelDe,
                row.LabelEn,
                row.DescriptionDe,
                row.DescriptionEn,
                row.ExampleDe,
                row.Upos,
                AllowedUpos = row.AllowedUpos.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                Alias = string.IsNullOrWhiteSpace(row.Alias) ? null : row.Alias,
                row.IsPunctuation,
                row.SortOrder,
            });
        }

        _logger.LogInformation("SttsTags: {Inserted} of {Total} tags inserted", inserted, rows.Count);
    }

    private static async Task<List<SttsTagCsvRow>> ReadRowsAsync(string path, CancellationToken token)
    {
        var settings = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true, Encoding = Encoding.UTF8 };
        using var reader = new StreamReader(path, Encoding.UTF8);
        using var csv = new CsvReader(reader, settings);
        var rows = new List<SttsTagCsvRow>();
        await foreach (var row in csv.GetRecordsAsync<SttsTagCsvRow>(token))
        {
            rows.Add(row);
        }

        return rows;
    }
}
