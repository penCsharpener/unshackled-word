using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Backup;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.Models.Dbo;
using UnshackledWord.Tooling.WebApi.Models;

namespace UnshackledWord.Tooling.WebApi.Endpoints.BibleTagger.BackupElbData.Mappings;

public class BackupFileService
{
    private readonly AppSettings _options;
    private readonly IElbDashboardRepository _repository;
    private readonly IFileService _file;
    private readonly IDbReader _reader;

    public BackupFileService(IElbDashboardRepository repository, IFileService file, IDbReader reader, IOptions<AppSettings> options)
    {
        _options = options.Value;
        _repository = repository;
        _file = file;
        _reader = reader;
    }

    public async Task WriteMappingBackupAsync(CancellationToken token = default)
    {
        var backupPath = _file.Combine(_options.SolutionAssetsPath, _options.BackupLocationPath);
        var mappingRows = await _repository.CreateBackupAsync(token);

        foreach (var (bookId, mappings) in mappingRows)
        {
            var book = BibleBook.AllBooks[bookId];

            var csvPath = _file.Combine(backupPath, $"{bookId.ToString().PadLeft(2, '0')}-{book.Name}.csv");

            if (_file.FileExists(csvPath))
            {
                _file.DeleteFile(csvPath);
            }

            await using var writer = new StreamWriter(csvPath);
            await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            await csv.WriteRecordsAsync(mappings, token);
        }
    }

    public async Task WriteElb1871TextBackupAsync(CancellationToken token = default)
    {
        var backupFilePath = _file.Combine(_options.SolutionAssetsPath, "elberfelder1871-theword-export.txt");

        var sql = """
                  SELECT DISTINCT ev."HebRefId", CONCAT(bb."Name", '$', ew."Chapter", ':', ew."Verse", ' || ', bb."Name", '$', ((ev."LxxRefId" % 1000000) / 1000) || ':' || (ev."LxxRefId" % 1000), ' || ', ev."VerseText") "WordExportTxt"
                  FROM "unshackled-word"."Elb1871Verses" ev
                    INNER JOIN "unshackled-word"."Elb1871Words" ew ON ev."HebRefId" = ew."HebRefId"
                    INNER JOIN "unshackled-word"."BibleBooks" bb ON ew."BibleBookId" = bb."Id"
                  ORDER BY ev."HebRefId";
                  """;

        var lines = await _reader.ReadAsListAsync<ElbVerseBackupLine>(sql);
        var bibleText = lines.Select(x => x.WordExportTxt).JoinStrings(Environment.NewLine);

        await _file.SafelyOverwriteAllTextAsync(backupFilePath, bibleText, Encoding.UTF8, token);
    }

    public async Task WriteElbMorphologyBackupAsync(CancellationToken token = default)
    {
        var sql = """
                  SELECT em."HebRefId"
                       , em."PositionInVerse"
                       , em."Lemma"
                       , em."PartOfSpeech"
                       , em."Stts"
                       , em."Degree"
                       , em."VerbForm"
                       , em."Tense"
                       , em."Person"
                       , em."Number"
                       , em."Mood"
                       , em."Case"
                       , em."Gender"
                  FROM "unshackled-word"."Elb1871Morphology" em
                  ORDER BY em."HebRefId", em."PositionInVerse";
                  """;

        var lines = await _reader.ReadAsListAsync<ElbMorphBackupItem>(sql);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = "\t",
            HasHeaderRecord = true
        };

        var backupDirectory = _file.Combine(_options.SolutionAssetsPath, "Elb1871Morphology");
        _file.CreateDirectoryIfNotExists(backupDirectory);

        foreach (var group in lines.GroupBy(x => BibleReference.FromRefId(x.HebRefId).BookId))
        {
            var bookName = BibleBook.AllBooks[group.Key].Name;
            var fileName = $"{group.Key}-{bookName}";

            var backupFilePath = _file.Combine(backupDirectory, $"{fileName}.csv");

            var bookBackup = group.Select(x => x)
                .OrderBy(x => x.HebRefId)
                .ThenBy(x => x.PositionInVerse)
                .ToList();

            await using var writer = new StreamWriter(backupFilePath);
            await using var csv = new CsvWriter(writer, config);
            await csv.WriteRecordsAsync(bookBackup, token);
        }
    }

    private sealed class ElbMorphBackupItem
    {
        public int HebRefId { get; set; }
        public int PositionInVerse { get; set; }
        public string Lemma { get; set; } = default!;
        public string PartOfSpeech { get; set; } = default!;
        public string Stts { get; set; } = default!;
        public string? Degree { get; set; }
        public string? VerbForm { get; set; }
        public string? Tense { get; set; }
        public string? Person { get; set; }
        public string? Number { get; set; }
        public string? Mood { get; set; }
        public string? Case { get; set; }
        public string? Gender { get; set; }
    }

    private class ElbVerseBackupLine
    {
        public int HebRefId { get; set; }
        public string WordExportTxt { get; set; } = default!;
    }
}
