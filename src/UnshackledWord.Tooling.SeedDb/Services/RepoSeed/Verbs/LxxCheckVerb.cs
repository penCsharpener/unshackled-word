using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Lxx;
using UnshackledWord.Domain.Models.BibleStructure.References;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Tooling.SeedDb.Services.StepBible;
using UnshackledWord.Tooling.SeedDb.Services.Swete;

namespace UnshackledWord.Tooling.SeedDb.Services.RepoSeed.Verbs;

/// <summary>
/// <c>lxx-check</c> (roadmap 0028.1/0028.2): downloads Swete's text (pinned commit), maps it with <see cref="LxxVersification"/> and the
/// exception list <c>assets/seed/lxx/swete-to-rahlfs.csv</c> onto the Greek column of TVTMS, and checks the result. Needs no database.
/// Restricted to the 39 protocanonical books. Writes <c>temp/lxx/swete-vs-tvtms.csv</c>:
/// <list type="bullet">
/// <item><c>chapter</c>: per Rahlfs chapter (and Daniel text form) the verse numbers Swete yields vs the TVTMS Greek verses, with
/// <c>Status</c> <c>ok</c>, <c>explained (exception)</c>, <c>explained (tvtms-fix)</c>, <c>gap</c> or <c>unexplained</c>.</item>
/// <item><c>rahlfs-not-in-tvtms</c>: a Rahlfs reference from Swete that TVTMS does not know (an empty Swete verse = <c>gap</c>; a subverse
/// from a marker or exception row = <c>explained (exception)</c>, an addition).</item>
/// <item><c>tvtms-not-in-swete</c>: a TVTMS Greek verse without Swete text (<c>gap</c> when a gap row of the exception list names it).</item>
/// <item><c>swete-absent</c>: Swete text the exception list maps to nothing.</item>
/// </list>
/// <c>--show &lt;ref&gt;</c> (repeatable) prints the chain Swete → Rahlfs → LxxRefId → HebRefId for a Swete or Rahlfs reference
/// (<c>Jer.32:1</c>, <c>Ps.9:22</c>, <c>1Ki.2:35.1</c>). Exit code 0 on success, 1 when a file cannot be read or the exception list has errors,
/// 2 when chapters stay unexplained.
/// </summary>
public sealed partial class LxxCheckVerb : IOfflineSeedVerb
{
    public const string ExceptionFile = "seed/lxx/swete-to-rahlfs.csv";

    public const string StatusOk = "ok";
    public const string StatusException = "explained (exception)";
    public const string StatusTvtmsFix = "explained (tvtms-fix)";
    public const string StatusGap = "gap";
    public const string StatusUnexplained = "unexplained";

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

    [GeneratedRegex(@"^(?<b>[^.]+)\.(?<c>[0-9]+):(?<v>[0-9]+)(?:\.(?<s>[0-9]+))?$")]
    private static partial Regex ShowPattern();

