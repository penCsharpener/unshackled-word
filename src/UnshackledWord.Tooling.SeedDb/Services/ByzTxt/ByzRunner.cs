using System.Globalization;
using CsvHelper.Configuration.Attributes;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.ByzTxt;

public class ByzRunner : IRunner
{
    private readonly ByzTxtStrategy _byzTxtStrategy;
    private readonly ILogger<ByzRunner> _logger;

    public ByzRunner(ByzTxtStrategy byzTxtStrategy, ILogger<ByzRunner> logger)
    {
        _byzTxtStrategy = byzTxtStrategy;
        _logger = logger;
    }

    public async Task Run(CancellationToken token = default)
    {
        await _byzTxtStrategy.SaveToDatabase("", token);
    }
}

public sealed class ByzTxtStrategy : IFileParserStrategy
{
    private readonly IFileService _fileService;
    private readonly IDbWriter _dbWriter;
    private readonly ByzantineSettings _options;
    private readonly HttpClient _githubClient;
    private static string _delimiter = $",{Environment.NewLine}    ";

    public ByzTxtStrategy(IFileService fileService, IDbWriter dbWriter, IHttpClientFactory clientFactory, IOptions<AppSettings> options)
    {
        _fileService = fileService;
        _dbWriter = dbWriter;
        _githubClient = clientFactory.CreateClient("Github");
        _options = options.Value.DatabaseSeeding.ByzantineSettings;
    }

    public async Task SaveToDatabase(string filePath, CancellationToken token = default)
    {
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

public sealed class ByzTxtEntity
{
    [Ignore]
    public int BibleBookId { get; set; }
    [Name("chapter")]
    public int Chapter { get; set; }
    [Name("verse")]
    public int Verse { get; set; }
    [Name("text")]
    public string CsvText { get; set; } = default;
    [Ignore]
    public List<ByzTxtWord> ByzWords { get; set; } = new();
}

public sealed class ByzTxtWord
{
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public int SortNumber { get; set; }
    public string Word { get; set; } = default;
    public string StrongNumber { get; set; } = default;
    public string Morphology { get; set; } = default;
}

public sealed class Constants
{
    public static readonly Dictionary<string, int> ByzTxtDownloadFileNames = new()
    {
        { "MAT", 40 },
        { "MAR", 41 },
        { "LUK", 42 },
        { "JOH", 43 },
        { "ACT", 44 },
        { "ROM", 45 },
        { "1CO", 46 },
        { "2CO", 47 },
        { "GAL", 48 },
        { "EPH", 49 },
        { "PHP", 50 },
        { "COL", 51 },
        { "1TH", 52 },
        { "2TH", 53 },
        { "1TI", 54 },
        { "2TI", 55 },
        { "TIT", 56 },
        { "PHM", 57 },
        { "HEB", 58 },
        { "JAM", 59 },
        { "1PE", 60 },
        { "2PE", 61 },
        { "1JO", 62 },
        { "2JO", 63 },
        { "3JO", 64 },
        { "JUD", 65 },
        { "REV", 66 },
    };
}
