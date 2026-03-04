using System.Globalization;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.ByzTxt;

public sealed class ByzTxtStrategy : IFileParserStrategy
{
    private readonly IFileService _fileService;
    private readonly IDbWriter _dbWriter;
    private readonly IDbReader _dbReader;
    private readonly ILogger<ByzTxtStrategy> _logger;
    private readonly ByzantineSettings _options;
    private readonly HttpClient _githubClient;
    private static string _delimiter = $",{Environment.NewLine}    ";

    public ByzTxtStrategy(IFileService fileService,
        IDbWriter dbWriter,
        IDbReader dbReader,
        IHttpClientFactory clientFactory,
        IOptions<AppSettings> options,
        ILogger<ByzTxtStrategy> logger)
    {
        _fileService = fileService;
        _dbWriter = dbWriter;
        _dbReader = dbReader;
        _logger = logger;
        _githubClient = clientFactory.CreateClient("Github");
        _options = options.Value.DatabaseSeeding.ByzantineSettings;
    }

    public async Task SaveToDatabase(string filePath, CancellationToken token = default)
    {
        var count = await GetCountAsync();
        if (count > 0)
        {
            _logger.LogInformation("Byzantine text data already imported... {count} rows", count);
            return;
        }

        var allEntries = new List<ByzTxtWord>();

        foreach (var fileName in Constants.ByzTxtDownloadFileNames.Keys)
        {
            var downloadedContent = await DownloadFileAsync(fileName, token);
            var bibleBookId = Constants.ByzTxtDownloadFileNames[fileName];

            using var textReader = new StringReader(downloadedContent);
            using var csvReader = new CsvHelper.CsvReader(textReader, CultureInfo.InvariantCulture);

            await foreach (var row in csvReader.GetRecordsAsync<ByzTxtEntity>(token))
            {
                row.BibleBookId = bibleBookId;
                ParseVerse(row);

                allEntries.AddRange(row.ByzWords);
            }
        }

        await InsertIntoDb(allEntries.OrderBy(x => x.BibleBookId)
            .ThenBy(x => x.Chapter)
            .ThenBy(x => x.Verse)
            .ThenBy(x => x.SortNumber).ToList(), token);
    }

    private async Task<int> GetCountAsync()
    {
        var sql = """
                  select COUNT(*)
                  from "unshackled-word"."ByzTxtWords";
                  """;

        return await _dbReader.ExecuteScalarAsync<int>(sql, CancellationToken.None);
    }

    private void ParseVerse(ByzTxtEntity entity)
    {
        var words = entity.CsvText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var sortNumber = 0;
        var byzWord = CreateByzTxtWord(entity);

        foreach (var word in words)
        {
            var isNumber = int.TryParse(word, out var strongsNumber);
            var isMorphologicalCode = word.Contains('{') || word.Contains('}');
            var isGreekWord = !isNumber && !isMorphologicalCode;

            if (isGreekWord)
            {
                byzWord.Word = word;
            }

            if (isNumber)
            {
                byzWord.StrongNumber = strongsNumber.ToString();
            }

            if (isMorphologicalCode)
            {
                sortNumber++;
                byzWord.Morphology = word.Trim('{').Trim('}');
                entity.ByzWords.Add(byzWord);
                byzWord.SortNumber = sortNumber;
                byzWord = CreateByzTxtWord(entity);
            }
        }
    }

    private ByzTxtWord CreateByzTxtWord(ByzTxtEntity entity)
    {
        return new ByzTxtWord
        {
            BibleBookId = entity.BibleBookId,
            Chapter = entity.Chapter,
            Verse = entity.Verse
        };
    }

    private async Task<string> DownloadFileAsync(string fileName, CancellationToken token = default)
    {
        return await _githubClient.GetStringAsync($"{_options.TextDownloadUrl.TrimEnd('/')}/{fileName}.csv", token);
    }

    private async Task InsertIntoDb(List<ByzTxtWord> allWords, CancellationToken token = default)
    {
        var batchSize = 10000;
        var batchCount = (allWords.Count / batchSize) + 1;

        for (var i = 0; i < batchCount; i++)
        {
            var rowList = new List<string>();

            foreach (var word in allWords.Skip(i * batchSize).Take(batchSize))
            {
                rowList.Add($"({word.BibleBookId}, {word.Chapter}, {word.Verse}, {word.SortNumber}, '{word.Word}', '{word.StrongNumber}', '{word.Morphology}')");
            }

            var sql = $"""
                       INSERT INTO "unshackled-word"."ByzTxtWords" ("BibleBookId", "Chapter", "Verse", "SortNumber", "Word", "StrongNumber", "Morphology")
                       VALUES
                       {rowList.JoinStrings(_delimiter)};
                       """;

            await _dbWriter.WriteAsync(sql, token);
        }
    }
}