    public async Task<int> RunAsync(VerbArguments arguments, CancellationToken token)
    {
        var sweteFiles = await _sweteDownloader.DownloadFileAsync(token);
        var tvtmsPath = await EnsureTvtmsAsync(token);
        if (tvtmsPath is null)
        {
            _logger.LogError("TVTMS file not found and could not be downloaded.");
            return 1;
        }

        var exceptionPath = _fileService.Combine(_options.SolutionAssetsPath, ExceptionFile);
        if (!_fileService.FileExists(exceptionPath))
        {
            _logger.LogError("Exception list {Path} not found.", exceptionPath);
            return 1;
        }

        var verses = SweteParser.Parse(await File.ReadAllLinesAsync(sweteFiles[0], token), await File.ReadAllLinesAsync(sweteFiles[1], token));
        var table = TvtmsGreekParser.Parse(await File.ReadAllLinesAsync(tvtmsPath, token));
        var parseErrors = new List<string>();
        var exceptions = SweteRahlfsExceptionList.Parse(await File.ReadAllLinesAsync(exceptionPath, token), parseErrors);
        _logger.LogInformation("Swete: {Verses} verses, {Words} words, {Books} book codes. TVTMS Greek column: {Rows} alignment rows, {Unparsed} unreadable cells. Exception list: {Exceptions} rows.",
            verses.Count, verses.Sum(v => v.Words.Count), verses.Select(v => v.BookCode).Distinct().Count(), table.Rows.Count, table.UnparsedCells.Count,
            exceptions.Count);
        foreach (var cell in table.UnparsedCells.Take(20))
        {
            _logger.LogWarning("TVTMS cell not read: {Cell}", cell);
        }

        LogBookTotals(verses);

        var result = LxxVersification.Map(verses, exceptions, table);
        foreach (var error in parseErrors.Concat(result.Errors))
        {
            _logger.LogError("Exception list: {Error}", error);
        }

        foreach (var reference in arguments.GetAll("--show"))
        {
            Show(reference, result);
        }

        var report = BuildReport(result);
        var outputPath = _fileService.Combine(_options.SolutionTempPath, "lxx", "swete-vs-tvtms.csv");
        _fileService.CreateDirectoryIfNotExists(_fileService.Combine(_options.SolutionTempPath, "lxx"));
        await _fileService.WriteAllTextAsync(outputPath, report.Csv, new UTF8Encoding(false), token);
        var mappingPath = _fileService.Combine(_options.SolutionTempPath, "lxx", "swete-to-rahlfs-mapping.csv");
        await _fileService.WriteAllTextAsync(mappingPath, MappingCsv(result), new UTF8Encoding(false), token);

        _logger.LogInformation("Mapped {Units} Swete units: {Kinds}.", result.Verses.Count,
            string.Join(", ", result.Verses.GroupBy(v => v.MapKind).OrderBy(g => g.Key).Select(g => $"{g.Key} {g.Count()}")));
        _logger.LogInformation("Wrote {Path}. Chapters: {Chapters}. References: {References}.", outputPath,
            Counts(report.ChapterStatus), Counts(report.ReferenceStatus));

        if (parseErrors.Count > 0 || result.Errors.Count > 0)
        {
            return 1;
        }

        return report.ChapterStatus.GetValueOrDefault(StatusUnexplained) > 0 ? 2 : 0;
    }

    private static string Counts(Dictionary<string, int> counts) =>
        string.Join(", ", new[] { StatusOk, StatusException, StatusTvtmsFix, StatusGap, StatusUnexplained }.Select(s => $"{s} {counts.GetValueOrDefault(s)}"));

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

    // ---- --show --------------------------------------------------------------------------------------------------------------------

    private void Show(string reference, LxxVersificationResult result)
    {
        var match = ShowPattern().Match(reference.Trim());
        var book = match.Success ? ResolveBook(match.Groups["b"].Value) : null;
        if (book is null)
        {
            _logger.LogWarning("--show {Reference}: expected Book.C:V or Book.C:V.S with a protocanonical book.", reference);
            return;
        }

        var chapter = int.Parse(match.Groups["c"].Value, CultureInfo.InvariantCulture);
        var verse = int.Parse(match.Groups["v"].Value, CultureInfo.InvariantCulture);
        int? sub = match.Groups["s"].Success ? int.Parse(match.Groups["s"].Value, CultureInfo.InvariantCulture) : null;

        var bySwete = result.Verses.Where(m => sub is null && LxxBookCodes.TvtmsFromSwete(m.SweteBook) == book && m.SweteChapter == chapter && m.SweteVerse == verse);
        var byRahlfs = result.Verses.Where(m => m.RahlfsBook == book && m.Chapter == chapter && m.Verse == verse && (sub is null || m.Subverse == sub));
        var hits = bySwete.Concat(byRahlfs).Distinct().OrderBy(m => m.Order).ToList();
        var gaps = result.Gaps.Where(g => g.Rahlfs.Book == book && g.Rahlfs.Chapter == chapter.ToString(CultureInfo.InvariantCulture) && g.Rahlfs.Verse == verse
            && (sub is null || g.Rahlfs.Sub == sub)).ToList();

        var text = new StringBuilder($"--show {reference}:");
        if (hits.Count == 0 && gaps.Count == 0)
        {
            text.Append(" no Swete or Rahlfs verse.");
        }

        foreach (var m in hits)
        {
            text.Append("\n  ").Append(Describe(m));
        }

        foreach (var gap in gaps)
        {
            text.Append("\n  Rahlfs ").Append(gap.Rahlfs).Append(": no Swete text (gap row, line ").Append(gap.Line).Append(": ").Append(gap.Note).Append(')');
        }

        _logger.LogInformation("{Chain}", text.ToString());
    }

