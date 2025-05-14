using Dapper;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.BibelKommentare;

namespace UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;

public class ElbRunner : IRunner
{
    private readonly IFileService _fileService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DatabaseSeedSettings _options;

    public ElbRunner(IFileService fileService, IServiceScopeFactory scopeFactory, IOptions<AppSettings> options)
    {
        _fileService = fileService;
        _scopeFactory = scopeFactory;
        _options = options.Value.DatabaseSeeding;
    }

    public async Task Run(CancellationToken token = default)
    {
        using var scope = _scopeFactory.CreateScope();
        IList<ElbVerse> bkList = null!;
        IList<Elb1871Verse> elb1871List = null!;

        var bkFilePath = "../../temp/SeedData/Elb/bible_elb_bk_mybible.xml";
        if (_fileService.FileExists(bkFilePath))
        {
            var strategy = scope.ServiceProvider.GetRequiredService<ElbParserStrategy>();

            await strategy.SaveToDatabase(bkFilePath, token);
            bkList = strategy.ElberfelderStrongsVerses;
        }

        var elb1871Path = "../../temp/SeedData/Elb/elberfelder1871.txt";
        if (_fileService.FileExists(elb1871Path))
        {
            var strategy = scope.ServiceProvider.GetRequiredService<Elberfelder1871Strategy>();

            await strategy.SaveToDatabase(elb1871Path, token);
            elb1871List = strategy.Elberfelder1871Verses;
        }

        var mergeStrategy = scope.ServiceProvider.GetRequiredService<ElberfelderMergeStrategy>();

        await mergeStrategy.SaveToDatabaseAsync(bkList, elb1871List, token);
    }
}

public sealed class ElberfelderMergeStrategy
{
    private readonly IDbWriter _writer;
    private readonly ILogger<ElberfelderMergeStrategy> _logger;
    private static string nl = Environment.NewLine;

    public ElberfelderMergeStrategy(IDbWriter writer, ILogger<ElberfelderMergeStrategy> logger)
    {
        _writer = writer;
        _logger = logger;
    }

    public async Task SaveToDatabaseAsync(IList<ElbVerse> bkList, IList<Elb1871Verse> elb1871List, CancellationToken token = default)
    {
        var insertSql = """
                        INSERT INTO "unshackled-word"."Elb1871Words" ("BibleBookId", "Chapter", "Verse", "WordInContext", "German", "Strongs", "PositionInVerse")
                        VALUES
                        """;


        foreach (var elb1871Verse in elb1871List)
        {
            var insertRows = new List<string>();

            var bkVerseWords = bkList.FirstOrDefault(bk =>
                bk.BibleBook.Id == elb1871Verse.BibleBookId && bk.ChapterId == elb1871Verse.Chapter &&
                bk.VerseId == elb1871Verse.Verse)?.Words ?? new List<ElbWord>();
            var dynParams = new DynamicParameters();
            var strongsPrefix = elb1871Verse.BibleBookId >= 40 ? "G" : "H";

            if (elb1871Verse.Words.Count == 0)
            {
                continue;
            }

            foreach (var word in elb1871Verse.Words)
            {
                var strongs = bkVerseWords.FirstOrDefault(x => x.Strong.IsNotNullOrWhiteSpace() && x.Word == word.Lemma)?.Strong;
                strongs = strongs.IsNullOrWhiteSpace() ? "NULL" : $"'{strongsPrefix}{strongs}'";
                dynParams.Add($"@InContent{word.Order}", word.InContext);
                dynParams.Add($"@Lemma{word.Order}", word.Lemma);
                insertRows.Add($"({elb1871Verse.BibleBookId}, {elb1871Verse.Chapter}, {elb1871Verse.Verse}, @InContent{word.Order}, @Lemma{word.Order}, {strongs}, {word.Order})");
                // insertRows.Add($"({elb1871Verse.BibleBookId}, {elb1871Verse.Chapter}, {elb1871Verse.Verse}, '{word.InContext}', '{word.Lemma}', {strongs}, {word.Order})");
            }

            var commandText = insertSql + nl + insertRows.JoinStrings($",{nl}") + ";";
            await _writer.WriteAsync(commandText, dynParams);
        }
    }
}
