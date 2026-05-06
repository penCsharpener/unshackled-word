using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.Models.Dbo;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Infrastructure.Repositories;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.ElbMorphology;

public sealed class ElbMorphologyBackupImporter : IRunner
{
    private readonly IFileService _file;
    private readonly IDbWriter _writer;
    private readonly DatabaseSeedSettings _options;

    public ElbMorphologyBackupImporter(IFileService file, IDbWriter writer, IOptions<AppSettings> options)
    {
        _file = file;
        _writer = writer;
        _options = options.Value.DatabaseSeeding;
    }

    public async Task Run(CancellationToken token = default)
    {
        var backupDirectory = _file.Combine(_options.SolutionAssetsPath, "Elb1871Morphology");
        var totalRecords = new List<Elb1871MorphologyDbo>();

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = "\t",
            HasHeaderRecord = true
        };

        for (int i = 1; i <= 66; i++)
        {
            var bookName = BibleBook.AllBooks[i].Name;
            var fileName = $"{i}-{bookName}";
            var backupFilePath = _file.Combine(backupDirectory, $"{fileName}.csv");

            using var reader = new StreamReader(backupFilePath);
            using var csv = new CsvReader(reader, config);
            var records = csv.GetRecord<Elb1871MorphologyDbo>();

            totalRecords.AddRange(records);
        }

        var capacity = totalRecords.Count;
        var parameters = new
        {
            HebRefId = new List<int>(capacity),
            PositionInVerse = new List<int>(capacity),
            Lemma = new List<string>(capacity),
            PartOfSpeech = new List<string>(capacity),
            Stts = new List<string>(capacity),
            Degree = new List<string?>(capacity),
            VerbForm = new List<string?>(capacity),
            Tense = new List<string?>(capacity),
            Person = new List<string?>(capacity),
            Number = new List<string?>(capacity),
            Mood = new List<string?>(capacity),
            Case = new List<string?>(capacity),
            Gender = new List<string?>(capacity),
        };

        foreach (var record in totalRecords)
        {
            parameters.HebRefId.Add(record.HebRefId);
            parameters.PositionInVerse.Add(record.PositionInVerse);
            parameters.Lemma.Add(record.Lemma);
            parameters.PartOfSpeech.Add(record.PartOfSpeech);
            parameters.Stts.Add(record.Stts);
            parameters.Degree.Add(record.Degree);
            parameters.VerbForm.Add(record.VerbForm);
            parameters.Tense.Add(record.Tense);
            parameters.Person.Add(record.Person);
            parameters.Number.Add(record.Number);
            parameters.Mood.Add(record.Mood);
            parameters.Case.Add(record.Case);
            parameters.Gender.Add(record.Gender);
        }

        var (quotedNames, parameterNames) = PropertyListHelper.GetAllNames(parameters);

        var sql = $"""
                   BEGIN;

                   INSERT INTO "unshackled-word"."Elb1871Morphology"
                   ({quotedNames})
                   SELECT *
                   FROM UNNEST({parameterNames})
                   ON CONFLICT DO NOTHING;

                   COMMIT;
                   """;
    }
}
