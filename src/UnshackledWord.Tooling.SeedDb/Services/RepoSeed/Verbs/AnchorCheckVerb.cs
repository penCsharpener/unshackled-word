using UnshackledWord.Infrastructure.Seed;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Seed.Anchoring;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;

namespace UnshackledWord.Tooling.SeedDb.Services.RepoSeed.Verbs;

/// <summary>
/// <c>anchor-check</c>: parses <c>assets/elberfelder1871-theword-export.txt</c> in memory (what a fresh install would get) and aligns it
/// against the German words of the configured database (what an export would be keyed to). Prints the counts per class and the
/// chapters with orphans and writes every orphan to <c>temp/seed-report/anchor-check.csv</c>. Read-only.
/// </summary>
public sealed class AnchorCheckVerb : ISeedVerb
{
    private readonly ElbWordTokenReader _wordReader;
    private readonly IFileService _fileService;
    private readonly DatabaseSeedSettings _settings;
    private readonly ILogger<AnchorCheckVerb> _logger;

    public AnchorCheckVerb(ElbWordTokenReader wordReader, IFileService fileService, IOptions<AppSettings> options, ILogger<AnchorCheckVerb> logger)
    {
        _wordReader = wordReader;
        _fileService = fileService;
        _settings = options.Value.DatabaseSeeding;
        _logger = logger;
    }

    public string Name => "anchor-check";

    public async Task<int> RunAsync(VerbArguments arguments, CancellationToken token)
    {
        var layout = new SeedLayout(_fileService, _settings);
        var lines = await _fileService.ReadAllLinesAsync(layout.BaseTextPath, SeedLayout.Utf8NoBom, token);
        var parsed = Elb1871WordParser.Parse(lines)
            .Where(w => !AnchorToken.IsBareDash(w.WordInContext))
            .Select(w => AnchorToken.ForWord(w.HebRefId, w.PositionInVerse, w.PlainWord))
            .OrderBy(t => t.Ref).ThenBy(t => t.Pos)
            .ToList();

        var databaseTokens = new List<AnchorToken>();

        foreach (var bookId in await _wordReader.ReadBookIdsAsync())
        {
            databaseTokens.AddRange(await _wordReader.ReadBookAsync(bookId));
        }

        _logger.LogInformation("Database words {Database}, freshly parsed words {Parsed} ({Path})", databaseTokens.Count, parsed.Count, layout.BaseTextPath);

        var run = AnchorRun.Align(databaseTokens, parsed);

        _logger.LogInformation("Old = database, new = fresh parse: exact {Exact}, shifted {Shifted}, modified {Modified}, orphaned {Orphaned}, only in new {Inserted}",
            run.Exact, run.Shifted, run.Modified, run.Orphaned, run.InsertedNew);

        foreach (var chapter in run.ChaptersWithOrphans)
        {
            _logger.LogInformation(
                "Chapter {Chapter}: exact {Exact}, shifted {Shifted}, modified {Modified}, orphaned {Orphaned}, only in new {Inserted}{Incomplete}",
                chapter.Chapter, chapter.Exact, chapter.Shifted, chapter.Modified, chapter.Orphaned, chapter.InsertedNew, chapter.Complete ? string.Empty : " (alignment gave up)");
        }

        await WriteReportAsync(layout, run, token);

        return 0;
    }

    private async Task WriteReportAsync(SeedLayout layout, AnchorRun run, CancellationToken token)
    {
        _fileService.CreateDirectoryIfNotExists(layout.ReportRoot);
        var builder = new StringBuilder("Chapter,Class,OldRef,OldPos,OldForm,NewRef,NewPos,NewForm\n");

        foreach (var match in run.Chapters.SelectMany(c => c.Matches).Where(m => m.Class is AnchorClass.Orphaned or AnchorClass.Modified))
        {
            builder.Append(match.Old.Chapter).Append(',').Append(match.Class).Append(',')
                .Append(match.Old.Ref).Append(',').Append(match.Old.Pos).Append(',').Append(match.Old.Text).Append(',')
                .Append(match.New?.Ref.ToString(CultureInfo.InvariantCulture)).Append(',').Append(match.New?.Pos.ToString(CultureInfo.InvariantCulture)).Append(',').Append(match.New?.Text)
                .Append('\n');
        }

        var path = _fileService.Combine(layout.ReportRoot, "anchor-check.csv");
        await _fileService.WriteAllTextAsync(path, builder.ToString(), SeedLayout.Utf8NoBom, token);
        _logger.LogInformation("Orphaned and modified words written to {Path}", path);
    }
}
