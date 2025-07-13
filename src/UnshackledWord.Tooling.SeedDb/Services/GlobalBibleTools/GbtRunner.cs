using System.Globalization;
using System.Text;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.Extensions.Options;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.Tsk.Extensions;
using UnshackledWord.Tooling.SeedDb.Services.Tsk.Models;

namespace UnshackledWord.Tooling.SeedDb.Services.GlobalBibleTools;

public class GbtRunner : IRunner
{
    private readonly GbtCsvStrategy _gbtCsvStrategy;

    public GbtRunner(GbtCsvStrategy gbtCsvStrategy)
    {
        _gbtCsvStrategy = gbtCsvStrategy;
    }

    public async Task Run(CancellationToken token = default)
    {
        await _gbtCsvStrategy.SaveToDatabase(null, token);
    }
}

public sealed class GbtCsvStrategy : IFileParserStrategy
{
    private readonly AppSettings _appSettings;

    public GbtCsvStrategy(IOptions<AppSettings> options)
    {
        _appSettings = options.Value;
    }

    public async Task SaveToDatabase(string filePath, CancellationToken token = default)
    {
        var words = await ReadFromCsvAsync(token);


    }

    private async Task<List<GbtLemma>> ReadFromCsvLemmasAsync(CancellationToken token = default)
    {
        var settings = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            Delimiter = ";",
            Encoding = Encoding.UTF8
        };

        using var textReader = new StreamReader(_appSettings.DatabaseSeeding.GlobalBibleToolsLemmaCsvFile, Encoding.UTF8);
        using var csvReader = new CsvHelper.CsvReader(textReader, settings);
        var list = new List<GbtLemma>();

        await foreach (var row in csvReader.GetRecordsAsync<GbtLemma>(token))
        {
            list.Add(row);
        }

        return list;
    }

    private async Task<List<GbtParsedWord>> ReadFromCsvAsync(CancellationToken token = default)
    {
        var settings = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            Delimiter = ";",
            Encoding = Encoding.UTF8
        };

        var gbtLemmaList = await ReadFromCsvLemmasAsync(token);
        var gbtLemmaDictionary = gbtLemmaList.ToDictionary(x => x.LemmaId, x => x);

        using var textReader = new StreamReader(_appSettings.DatabaseSeeding.GlobalBibleToolsWordsCsvFile, Encoding.UTF8);
        using var csvReader = new CsvHelper.CsvReader(textReader, settings);
        var list = new List<GbtParsedWord>();

        await foreach (var row in csvReader.GetRecordsAsync<GbtWord>(token))
        {
            var parsedWord = row.ToGbtParsedWord();
            parsedWord.GrammarKey = gbtLemmaDictionary[row.FormId].Grammar;

            list.Add(parsedWord);
        }

        return list;
    }
}

public sealed class GbtLemma
{
    [Name("id")]
    public string LemmaId { get; set; } = null!;
    [Name("grammar")]
    public string Grammar { get; set; } = null!;
    [Name("lemma_id")]
    public string StrongsNumber { get; set; } = null!;
}

public class GbtWord
{
    [Name("id")]
    public string Id { get; set; } = null!;
    [Name("text")]
    public string Text { get; set; } = null!;
    [Name("verse_id")]
    public string VerseId { get; set; } = null!;
    [Name("form_id")]
    public string FormId { get; set; } = null!;

    public GbtParsedWord ToGbtParsedWord()
    {
        var bookId = int.Parse(Id[..2]);
        var chapterId = int.Parse(Id.Substring(2, 3));
        var verseId = int.Parse(Id.Substring(5, 3));
        var sortOrder = int.Parse(Id.Substring(8, 2));

        return new GbtParsedWord
        {
            BibleReference = new BibleReference(bookId, chapterId, verseId),
            BookId = bookId,
            ChapterId = chapterId,
            VerseId = verseId,
            SortNumber = sortOrder,
            Text = Text
        };
    }
}

public class GbtParsedWord
{
    public BibleReference BibleReference { get; set; }
    public int BookId { get; set; }
    public int ChapterId { get; set; }
    public int VerseId { get; set; }
    public int SortNumber { get; set; }
    public string Text { get; set; } = null!;
    public string GrammarKey { get; set; } = null!;
}
