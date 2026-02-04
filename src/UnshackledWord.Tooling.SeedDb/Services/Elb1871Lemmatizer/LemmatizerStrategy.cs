using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using LemmaSharp.Classes;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.Elb1871Lemmatizer;

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