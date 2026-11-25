using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Lxx;
using UnshackledWord.Domain.Models.BibleStructure.References;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Tooling.SeedDb.Services.StepBible;
using UnshackledWord.Tooling.SeedDb.Services.Swete;

namespace UnshackledWord.Tooling.SeedDb.Services.RepoSeed.Verbs;

/// <summary>
/// <c>lxx-check</c> (roadmap 0028.1): downloads Swete's text (pinned commit) and compares its verse structure with the Greek column of TVTMS.
/// Needs no database. Restricted to the 39 protocanonical books (Swete's Wisdom, Sirach, Maccabees ... are skipped). Writes
/// <c>temp/lxx/swete-vs-tvtms.csv</c> with one row per chapter (Swete verse count vs TVTMS Greek verse count, <c>Status</c> = <c>ok</c> or
/// <c>differs</c>, <c>Detail</c> = verses only one side has) and one row per Swete verse that TVTMS's Greek tradition does not have
/// (<c>Kind</c> = <c>swete-missing-in-tvtms</c>). Logs words and verses per book and the Swete book codes. Exit code 0 on success, 1 when a
/// needed file cannot be read.
/// </summary>
public sealed class LxxCheckVerb : IOfflineSeedVerb
{
    private readonly SweteDownloader _sweteDownloader;
    private readonly StepGithubDownloader _stepDownloader;
    private readonly IFileService _fileService;
    private readonly DatabaseSeedSettings _options;
    private readonly ILogger<LxxCheckVerb> _logger;

    public LxxCheckVerb(SweteDownloader sweteDownloader, StepGithubDownloader stepDownloader, IFileService fileService,
        IOptions<AppSettings> options, ILogger<LxxCheckVerb> logger)
    {
        _sweteDownloader = sweteDownloader;
        _stepDownloader = stepDownloader;
        _fileService = fileService;
        _options = options.Value.DatabaseSeeding;
        _logger = logger;
    }

    public string Name => "lxx-check";

    public async Task<int> RunAsync(VerbArguments arguments, CancellationToken token)
    {
        var sweteFiles = await _sweteDownloader.DownloadFileAsync(token);
        var tvtmsPath = await EnsureTvtmsAsync(token);
        if (tvtmsPath is null)
        {
            _logger.LogError("TVTMS file not found and could not be downloaded.");
            return 1;
        }

        var verses = SweteParser.Parse(await File.ReadAllLinesAsync(sweteFiles[0], token), await File.ReadAllLinesAsync(sweteFiles[1], token));
        var table = TvtmsGreekParser.Parse(await File.ReadAllLinesAsync(tvtmsPath, token));
        _logger.LogInformation("Swete: {Verses} verses, {Words} words, {Books} book codes. TVTMS Greek column: {Rows} alignment rows, {Unparsed} unreadable cells.",
            verses.Count, verses.Sum(v => v.Words.Count), verses.Select(v => v.BookCode).Distinct().Count(), table.Rows.Count, table.UnparsedCells.Count);
        foreach (var cell in table.UnparsedCells.Take(20))
        {
            _logger.LogWarning("TVTMS cell not read: {Cell}", cell);
        }

        LogBookTotals(verses);

        var greek = table.GreekVerses(code => LxxBookCodes.BookIdFromTvtms(code) is { } id ? VerseTables.EnglishCounts(id) : null);
        var (csv, differing, missing) = BuildReport(verses, greek);

        var outputPath = _fileService.Combine(_options.SolutionTempPath, "lxx", "swete-vs-tvtms.csv");
        _fileService.CreateDirectoryIfNotExists(_fileService.Combine(_options.SolutionTempPath, "lxx"));
        await _fileService.WriteAllTextAsync(outputPath, csv, new UTF8Encoding(false), token);
        _logger.LogInformation("Wrote {Path}: {Differing} differing chapters, {Missing} Swete verses missing in the TVTMS Greek tradition.",
            outputPath, differing, missing);

        return 0;
    }

    private async Task<string?> EnsureTvtmsAsync(CancellationToken token)
    {
        var fileName = _fileService.GetFileName(_options.StepBibleData.VersificationFile).Replace("%20", " ");
        var path = _fileService.Combine(_options.SolutionTempPath, "SeedData", "Step", fileName);
        if (!_fileService.FileExists(path))
        {
            _logger.LogInformation("TVTMS not cached; running the STEP downloader.");
            await _stepDownloader.DownloadFileAsync(token);
        }

        return _fileService.FileExists(path) ? path : null;
    }

    private void LogBookTotals(IReadOnlyList<SweteVerse> verses)
    {
        foreach (var book in verses.GroupBy(v => v.BookCode))
        {
            var tvtms = LxxBookCodes.TvtmsFromSwete(book.Key);
            _logger.LogInformation("Swete {Code,-3} {Protocanonical,-14} {Verses,5} verses {Words,7} words", book.Key,
                tvtms is null ? "(not checked)" : $"-> {tvtms}", book.Count(), book.Sum(v => v.Words.Count));
        }
    }

