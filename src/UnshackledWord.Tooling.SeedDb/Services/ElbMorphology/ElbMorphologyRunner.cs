using System.Diagnostics;
using System.Globalization;
using CsvHelper.Configuration;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Infrastructure.Repositories;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.ElbMorphology;

public sealed class ElbMorphologyRunner : IRunner
{
    private readonly IFileService _fileService;
    private readonly IDbWriter _writer;
    private readonly ILogger<ElbMorphologyRunner> _logger;
    private readonly DatabaseSeedSettings _options;

    public ElbMorphologyRunner(IFileService fileService, IDbWriter writer, IOptions<AppSettings> options, ILogger<ElbMorphologyRunner> logger)
    {
        _fileService = fileService;
        _writer = writer;
        _logger = logger;
        _options = options.Value.DatabaseSeeding;
    }

    public async Task Run(CancellationToken token = default)
    {
        var filePath = _fileService.Combine(_options.SolutionTempPath, "elberfelder1871_morphology.csv");

        if (!_fileService.FileExists(filePath))
        {
            _logger.LogWarning("Elberfelder morphology file not found at {path}. Skipping morphology parsing.", filePath);
            return;
        }

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = "\t",
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim,
        };

        using var textReader = new StreamReader(filePath);
        using var csvReader = new CsvHelper.CsvReader(textReader, config);

        var records = csvReader.GetRecords<ElbMorph>().ToList();

        await EnsureTableExistsAsync();
        await SaveCsvDataToDatabaseAsync(records, token);
    }

    private async Task SaveCsvDataToDatabaseAsync(ICollection<ElbMorph> records, CancellationToken token = default)
    {
        if (records.Count == 0)
        {
            return;
        }

        var dataSize = records.Count + 1;
        var parameters = new
        {
            Id = new List<int>(dataSize),
            HebRefId = new List<int>(dataSize),
            PositionInVerse = new List<int>(dataSize),
            Lemma = new List<string>(dataSize),
            PartOfSpeech = new List<string?>(dataSize),
            Degree = new List<string?>(dataSize),
            VerbForm = new List<string?>(dataSize),
            Stts = new List<string?>(dataSize),
            Tense = new List<string?>(dataSize),
            Person = new List<string?>(dataSize),
            Number = new List<string?>(dataSize),
            Mood = new List<string?>(dataSize),
            Case = new List<string?>(dataSize),
            Gender = new List<string?>(dataSize)
        };

        var i = 1;
        foreach (var entry in records)
        {
            parameters.Id.Add(i);
            parameters.HebRefId.Add(entry.HebRefId);
            parameters.PositionInVerse.Add(entry.PositionInVerse);
            parameters.Lemma.Add(entry.Lemma);
            parameters.PartOfSpeech.Add(entry.PartOfSpeech);
            parameters.Degree.Add(entry.Degree.IsNullOrEmpty() ? null : entry.Degree);
            parameters.VerbForm.Add(entry.VerbForm.IsNullOrEmpty() ? null : entry.VerbForm);
            parameters.Stts.Add(entry.Stts.IsNullOrEmpty() ? null : entry.Stts);
            parameters.Tense.Add(entry.Tense.IsNullOrEmpty() ? null : entry.Tense);
            parameters.Person.Add(entry.Person.IsNullOrEmpty() ? null : entry.Person);
            parameters.Number.Add(entry.Number.IsNullOrEmpty() ? null : Shorten(entry.Number));
            parameters.Mood.Add(entry.Mood.IsNullOrEmpty() ? null : entry.Mood);
            parameters.Case.Add(entry.Case.IsNullOrEmpty() ? null : entry.Case);
            parameters.Gender.Add(entry.Gender.IsNullOrEmpty() ? null : Shorten(entry.Gender));

            i++;
        }

        var names = PropertyListHelper.GetPropertyNames(parameters);

        var sql = $"""
                   INSERT INTO "unshackled-word"."Elb1871Morphology" (
                       {names.Select(x => $"\"{x}\"").JoinStrings(",")}
                   )
                   SELECT *
                   FROM UNNEST({names.Select(x => $"@{x}").JoinStrings(",")})
                   ON CONFLICT ("HebRefId", "PositionInVerse") DO UPDATE SET
                       "PartOfSpeech" = EXCLUDED."PartOfSpeech",
                       "Stts" = EXCLUDED."Stts",
                       "Degree" = EXCLUDED."Degree",
                       "VerbForm" = EXCLUDED."VerbForm",
                       "Tense" = EXCLUDED."Tense",
                       "Person" = EXCLUDED."Person",
                       "Number" = EXCLUDED."Number",
                       "Mood" = EXCLUDED."Mood",
                       "Case" = EXCLUDED."Case",
                       "Gender" = EXCLUDED."Gender";
                   """;

        await _writer.ExecuteScalarAsync<int>(sql, parameters);
    }

    private string? Shorten(string? input)
    {
        return input switch
        {
            "Fem" => "F",
            "Masc" => "M",
            "Neut" => "N",
            "Plur" => "P",
            "Sing" => "S",
            _ => throw new UnreachableException($"Unexpected input: {input}")
        };
    }

    private async Task EnsureTableExistsAsync()
    {
        var sql = """
                  CREATE TABLE IF NOT EXISTS "unshackled-word"."Elb1871Morphology" (
                      "Id" SERIAL PRIMARY KEY,
                      "HebRefId" INTEGER NOT NULL,
                      "PositionInVerse" INTEGER NOT NULL,
                      "Lemma" VARCHAR(255) NOT NULL,
                      "PartOfSpeech" VARCHAR(10) NOT NULL,
                      "Stts" VARCHAR(10),
                      "Degree" VARCHAR(5),
                      "VerbForm" VARCHAR(5),
                      "Tense" VARCHAR(5),
                      "Person" VARCHAR(2),
                      "Number" VARCHAR(2),
                      "Mood" VARCHAR(5),
                      "Case" VARCHAR(5),
                      "Gender" VARCHAR(2),

                      CONSTRAINT "UQ_Elb1871Morphology_Verse_Position" UNIQUE ("HebRefId", "PositionInVerse")
                  );
                  """;

        await _writer.ExecuteScalarAsync<int>(sql);
    }
}
/*
Fem	Plur	1
Masc	Sing	3
Neut	Sing	2
 */
