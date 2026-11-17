using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CsvHelper;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.Dbo;
using UnshackledWord.Domain.Models.Settings;

namespace UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;

public partial class HyphenTypoDetectionService
{
    private readonly IDbReader _reader;
    private readonly IDbWriter _writer;
    private readonly IFileService _fileService;
    private readonly ILogger<HyphenTypoDetectionService> _logger;
    private readonly DatabaseSeedSettings _options;

    private readonly HashSet<string> _germanWords =
    [
        "Dank-Friedensopfer",
        "Dank-Friedensopfers",
        "Morgen-Brandopfer",
        "Wanderer-Herberge",
        "weiß-rötliches",
        "weiß-rötlicher",
        "weiß-rötlich",
        "Nicht-Götter",
        "Ja-ja",
        "Nein-nein",
    ];

    private readonly HashSet<string> _mustSplit =
    [
        "Nophach-Feuer",
        "Beth-Horon-da",
        "Worte-Silber",
        "Storch-Cypressen",
        "Jerusalem-Berge",
        "Feuers-Wolkenbruch",
        "Noph-Feinde",
        "Mene-Gott",
        "Heerscharen-Jahwe",
    ];

    public HyphenTypoDetectionService(IDbReader reader, IDbWriter writer, IFileService fileService, IOptions<AppSettings> options, ILogger<HyphenTypoDetectionService> logger)
    {
        _reader = reader;
        _writer = writer;
        _fileService = fileService;
        _logger = logger;
        _options = options.Value.DatabaseSeeding;
    }

    public async Task<List<HyphenVerseDto>> GetHyphenWordsAsync(CancellationToken token = default)
    {
        var verses = await GetVersesAsync(token);
        var verseFindings = new List<HyphenVerseDto>();

        foreach (var verse in verses)
        {
            if (verse.VerseText.Contains('-'))
            {
                var hyphenVerse = new HyphenVerseDto();
                hyphenVerse.HebRefId = verse.HebRefId;
                hyphenVerse.OriginalVerse = verse.VerseText;
                hyphenVerse.CorrectedVerse = verse.VerseText;
                hyphenVerse.Id = verse.Id;

                var words = verse.VerseText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var position = 1;

                foreach (var word in words)
                {
                    if (word.Contains('-') && word.Length > 1)
                    {
                        var finding = new HyphenWordDto
                        {
                            PositionInVerse = position,
                            HyphenWord = word,
                            HyphenPlainWord = word.RemovePunctuation(),
                            CorrectedWord = word,
                        };

                        finding.IsPlaceName = ValidHyphenWords.NonPlaceNames.Contains(finding.HyphenPlainWord);
                        finding.IsMultiHyphen = finding.HyphenPlainWord.IndexOf('-') != finding.HyphenPlainWord.LastIndexOf('-');
                        finding.NeedsSplitting = _mustSplit.Contains(finding.HyphenPlainWord);

                        if (!finding.IsPlaceName && !finding.IsMultiHyphen && !finding.HyphenWord.Contains("Beth-Horon-da"))
                        {
                            if (word.Contains("-:"))
                            {
                                finding.CorrectedWord = word.Replace("-:", ": ");
                            }
                            else if (finding.HyphenPlainWord.Contains('-'))
                            {
                                if (!_germanWords.Contains(finding.HyphenPlainWord))
                                {
                                    finding.CorrectedWord = word.Replace("-", " - ");
                                }
                            }
                        }

                        if (finding.HyphenWord.Contains("Beth-Horon-da"))
                        {
                            finding.CorrectedWord = word.Replace("Beth-Horon-da", "Beth-Horon - da");
                        }

                        hyphenVerse.CorrectedVerse = hyphenVerse.CorrectedVerse.Replace(word, finding.CorrectedWord);

                        hyphenVerse.HyphenFindings.Add(finding);
                    }

                    position++;
                }

                verseFindings.Add(hyphenVerse);
            }
        }

        verseFindings = verseFindings.Where(x => x.CorrectedVerse != x.OriginalVerse).OrderBy(x => x.HebRefId).ToList();

        var csvPath = _fileService.Combine(_options.SolutionTempPath, $"HyphenTypoFindings.csv");

        if (_fileService.FileExists(csvPath))
        {
            _fileService.DeleteFile(csvPath);
        }

        var hashSetNonPlaceNames = new StringBuilder();
        await using var writer = new StreamWriter(csvPath);
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        var wordFindings = verseFindings.OrderBy(x => x.HebRefId).SelectMany(x => x.HyphenFindings).Where(x => x.HyphenPlainWord.Contains('-')
                                                 && !ValidHyphenWords.NonPlaceNames.Contains(x.HyphenPlainWord)
                                                 && !_germanWords.Contains(x.HyphenPlainWord))
            .DistinctBy(x => x.HyphenPlainWord)
            .ToList();

        foreach (var f in wordFindings)
        {
            if (f.IsPlaceName || f.IsMultiHyphen)
            {
                hashSetNonPlaceNames.AppendLine($"\"{f.HyphenPlainWord}\",");
            }
        }

        var allVersesWithFix = "\"HebRefId\"  IN (" + verseFindings
            .Select(x => x.HebRefId)
            .Distinct()
            .JoinStrings(",") + ")";

        await csv.WriteRecordsAsync(wordFindings, token);

        return verseFindings;
    }

    private async Task<List<Elb1871VersesDbo>> GetVersesAsync(CancellationToken token = default)
    {
        var sql = """
                  SELECT ev."Id", ev."HebRefId", ev."VerseText"
                  FROM "unshackled-word"."Elb1871Verses" ev
                  WHERE ev."VerseText" LIKE '%-%'
                  """;

        return (await _reader.ReadAsListAsync<Elb1871VersesDbo>(sql)).ToList();
    }

    public async Task FixHyphenWordsAsync(List<HyphenVerseDto> findings, CancellationToken token = default)
    {
        var verses = await GetVersesAsync(token);
        var capacity = verses.Count;
        var parameters = new { Id = new List<int>(capacity), VerseText = new List<string>(capacity) };

        foreach (var verse in findings)
        {
            parameters.Id.Add(verse.Id);
            parameters.VerseText.Add(verse.CorrectedVerse);
        }

        var insertSql = """
                        BEGIN;

                        UPDATE "unshackled-word"."Elb1871Verses" ev
                        SET "VerseText" = t."VerseText"
                        FROM UNNEST(@Id,@VerseText)
                            AS t("Id","VerseText")
                        WHERE ev."Id" = t."Id";

                        COMMIT;
                        """;

        await _writer.WriteAsync(insertSql, parameters);
    }

    [GeneratedRegex(@"([A-Z]{1}[a-z]+?)(-)([A-Z]{1}[a-z]+?)")]
    public partial Regex IsPlaceName();
}

public sealed class HyphenVerseDto
{
    public int Id { get; set; }
    public int HebRefId { get; set; }
    public string OriginalVerse { get; set; } = default!;
    public string CorrectedVerse { get; set; } = default!;
    public List<HyphenWordDto> HyphenFindings { get; set; } = [];
}

public class HyphenWordDto
{
    public int PositionInVerse { get; set; }
    public string HyphenWord { get; set; } = default!;
    public string HyphenPlainWord { get; set; } = default!;
    public string CorrectedWord { get; set; } = default!;
    public bool IsPlaceName { get; set; }
    public bool IsMultiHyphen { get; set; }
    public bool NeedsSplitting { get; set; }
}
