using System.Globalization;
using CsvHelper.Configuration;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Dbo;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;

public class HyphenFixUndoRunner : IRunner
{
    private readonly IFileService _fileService;
    private readonly IDbWriter _writer;
    private readonly DatabaseSeedSettings _options;

    public HyphenFixUndoRunner(IFileService fileService, IDbWriter writer, IOptions<AppSettings> options)
    {
        _fileService = fileService;
        _writer = writer;
        _options = options.Value.DatabaseSeeding;
    }

    public async Task Run(CancellationToken token = default)
    {
        await RemoveAddedEntriesAsync(token);

        await RestoreFromBackupAsync<Elb1871WordDbo>("_Elb1871Words__20260427.tsv", RestoreElbWords, token);
        await RestoreFromBackupAsync<Elb1871GreekMappingDbo>("_Elb1871GreekMapping__20260427.tsv", RestoreElbGreekMappings, token);
        await RestoreFromBackupAsync<Elb1871HebrewMappingDbo>("_Elb1871HebrewMapping__20260427.tsv", RestoreElbHebrewMappings, token);
        await RestoreFromBackupAsync<Elb1871MorphologyDbo>("_ElbMorphologyRaw__20260427.tsv", RestoreElbMorph, token);
        await RestoreFromBackupAsync<Elb1871VersesDbo>("_Elb1871Verses__20260503.tsv", RestoreElbVerses, token);

        await ResetIdsAsync(token);
    }

    private async Task RestoreElbVerses(ICollection<Elb1871VersesDbo> list, CancellationToken arg2)
    {
        var capacity = list.Count;

        var parameters = new
        {
            Id = new List<int>(capacity),
            VerseText = new List<string>(capacity),
        };

        foreach (var item in list)
        {
            parameters.Id.Add(item.Id);
            parameters.VerseText.Add(item.VerseText);
        }

        var sql = """
                  UPDATE "unshackled-word"."Elb1871Verses" ev
                  SET "VerseText" = t."VerseText"
                  FROM UNNEST(@Id,@VerseText)
                      AS t("Id","VerseText")
                  WHERE ev."Id" = t."Id";
                  """;

        await _writer.WriteAsync(sql, parameters);
    }

    private async Task ResetIdsAsync(CancellationToken token)
    {
        var sql = """
                  SELECT setval(
                      pg_get_serial_sequence('"unshackled-word"."Elb1871Words"', 'Id'),
                      (SELECT MAX("Id") FROM "unshackled-word"."Elb1871Words")
                  );

                  SELECT setval(
                      pg_get_serial_sequence('"unshackled-word"."ElbMorphologyRaw"', 'Id'),
                      (SELECT MAX("Id") FROM "unshackled-word"."ElbMorphologyRaw")
                  );
                  """;

        await _writer.WriteAsync(sql);
    }

    private async Task RemoveAddedEntriesAsync(CancellationToken token)
    {
        var sql = """
                  DELETE FROM "unshackled-word"."Elb1871HebrewMapping"
                  WHERE "ElbWordId" >= 999999;
                  DELETE FROM "unshackled-word"."Elb1871GreekMapping"
                  WHERE "ElbWordId" >= 999999;
                  DELETE FROM "unshackled-word"."Elb1871Words"
                  WHERE "Id" > 722257;
                  DELETE FROM "unshackled-word"."ElbMorphologyRaw"
                  WHERE "Id" > 722257;
                  """;

        await _writer.WriteAsync(sql);
    }

    private async Task RestoreElbGreekMappings(ICollection<Elb1871GreekMappingDbo> list, CancellationToken token = default)
    {
        var capacity = list.Count;

        var parameters = new
        {
            Id = new List<int>(capacity),
            ElbWordId = new List<int>(capacity),
            StepWordId = new List<int?>(capacity),
            ParentGermanWordId = new List<int?>(capacity),
            PositionInVerse = new List<int>(capacity),
        };

        foreach (var item in list)
        {
            parameters.Id.Add(item.Id);
            parameters.ElbWordId.Add(item.ElbWordId);
            parameters.StepWordId.Add(item.StepWordId);
            parameters.ParentGermanWordId.Add(item.ParentGermanWordId);
            parameters.PositionInVerse.Add(item.PositionInVerse);
        }

        var sql = """
                  UPDATE "unshackled-word"."Elb1871GreekMapping" egm
                  SET "PositionInVerse" = t."PositionInVerse"
                  FROM UNNEST(@Id,@PositionInVerse)
                      AS t("Id","PositionInVerse")
                  WHERE egm."Id" = t."Id";
                  """;

        await _writer.WriteAsync(sql, parameters);
    }