    private static string? ResolveBook(string code)
    {
        if (LxxBookCodes.Tvtms.Contains(code))
        {
            return code;
        }

        if (LxxBookCodes.TvtmsFromSwete(code) is { } fromSwete)
        {
            return fromSwete;
        }

        return BookAliasCatalog.Resolve(code, AliasProfile.Legacy) is { BookId: { } id and >= 1 and <= 39 } ? LxxBookCodes.Tvtms[id - 1] : null;
    }

    private static string Describe(LxxVerseMapping m)
    {
        var words = m.Words.Where(w => !LxxVersification.IsMarkerToken(w.Text)).Select(w => w.Text).ToList();
        var preview = string.Join(' ', words.Take(6)) + (words.Count > 6 ? " …" : "");
        var rahlfs = m.RahlfsRefId == 0 ? "(none)" : $"{m.RahlfsRef}{(m.TextForm.Length > 0 ? " " + m.TextForm : "")} [{m.RahlfsRefId}]";
        var after = m.AfterHebRefId is { } a ? $", after Heb {LxxVersification.FormatRefId(a)}" : "";
        var origin = m.Origin + (m.ExceptionLine is { } line ? $" line {line}" : "") + (m.TvtmsFix ? ", tvtms-fix" : "");

        return $"Swete {m.SweteRef} -> Rahlfs {rahlfs} -> LxxRefId {LxxVersification.FormatRefId(m.LxxRefId)} ({Id(m.LxxRefId)})"
            + $" -> HebRefId {LxxVersification.FormatRefId(m.HebRefId)} ({Id(m.HebRefId)}) | {m.MapKind}{after} | {origin} | {m.Words.Count} words: {preview}";
    }

    private static string Id(int? id) => id?.ToString(CultureInfo.InvariantCulture) ?? "NULL";

    // ---- report --------------------------------------------------------------------------------------------------------------------

    private sealed record Report(string Csv, Dictionary<string, int> ChapterStatus, Dictionary<string, int> ReferenceStatus);

