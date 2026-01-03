using System.Globalization;
using System.Net;
using CsvHelper;
using CsvHelper.Configuration;
using LemmaSharp.Classes;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.Elb1871Lemmatizer;

public sealed class LemmatizerRunner : IRunner
{
    private readonly LemmatizerStrategy _lemmatizerStrategy;
    private readonly LemmatizerDownloader _downloader;

    public LemmatizerRunner(LemmatizerStrategy lemmatizerStrategy,
        LemmatizerDownloader downloader)
    {
        _lemmatizerStrategy = lemmatizerStrategy;
        _downloader = downloader;
    }

    public async Task Run(CancellationToken token = default)
    {
        await _downloader.DownloadFileAsync(token);
        await _lemmatizerStrategy.SaveToDatabase(null!, token);
    }
}

public sealed class LemmatizerStrategy : IFileParserStrategy
{
    private readonly LemmatizerRepository _repository;
    private readonly IFileService _fileService;
    private readonly ILogger<LemmatizerStrategy> _logger;
    private readonly AppSettings _settings;

    public LemmatizerStrategy(LemmatizerRepository repository,
        IFileService fileService,
        IOptions<AppSettings> settingsOptions,
        ILogger<LemmatizerStrategy> logger)
    {
        _repository = repository;
        _fileService = fileService;
        _settings = settingsOptions.Value;
        _logger = logger;
    }

    public async Task SaveToDatabase(string filePath, CancellationToken token = default)
    {
        var words = await _repository.GetElberfelderWordsAsync(token);

        var lemmatizerPath = _fileService.GetFileName(_settings.DatabaseSeeding.Elberfelder1871.LemmatizerGermanLink);
        var pathLemmatizer = _fileService.Combine(_settings.DatabaseSeeding.SolutionTempPath, lemmatizerPath);

        using var stream = File.OpenRead(pathLemmatizer);
        var lemmatizer = new Lemmatizer(stream);
        var dictionaryResults = new Dictionary<string, List<string>>();

        foreach (var word in words)
        {
            var result = lemmatizer.Lemmatize(word.Trim('-').Trim('?').Trim('!').Trim('.').Trim(':').Trim(';').Trim('…').Trim('\"').Trim(')').Trim('('));

            if (dictionaryResults.ContainsKey(result))
            {
                dictionaryResults[result].Add(word);
            }
            else
            {
                dictionaryResults.Add(result, [word]);
            }
        }

        var csvDictionary = new List<CsvWordListItem>();

        foreach (var (key, value) in dictionaryResults)
        {
            dictionaryResults[key] = value.OrderBy(x => x).ToList();
            var item = new CsvWordListItem
            {
                Lemma = key,
                Forms = value.JoinStrings("|")
            };
            item.IsSame = item.Forms == item.Lemma;
            csvDictionary.Add(item);
        }

        var path = _fileService.Combine(_settings.DatabaseSeeding.SolutionTempPath, "Elberfelder-lemma-list.csv");
        await using (TextWriter writer = new StreamWriter(path, false, System.Text.Encoding.UTF8))
        {
            var csvWriterConfig = new CsvConfiguration(CultureInfo.InvariantCulture) { Delimiter = ";" };
            await using var csv = new CsvWriter(writer, csvWriterConfig);
            await csv.WriteRecordsAsync(csvDictionary, token);
        }
    }
}

public sealed class CsvWordListItem
{
    public string Lemma { set; get; } = default!;
    public string Forms { set; get; } = default!;
    public bool IsSame { set; get; }
}

public sealed class LemmatizerRepository
{
    private readonly IDbReader _dbReader;
    private readonly IDbWriter _dbWriter;
    private readonly ILogger<LemmatizerRepository> _logger;

    public LemmatizerRepository(IDbReader dbReader, IDbWriter dbWriter, ILogger<LemmatizerRepository> logger)
    {
        _dbReader = dbReader;
        _dbWriter = dbWriter;
        _logger = logger;
    }

    public async Task<List<string>> GetElberfelderWordsAsync(CancellationToken token = default)
    {
        var sql = """
                  select elb."PlainWord" --, count(elb."PlainWord") CountPlainWord
                  from "unshackled-word"."Elb1871Words" elb
                  where elb."BibleBookId" >= 40
                  group by elb."PlainWord"
                  order by count(elb."PlainWord") desc;
                  """;

        var collection = await _dbReader.ReadAsListAsync<StringWrapper>(sql);
        return collection.Select(x => x.PlainWord).ToList();
    }

    public Task UpdateElberfelderLemmaAsync(CancellationToken token = default)
    {
        return Task.CompletedTask;
    }

    private class StringWrapper
    {
        public string PlainWord { get; set; } = default!;
    }
}