    private async Task RestoreElbHebrewMappings(ICollection<Elb1871HebrewMappingDbo> list, CancellationToken token = default)
    {
        var capacity = list.Count;

        var parameters = new
        {
            Id = new List<int>(capacity),
            PositionInVerse = new List<int>(capacity),
        };

        foreach (var item in list)
        {
            parameters.Id.Add(item.Id);
            parameters.PositionInVerse.Add(item.PositionInVerse);
        }

        var sql = """
                  UPDATE "unshackled-word"."Elb1871HebrewMapping" ehm
                  SET "PositionInVerse" = t."PositionInVerse"
                  FROM UNNEST(@Id,@PositionInVerse)
                      AS t("Id","PositionInVerse")
                  WHERE ehm."Id" = t."Id";
                  """;

        await _writer.WriteAsync(sql, parameters);
    }

    private async Task RestoreElbMorph(ICollection<Elb1871MorphologyDbo> list, CancellationToken token = default)
    {
        var capacity = list.Count;

        var parameters = new
        {
            Id = new List<int>(capacity),
            HebRefId = new List<int>(capacity),
            PositionInVerse = new List<int>(capacity),
            Lemma = new List<string>(capacity),
            PartOfSpeech = new List<string>(capacity),
            Degree = new List<string?>(capacity),
            VerbForm = new List<string?>(capacity),
            Category = new List<string?>(capacity),
            Tense = new List<string?>(capacity),
            Person = new List<string?>(capacity),
            Number = new List<string?>(capacity),
            Mood = new List<string?>(capacity),
            Case = new List<string?>(capacity),
            Gender = new List<string?>(capacity),
        };

        foreach (var item in list)
        {
            parameters.Id.Add(item.Id);
            parameters.HebRefId.Add(item.HebRefId);
            parameters.PositionInVerse.Add(item.PositionInVerse);
            parameters.Lemma.Add(item.Lemma);
            parameters.PartOfSpeech.Add(item.PartOfSpeech);
            parameters.Degree.Add(item.Degree);
            parameters.VerbForm.Add(item.VerbForm);
            parameters.Category.Add(item.Stts);
            parameters.Tense.Add(item.Tense);
            parameters.Person.Add(item.Person);
            parameters.Number.Add(item.Number);
            parameters.Mood.Add(item.Mood);
            parameters.Case.Add(item.Case);
            parameters.Gender.Add(item.Gender);
        }

        var sql = """
                  UPDATE "unshackled-word"."ElbMorphologyRaw" emr
                  SET "PositionInVerse" = t."PositionInVerse"
                  FROM UNNEST(@Id,@PositionInVerse)
                      AS t("Id","PositionInVerse")
                  WHERE emr."Id" = t."Id";
                  """;

        await _writer.WriteAsync(sql, parameters);
    }

    private async Task RestoreElbWords(ICollection<Elb1871WordDbo> list, CancellationToken token = default)
    {
        var capacity = list.Count;

        var parameters = new
        {
            Id = new List<int>(capacity),
            BibleBookId = new List<int>(capacity),
            Chapter = new List<int>(capacity),
            Verse = new List<int>(capacity),
            HebRefId = new List<int>(capacity),
            PositionInVerse = new List<int>(capacity),
            WordInContext = new List<string>(capacity),
            PlainWord = new List<string?>(capacity),
        };

        foreach (var item in list)
        {
            parameters.Id.Add(item.Id);
            parameters.BibleBookId.Add(item.BibleBookId);
            parameters.Chapter.Add(item.Chapter);
            parameters.Verse.Add(item.Verse);
            parameters.HebRefId.Add(item.HebRefId);
            parameters.PositionInVerse.Add(item.PositionInVerse);
            parameters.WordInContext.Add(item.WordInContext);
            parameters.PlainWord.Add(item.PlainWord);
        }

        var sql = """
                  UPDATE "unshackled-word"."Elb1871Words" ew
                  SET "WordInContext" = t."WordInContext",
                      "PlainWord" = t."PlainWord",
                      "PositionInVerse" = t."PositionInVerse"
                  FROM (
                      SELECT *
                      FROM UNNEST(@Id,@BibleBookId,@Chapter,@Verse,@HebRefId,@PositionInVerse,@WordInContext,@PlainWord)
                      AS u("Id","BibleBookId","Chapter","Verse","HebRefId","PositionInVerse","WordInContext","PlainWord")
                      ORDER BY "Id" ASC
                  ) AS t
                  WHERE ew."Id" = t."Id";
                  """;

        await _writer.WriteAsync(sql, parameters);
    }

    private async Task RestoreFromBackupAsync<T>(string filename, Func<ICollection<T>, CancellationToken, Task> restoreAsync, CancellationToken token = default)
    {
        var filePath = _fileService.Combine(_options.SolutionTempPath, "Backup-Before-Elb-HyphenFix", filename);
        using var textReader = new StreamReader(filePath);
        using var csvReader = new CsvHelper.CsvReader(textReader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = "\t",
            HasHeaderRecord = true,
            HeaderValidated = null,
            MissingFieldFound = null,
        });

        var list = new List<T>();
        await foreach (var row in csvReader.GetRecordsAsync<T>(token))
        {
            list.Add(row);
        }

        await restoreAsync(list, token);
    }
}