    private static Report BuildReport(LxxVersificationResult result)
    {
        var csv = new StringBuilder("Kind,Book,TextForm,Chapter,Verse,SweteVerses,TvtmsGreekVerses,SweteMaxVerse,TvtmsMaxVerse,Status,Detail\n");
        var chapterStatus = new Dictionary<string, int>();
        var referenceStatus = new Dictionary<string, int>();
        var greek = LxxVersification.GreekVerses(result.Table);
        var index = new LxxVersification.TvtmsIndex(result.Table);

        var mapped = result.Verses.Where(m => m.RahlfsRefId != 0).ToList();
        var forms = mapped.Select(m => (m.RahlfsBook, m.TextForm)).Distinct().ToList();
        var chapters = mapped.Select(m => (m.BibleBookId, m.RahlfsBook, m.TextForm, m.Chapter)).ToHashSet();
        foreach (var ((book, chapter), _) in greek)
        {
            if (LxxBookCodes.BookIdFromTvtms(book) is { } id && int.TryParse(chapter, out var number))
            {
                foreach (var (_, form) in forms.Where(f => f.RahlfsBook == book))
                {
                    chapters.Add((id, book, form, number));
                }
            }
        }

        foreach (var (bookId, book, form, chapter) in chapters.OrderBy(c => c.BibleBookId).ThenBy(c => c.TextForm).ThenBy(c => c.Chapter))
        {
            var chapterText = chapter.ToString(CultureInfo.InvariantCulture);
            var inChapter = mapped.Where(m => m.RahlfsBook == book && m.TextForm == form && m.Chapter == chapter).ToList();
            var numbers = inChapter.Select(m => m.Verse).Where(v => v > 0).Distinct().Order().ToList();
            var tvtms = greek.GetValueOrDefault((book, chapterText))?.Verses.Where(v => v > 0).ToList() ?? [];
            var onlySwete = numbers.Except(tvtms).Order().ToList();
            var onlyTvtms = tvtms.Except(numbers).Order().ToList();

            var unexplained = new List<string>();
            var gaps = new List<string>();
            foreach (var verse in onlySwete)
            {
                if (inChapter.Where(m => m.Verse == verse).All(m => m.IsEmpty))
                {
                    gaps.Add($"{verse} empty in Swete");
                }
                else
                {
                    unexplained.Add($"{verse} only Swete");
                }
            }

            foreach (var verse in onlyTvtms)
            {
                if (IsGapRow(result, book, form, chapterText, verse))
                {
                    gaps.Add($"{verse} gap row");
                }
                else
                {
                    unexplained.Add($"{verse} only TVTMS");
                }
            }

            var fixTouched = TvtmsGreekFixes.TouchedChapters.Contains((book, chapterText));
            var exceptionTouched = inChapter.Any(m => m.Origin == LxxMapOrigins.Exception)
                || result.Gaps.Any(g => g.Rahlfs.Book == book && g.Rahlfs.Chapter == chapterText);
            var status = unexplained.Count > 0 ? StatusUnexplained
                : fixTouched ? StatusTvtmsFix
                : exceptionTouched ? StatusException
                : gaps.Count > 0 ? StatusGap
                : StatusOk;
            chapterStatus[status] = chapterStatus.GetValueOrDefault(status) + 1;
            var detail = string.Join("; ", unexplained.Concat(gaps).DefaultIfEmpty(""));
            AppendRow(csv, "chapter", book, form, chapterText, "", numbers.Count, tvtms.Count, numbers.Count > 0 ? numbers.Max() : 0,
                tvtms.Count > 0 ? tvtms.Max() : 0, status, detail);

            foreach (var verse in onlyTvtms)
            {
                var gap = result.Gaps.FirstOrDefault(g => g.Rahlfs.Book == book && g.Rahlfs.Chapter == chapterText && g.Rahlfs.Verse == verse);
                var refStatus = gap is null ? StatusUnexplained : StatusGap;
                referenceStatus[refStatus] = referenceStatus.GetValueOrDefault(refStatus) + 1;
                AppendRow(csv, "tvtms-not-in-swete", book, form, chapterText, verse.ToString(CultureInfo.InvariantCulture), 0, 0, 0, 0, refStatus,
                    gap is null ? "" : $"line {gap.Line}: {gap.Note}");
            }
        }

        foreach (var m in mapped)
        {
            var target = new TvtmsRef(m.RahlfsBook, m.Chapter.ToString(CultureInfo.InvariantCulture), m.Verse, m.Subverse);
            if (index.Knows(target))
            {
                continue;
            }

            var (status, detail) = m.IsEmpty ? (StatusGap, "empty in Swete")
                : m.Subverse > 0 && m.MapKind == LxxMapKinds.Addition ? (StatusException, $"{m.Origin} subverse, addition after Heb {LxxVersification.FormatRefId(m.AfterHebRefId)}")
                : (StatusUnexplained, $"{m.MapKind}, from Swete {m.SweteRef}");
            referenceStatus[status] = referenceStatus.GetValueOrDefault(status) + 1;
            AppendRow(csv, "rahlfs-not-in-tvtms", m.RahlfsBook, m.TextForm, m.Chapter.ToString(CultureInfo.InvariantCulture),
                m.Subverse > 0 ? $"{m.Verse}.{m.Subverse}" : m.Verse.ToString(CultureInfo.InvariantCulture), 0, 0, 0, 0, status, $"{detail} (Swete {m.SweteRef})");
        }

        foreach (var m in result.Verses.Where(m => m.RahlfsRefId == 0))
        {
            referenceStatus[StatusGap] = referenceStatus.GetValueOrDefault(StatusGap) + 1;
            AppendRow(csv, "swete-absent", m.RahlfsBook, m.TextForm, m.SweteChapter.ToString(CultureInfo.InvariantCulture),
                m.SweteVerse.ToString(CultureInfo.InvariantCulture), 0, 0, 0, 0, StatusGap, $"Swete {m.SweteRef}: {m.Note}");
        }

        return new Report(csv.ToString(), chapterStatus, referenceStatus);
    }

