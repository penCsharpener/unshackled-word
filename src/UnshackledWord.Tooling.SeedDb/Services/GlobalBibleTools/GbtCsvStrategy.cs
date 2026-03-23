using System.Globalization;
using System.Text;
using CsvHelper.Configuration;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.Dbo;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.GlobalBibleTools;

public sealed class GbtCsvStrategy : IFileParserStrategy
{
    private readonly IDbReader _dbReader;
    private readonly IDbWriter _dbWriter;
    private readonly AppSettings _appSettings;
    private static string nl = Environment.NewLine;

    public GbtCsvStrategy(IDbReader dbReader, IDbWriter dbWriter, IOptions<AppSettings> options)
    {
        _dbReader = dbReader;
        _dbWriter = dbWriter;
        _appSettings = options.Value;
    }

    public async Task SaveToDatabase(string filePath, CancellationToken token = default)
    {
        var words = await ReadFromCsvAsync(token);
        var sortedWords = words.OrderBy(x => x.BibleReference.RefId)
            .ThenBy(x => x.PositionInVerse)
            .ToList();

        await WriteWordsToDatabase(sortedWords, token);
    }

    private async Task WriteWordsToDatabase(List<GbtParsedWord> words, CancellationToken token = default)
    {
        var count = await GetCountAsync();

        if (count > 0)
        {
            return;
        }

        const int batchSize = 10000;
        var totalBatches = (int)Math.Ceiling((double)words.Count / batchSize);

        for (int i = 0; i < totalBatches; i++)
        {
            var batch = words.Skip(i * batchSize).Take(batchSize).ToList();

            var rowList = new List<string>();
            foreach (var word in batch)
            {
                rowList.Add($"({word.BibleReference.RefId}, {word.PositionInVerse}, '{word.Text.Replace("'", "''")}', '{word.GrammarKey}')");
            }

            var sqlBatch = $"""
                            INSERT INTO {GbtSourceWordDbo.DboName}
                                ("LxxRefId", "PositionInVerse", "SourceWord", "GrammarKey")
                                VALUES
                                {rowList.JoinStrings($",{nl}    ")};
                            """;

            await _dbWriter.WriteAsync(sqlBatch);
        }
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

    private async Task<int> GetCountAsync()
    {
        var sql = $"""
                   SELECT COUNT(*)
                   FROM {GbtSourceWordDbo.DboName}
                   """;

        return await _dbReader.ExecuteScalarAsync<int>(sql);
    }
}
