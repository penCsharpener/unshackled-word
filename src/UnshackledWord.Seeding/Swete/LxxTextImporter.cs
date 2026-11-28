using System.Diagnostics;
using UnshackledWord.Application.Features.Lxx;
using UnshackledWord.Seeding.Abstractions;

namespace UnshackledWord.Seeding.Swete;

/// <summary>
/// SeedDb pipeline step of roadmap 0028.3: fills <c>LxxVerses</c>/<c>LxxWords</c> with Swete's text in Rahlfs numbering, the 39 protocanonical
/// books (Daniel as <c>og</c> and <c>th</c>). Runs after the STEP import, whose download holds TVTMS. Only while <c>LxxVerses</c> is empty,
/// like the STEP import gate: a filled table is never touched again, so later tags (0028.5) that anchor on
/// <c>(RahlfsRefId, TextForm, Position, SrcHash)</c> stay put. Empty Swete verses are skipped; the word filter is <see cref="LxxWordFilter"/>.
/// One transaction. An exception list with errors stops the import (logged), it does not stop the seed.
/// </summary>
public sealed class LxxTextImporter : IRunner
{
    private readonly LxxSourceLoader _loader;
    private readonly ILxxTextRepository _repository;
    private readonly ILogger<LxxTextImporter> _logger;

    public LxxTextImporter(LxxSourceLoader loader, ILxxTextRepository repository, ILogger<LxxTextImporter> logger)
    {
        _loader = loader;
        _repository = repository;
        _logger = logger;
    }

    public async Task Run(CancellationToken token = default)
    {
        if (!await _repository.IsEmptyAsync(token))
        {
            _logger.LogInformation("LXX text up to date, no import");
            return;
        }

        var watch = Stopwatch.StartNew();
        var sources = await _loader.LoadAsync(token);
        if (sources is null)
        {
            return;
        }

        var errors = sources.ParseErrors.Concat(sources.Result.Errors).ToList();
        if (errors.Count > 0)
        {
            foreach (var error in errors)
            {
                _logger.LogError("LXX exception list: {Error}", error);
            }

            _logger.LogError("LXX text not imported: the exception list has {Count} errors.", errors.Count);
            return;
        }

        var text = LxxTextBuilder.Build(sources.Result.Verses);
        var inserted = await _repository.InsertAsync(text.Verses, token);
        if (inserted == 0)
        {
            _logger.LogInformation("LXX text up to date, no import");
            return;
        }

        foreach (var book in text.Verses.GroupBy(v => (v.Verse.BibleBookId, v.Verse.TextForm)).OrderBy(g => g.Key.BibleBookId).ThenBy(g => g.Key.TextForm))
        {
            _logger.LogInformation("LXX {Book,-3} {TextForm,-2} {Verses,5} verses {Words,7} words {Additions,4} additions {Absent,2} absent",
                LxxBookCodes.Tvtms[book.Key.BibleBookId - 1], book.Key.TextForm, book.Count(), book.Sum(v => v.Words.Count),
                book.Count(v => v.Verse.MapKind == LxxMapKinds.Addition), book.Count(v => v.Verse.MapKind == LxxMapKinds.Absent));
        }

        _logger.LogInformation(
            "LXX text imported in {Seconds:0.0} s: {Verses} verses ({Kinds}), {Words} words; skipped {Empty} empty Swete verses and {NoWords} units without words; dropped tokens: {Dropped}.",
            watch.Elapsed.TotalSeconds, inserted,
            string.Join(", ", text.Verses.GroupBy(v => v.Verse.MapKind).OrderBy(g => g.Key).Select(g => $"{g.Key} {g.Count()}")),
            text.WordCount, text.SkippedEmpty, text.SkippedNoWords,
            string.Join(", ", text.Dropped.OrderBy(d => d.Key).Select(d => $"{d.Key} {d.Value}")));
    }
}