    private static (string Csv, int Differing, int Missing) BuildReport(IReadOnlyList<SweteVerse> verses,
        IReadOnlyDictionary<(string Book, string Chapter), TvtmsGreekChapter> greek)
    {
        var csv = new StringBuilder("Kind,Book,Chapter,Verse,SweteVerses,TvtmsGreekVerses,SweteMaxVerse,TvtmsMaxVerse,Status,Detail\n");
        var differing = 0;
        var missing = 0;
        var sweteChapters = new HashSet<(string, string)>();

        foreach (var chapter in verses.Where(v => LxxBookCodes.TvtmsFromSwete(v.BookCode) is not null)
                     .GroupBy(v => (v.BookCode, v.Chapter)))
        {
            var tvtmsBook = LxxBookCodes.TvtmsFromSwete(chapter.Key.BookCode)!;
            var numbers = chapter.Select(v => v.Verse).ToList();
            sweteChapters.Add((chapter.Key.BookCode, chapter.Key.Chapter.ToString(CultureInfo.InvariantCulture)));
            var tvtms = greek.GetValueOrDefault((tvtmsBook, chapter.Key.Chapter.ToString(CultureInfo.InvariantCulture)));
            var tvtmsVerses = tvtms?.Verses.Where(v => v > 0).ToList() ?? [];
            var onlySwete = numbers.Except(tvtmsVerses).Order().ToList();
            var onlyTvtms = tvtmsVerses.Except(numbers).Order().ToList();

            var same = numbers.Count == tvtmsVerses.Count && onlySwete.Count == 0 && onlyTvtms.Count == 0;
            differing += same ? 0 : 1;
            var detail = same ? "" : $"only Swete: {Compress(onlySwete)}; only TVTMS: {Compress(onlyTvtms)}";
            AppendRow(csv, "chapter", chapter.Key.BookCode, chapter.Key.Chapter.ToString(CultureInfo.InvariantCulture), "", numbers.Count,
                tvtmsVerses.Count, numbers.Max(), tvtmsVerses.Count > 0 ? tvtmsVerses.Max() : 0, same ? "ok" : "differs", detail);

            foreach (var verse in onlySwete)
            {
                missing++;
                AppendRow(csv, "swete-missing-in-tvtms", chapter.Key.BookCode, chapter.Key.Chapter.ToString(CultureInfo.InvariantCulture),
                    verse.ToString(CultureInfo.InvariantCulture), 0, 0, 0, 0, "missing", "");
            }
        }

        // Greek chapters of TVTMS that Swete has no chapter for (Esther additions with letter chapters, chapters Swete numbers differently).
        foreach (var ((tvtmsBook, chapter), tvtms) in greek.OrderBy(g => LxxBookCodes.BookIdFromTvtms(g.Key.Book)).ThenBy(g => g.Key.Chapter.PadLeft(4, '0')))
        {
            var sweteCodes = verses.Select(v => v.BookCode).Distinct().Where(c => LxxBookCodes.TvtmsFromSwete(c) == tvtmsBook).ToList();
            foreach (var code in sweteCodes.Where(c => !sweteChapters.Contains((c, chapter))))
            {
                var tvtmsVerses = tvtms.Verses.Where(v => v > 0).ToList();
                differing++;
                AppendRow(csv, "chapter", code, chapter, "", 0, tvtmsVerses.Count, 0, tvtmsVerses.Count > 0 ? tvtmsVerses.Max() : 0, "differs", "chapter only in TVTMS");
            }
        }

        return (csv.ToString(), differing, missing);
    }

    private static void AppendRow(StringBuilder csv, string kind, string book, string chapter, string verse, int sweteCount, int tvtmsCount,
        int sweteMax, int tvtmsMax, string status, string detail)
    {
        csv.Append(kind).Append(',').Append(book).Append(',').Append(chapter).Append(',').Append(verse).Append(',')
            .Append(sweteCount).Append(',').Append(tvtmsCount).Append(',').Append(sweteMax).Append(',').Append(tvtmsMax).Append(',')
            .Append(status).Append(',').Append('"').Append(detail.Replace("\"", "\"\"")).Append("\"\n");
    }

    private static string Compress(List<int> numbers)
    {
        if (numbers.Count == 0)
        {
            return "-";
        }

        var parts = new List<string>();
        for (var i = 0; i < numbers.Count;)
        {
            var j = i;
            while (j + 1 < numbers.Count && numbers[j + 1] == numbers[j] + 1)
            {
                j++;
            }

            parts.Add(j > i ? $"{numbers[i]}-{numbers[j]}" : numbers[i].ToString(CultureInfo.InvariantCulture));
            i = j + 1;
        }

        return string.Join(' ', parts);
    }
}