    /// <summary>Every mapped unit, one line each (review aid, <c>temp/lxx/swete-to-rahlfs-mapping.csv</c>).</summary>
    private static string MappingCsv(LxxVersificationResult result)
    {
        var csv = new StringBuilder("Order,SweteRef,RahlfsRef,TextForm,RahlfsRefId,LxxRef,LxxRefId,HebRef,HebRefId,AfterHebRef,MapKind,Origin,Line,Words,FirstWords\n");
        foreach (var m in result.Verses)
        {
            var words = m.Words.Where(w => !LxxVersification.IsMarkerToken(w.Text)).Select(w => w.Text).Take(5);
            csv.Append(m.Order).Append(',').Append(m.SweteRef).Append(',').Append(m.RahlfsRefId == 0 ? "" : m.RahlfsRef).Append(',').Append(m.TextForm)
                .Append(',').Append(m.RahlfsRefId).Append(',').Append(LxxVersification.FormatRefId(m.LxxRefId)).Append(',').Append(Id(m.LxxRefId))
                .Append(',').Append(LxxVersification.FormatRefId(m.HebRefId)).Append(',').Append(Id(m.HebRefId)).Append(',')
                .Append(LxxVersification.FormatRefId(m.AfterHebRefId)).Append(',').Append(m.MapKind).Append(',').Append(m.Origin).Append(m.TvtmsFix ? "+fix" : "")
                .Append(',').Append(m.ExceptionLine).Append(',').Append(m.IsEmpty ? 0 : m.Words.Count).Append(",\"")
                .Append(string.Join(' ', words).Replace("\"", "\"\"")).Append("\"\n");
        }

        return csv.ToString();
    }

    private static bool IsGapRow(LxxVersificationResult result, string book, string form, string chapter, int verse) =>
        result.Gaps.Any(g => g.Rahlfs.Book == book && g.Rahlfs.Chapter == chapter && g.Rahlfs.Verse == verse && (g.TextForm.Length == 0 || g.TextForm == form));

    private static void AppendRow(StringBuilder csv, string kind, string book, string form, string chapter, string verse, int sweteCount, int tvtmsCount,
        int sweteMax, int tvtmsMax, string status, string detail)
    {
        csv.Append(kind).Append(',').Append(book).Append(',').Append(form).Append(',').Append(chapter).Append(',').Append(verse).Append(',')
            .Append(sweteCount).Append(',').Append(tvtmsCount).Append(',').Append(sweteMax).Append(',').Append(tvtmsMax).Append(',')
            .Append('"').Append(status).Append("\",").Append('"').Append(detail.Replace("\"", "\"\"")).Append("\"\n");
    }
}
