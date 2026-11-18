using UnshackledWord.Domain.WebApi.Maintainer;
using Microsoft.Extensions.Logging;
using UnshackledWord.Infrastructure.Seed;
using UnshackledWord.Infrastructure.Seed.Mappings;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Seed.Anchoring;
using UnshackledWord.Application.Features.Seed.Mappings;
using UnshackledWord.Application.Features.Seed.Morphology;
using UnshackledWord.Application.Features.VerseMapping;
using UnshackledWord.Domain.Models.Settings;

namespace UnshackledWord.Infrastructure.Seed.Mappings;

/// <summary>The rows of one book as they will be written, and how the seed lines were placed.</summary>
public sealed class BookPlan
{
    public required int BookId { get; init; }
    public required bool Greek { get; init; }
    public required List<MappingRowPlan> Rows { get; init; }
    public required MappingPlacement Placement { get; init; }
    public required int ChaptersAligned { get; init; }
}

/// <summary>
/// Imports and updates the mapping seed in format 2 (roadmap 0010.9 and 0010.10). German words are found through the morphology seed's
/// token manifest and <see cref="GermanWordResolver"/>, STEP words through <see cref="SourceWordResolver"/>; whatever cannot be placed is
/// listed in <c>temp/seed-report</c> and counted in <c>SeedImports</c>, never guessed.
/// </summary>
public sealed class MappingSeedImporter
{
    public const string Kind = "mappings";

    private readonly MappingSeedRepository _repository;
    private readonly MappingSeedSourceReader _sourceReader;
    private readonly SeedManifestStore _manifestStore;
    private readonly IFileService _fileService;
    private readonly DatabaseSeedSettings _settings;
    private readonly ILogger<MappingSeedImporter> _logger;

    public MappingSeedImporter(
        MappingSeedRepository repository,
        MappingSeedSourceReader sourceReader,
        SeedManifestStore manifestStore,
        IFileService fileService,
        IOptions<AppSettings> options,
        ILogger<MappingSeedImporter> logger)
    {
        _repository = repository;
        _sourceReader = sourceReader;
        _manifestStore = manifestStore;
        _fileService = fileService;
        _settings = options.Value.DatabaseSeeding;
        _logger = logger;
    }

    public SeedLayout CreateLayout(string? seedRootOverride = null) => new(_fileService, _settings, seedRootOverride);

    public Task<MappingSeedSource?> LoadSourceAsync(SeedLayout layout, CancellationToken token = default) => _sourceReader.LoadAsync(layout, token);

    // ---- first import ----------------------------------------------------------------------------------------------------------

    /// <summary>Imports a format 2 seed into empty mapping tables: all books in one transaction, then the verse state and the SeedImports row.</summary>
    public async Task<SeedStepReport> ImportAsync(SeedLayout layout, MappingSeedSource source, CancellationToken token = default)
    {
        using var session = await _repository.BeginWriteAsync(token);

        if (await session.CountMappingRowsAsync() > 0)
        {
            _logger.LogInformation("Mapping seed skipped: the mapping tables are not empty");

            return SeedStepReport.Skip(Kind, "import", SeedSkipReason.TablesNotEmpty, source.SeedVersion);
        }

        var morphology = (await _manifestStore.ReadAsync(layout.ManifestPath, token))?.Morphology;
        var counts = new int[5];
        var reportLines = new List<PlacedMapping>();
        var verses = new HashSet<int>();
        var alignedChapters = 0;

        foreach (var file in source.Files)
        {
            var plan = await PlanBookAsync(layout, file, morphology, token);

            await session.InsertAsync(plan.Greek, plan.Rows);
            Add(counts, plan);
            alignedChapters += plan.ChaptersAligned;
            reportLines.AddRange(plan.Placement.Reportable);
            verses.UnionWith(plan.Rows.Select(r => r.HebRefId));
        }

        var hashes = await session.ReadVerseHashesAsync(verses.ToArray());
        await session.UpsertStateAsync(source.SeedVersion, hashes);
        await session.CompleteAsync(new MappingImportRecord(source.SeedVersion, source.ExportedAt, counts[4], counts[0], counts[1], counts[2], counts[3], null, null));

        _logger.LogInformation(
            "Mapping seed imported: {Rows} rows in {Verses} verses (exact {Exact}, shifted {Shifted}, modified {Modified}, orphaned {Orphaned}), {Aligned} chapters needed alignment",
            counts[4], verses.Count, counts[0], counts[1], counts[2], counts[3], alignedChapters);

        var reportFile = $"mappings-{source.SeedVersion[..12]}.csv";
        await WriteReportAsync(reportFile, reportLines, token);

        return new SeedStepReport { Kind = Kind, Action = "import", SeedVersion = source.SeedVersion }
            .WithCount("rows", counts[4]).WithCount("verses", verses.Count)
            .WithCount("exact", counts[0]).WithCount("shifted", counts[1]).WithCount("modified", counts[2]).WithCount("orphaned", counts[3])
            .WithReportFile(reportFile);
    }

    /// <summary>Explicit import (maintainer page): only into empty mapping tables.</summary>
    public async Task<SeedStepReport> ImportFromLayoutAsync(SeedLayout layout, CancellationToken token = default)
    {
        var source = await LoadSourceAsync(layout, token);

        if (source is null || !MappingSeedFormat.IsAnchored(source.Format))
        {
            return SeedStepReport.Skip(Kind, "import", source is null ? SeedSkipReason.NoSeed : SeedSkipReason.UnsupportedFormat);
        }

        return await ImportAsync(layout, source, token);
    }

    // ---- later starts ----------------------------------------------------------------------------------------------------------

    /// <summary>
    /// On a populated database: when the repo's mapping seed is newer than the last import, replaces the verses nobody changed since
    /// (database hash equals the stored one) and keeps and reports the others. Returns without any change when updates are switched off,
    /// when the seed is the one already imported, or when there is no record of a seed import to compare against.
    /// </summary>
    public async Task<SeedStepReport> UpdateAsync(SeedLayout layout, CancellationToken token = default)
    {
        if (!_settings.UpdateMappingSeed)
        {
            _logger.LogInformation("Mapping seed update skipped: AppSettings:DatabaseSeeding:UpdateMappingSeed is false");

            return SeedStepReport.Skip(Kind, "update", SeedSkipReason.UpdateDisabled);
        }

        var source = await _sourceReader.LoadAsync(layout, token);

        if (source is null || !MappingSeedFormat.IsAnchored(source.Format))
        {
            _logger.LogInformation("Mapping seed update skipped: no mapping seed in format {Format} in {Folder}", MappingSeedFormat.Anchored, layout.MappingsRoot);

            return SeedStepReport.Skip(Kind, "update", source is null ? SeedSkipReason.NoSeed : SeedSkipReason.UnsupportedFormat);
        }

        var latest = await _repository.ReadLatestImportAsync();

        if (latest is null)
        {
            _logger.LogInformation("Mapping seed update skipped: the mappings were not imported from a seed (no SeedImports record), so there is no baseline to tell local changes from seed rows");

            return SeedStepReport.Skip(Kind, "update", SeedSkipReason.NoBaseline, source.SeedVersion);
        }

        if (latest.SeedVersion == source.SeedVersion)
        {
            _logger.LogInformation("Mapping seed is up to date (version {Version}), nothing to update", source.SeedVersion[..12]);

            return SeedStepReport.Skip(Kind, "update", SeedSkipReason.UpToDate, source.SeedVersion);
        }

        var state = await _repository.ReadStateAsync();

        if (state.Count == 0)
        {
            _logger.LogWarning("Mapping seed update skipped: SeedVerseState is empty, so no verse can be proven unchanged");

            return SeedStepReport.Skip(Kind, "update", SeedSkipReason.StateEmpty, source.SeedVersion);
        }

        return await UpdateFromAsync(layout, source, latest.SeedVersion, state, token);
    }

    private async Task<SeedStepReport> UpdateFromAsync(SeedLayout layout, MappingSeedSource source, string previousVersion, Dictionary<int, string> state, CancellationToken token)
    {
        var morphology = (await _manifestStore.ReadAsync(layout.ManifestPath, token))?.Morphology;
        var counts = new int[5];
        var reportLines = new List<PlacedMapping>();
        var desired = new Dictionary<int, VerseRows>();

        foreach (var file in source.Files)
        {
            var plan = await PlanBookAsync(layout, file, morphology, token);
            Add(counts, plan);
            reportLines.AddRange(plan.Placement.Reportable);

            foreach (var verse in plan.Rows.GroupBy(r => r.HebRefId))
            {
                desired[verse.Key] = new VerseRows(plan.Greek, verse.ToList());
            }
        }

        var dbHashes = await _repository.ReadAllVerseHashesAsync();
        var outcomes = Decide(desired, state, dbHashes);

        using var session = await _repository.BeginWriteAsync(token);
        var candidates = outcomes.Where(o => o.Value is VerseOutcome.Replaced or VerseOutcome.New or VerseOutcome.Removed).Select(o => o.Key).ToArray();
        await GuardAgainstConcurrentEditsAsync(session, outcomes, candidates, state);

        var toDelete = outcomes.Where(o => o.Value is VerseOutcome.Replaced or VerseOutcome.Removed).Select(o => o.Key).ToArray();
        await session.DeleteVersesAsync(toDelete);

        foreach (var group in outcomes.Where(o => o.Value is VerseOutcome.Replaced or VerseOutcome.New).GroupBy(o => desired[o.Key].Greek))
        {
            await session.InsertAsync(group.Key, group.SelectMany(o => desired[o.Key].Rows).ToList());
        }

        var written = outcomes.Where(o => o.Value is VerseOutcome.Replaced or VerseOutcome.New).Select(o => o.Key).ToArray();
        var newState = await session.ReadVerseHashesAsync(written);

        foreach (var unchanged in outcomes.Where(o => o.Value == VerseOutcome.Unchanged).Select(o => o.Key))
        {
            newState[unchanged] = dbHashes[unchanged];
        }

        await session.UpsertStateAsync(source.SeedVersion, newState);
        await session.DeleteStateAsync(outcomes.Where(o => o.Value == VerseOutcome.Removed).Select(o => o.Key).ToArray());

        var updated = outcomes.Count(o => o.Value is VerseOutcome.Replaced or VerseOutcome.New or VerseOutcome.Removed);
        var kept = outcomes.Count(o => o.Value == VerseOutcome.KeptLocal);
        await session.CompleteAsync(new MappingImportRecord(source.SeedVersion, source.ExportedAt, counts[4], counts[0], counts[1], counts[2], counts[3], updated, kept));

        _logger.LogInformation(
            "Mapping seed updated {Previous} -> {Version}: {Updated} verses changed, {Kept} verses kept because they were changed locally, {Unchanged} verses already equal",
            previousVersion[..12], source.SeedVersion[..12], updated, kept, outcomes.Count(o => o.Value == VerseOutcome.Unchanged));

        var reportFile = $"mappings-update-{source.SeedVersion[..12]}.csv";
        var versesFile = $"mappings-update-{source.SeedVersion[..12]}-verses.csv";
        await WriteReportAsync(reportFile, reportLines, token);
        await WriteVerseReportAsync(versesFile, outcomes, token);

        var report = new SeedStepReport { Kind = Kind, Action = "update", SeedVersion = source.SeedVersion }
            .WithCount("rows", counts[4])
            .WithCount("exact", counts[0]).WithCount("shifted", counts[1]).WithCount("modified", counts[2]).WithCount("orphaned", counts[3])
            .WithCount("replaced", outcomes.Count(o => o.Value == VerseOutcome.Replaced))
            .WithCount("new", outcomes.Count(o => o.Value == VerseOutcome.New))
            .WithCount("removed", outcomes.Count(o => o.Value == VerseOutcome.Removed))
            .WithCount("kept", kept)
            .WithCount("unchanged", outcomes.Count(o => o.Value == VerseOutcome.Unchanged))
            .WithReportFile(reportFile).WithReportFile(versesFile);

        foreach (var (name, outcome) in new[] { ("replaced", VerseOutcome.Replaced), ("kept", VerseOutcome.KeptLocal), ("new", VerseOutcome.New), ("removed", VerseOutcome.Removed) })
        {
            report.AddList(name, outcomes.Where(o => o.Value == outcome).Select(o => o.Key).Order().ToList(), k => SeedVerseFormat.Verse(k));
        }

        return report;
    }

    /// <summary>Per verse: replace where the database still equals the last import, keep where it was changed, adopt where it already equals the new seed.</summary>
    public static Dictionary<int, VerseOutcome> Decide(
        IReadOnlyDictionary<int, VerseRows> desired,
        IReadOnlyDictionary<int, string> state,
        IReadOnlyDictionary<int, string> dbHashes)
    {
        var outcomes = new Dictionary<int, VerseOutcome>();

        foreach (var verse in desired.Keys.Union(state.Keys).Order())
        {
            var wanted = desired.TryGetValue(verse, out var rows) ? ComputeHash(rows.Rows) : null;
            var current = dbHashes.GetValueOrDefault(verse);
            var stored = state.GetValueOrDefault(verse);

            if (current == wanted)
            {
                outcomes[verse] = wanted is null ? VerseOutcome.Nothing : VerseOutcome.Unchanged;
            }
            else if (current == stored)
            {
                outcomes[verse] = current is null ? VerseOutcome.New : wanted is null ? VerseOutcome.Removed : VerseOutcome.Replaced;
            }
            else
            {
                outcomes[verse] = VerseOutcome.KeptLocal;
            }
        }

        return outcomes.Where(o => o.Value != VerseOutcome.Nothing).ToDictionary(o => o.Key, o => o.Value);
    }

    public static string ComputeHash(IEnumerable<MappingRowPlan> rows)
    {
        return MappingVerseHash.Compute(rows.Select(r => new VerseEdge(r.PositionInVerse, r.Part ?? string.Empty, r.SrcRef, r.SrcPos, r.Added, r.ParentRef, r.ParentPos, r.Role, r.Strongs ?? string.Empty, r.PartLemma ?? string.Empty, r.MultiHead)));
    }

    /// <summary>Checks again inside the write transaction that the verses about to be replaced still equal the last import.</summary>
    private static async Task GuardAgainstConcurrentEditsAsync(MappingWriteSession session, Dictionary<int, VerseOutcome> outcomes, int[] candidates, Dictionary<int, string> state)
    {
        var now = await session.ReadVerseHashesAsync(candidates);

        foreach (var verse in candidates)
        {
            if (now.GetValueOrDefault(verse) != state.GetValueOrDefault(verse))
            {
                outcomes[verse] = VerseOutcome.KeptLocal;
            }
        }
    }

    // ---- format 1 -------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Records a legacy (format 1) import that the old code path just made: the SeedImports row and the state of every verse, so a later
    /// format 2 seed can update verses that were not changed since.
    /// </summary>
    public async Task RecordLegacyImportAsync(MappingSeedSource source, CancellationToken token = default)
    {
        using var session = await _repository.BeginWriteAsync(token);
        var rows = await session.CountMappingRowsAsync();
        var hashes = await _repository.ReadAllVerseHashesAsync();

        await session.UpsertStateAsync(source.SeedVersion, hashes);
        await session.CompleteAsync(new MappingImportRecord(source.SeedVersion, source.ExportedAt, rows, rows, 0, 0, 0, null, null));

        _logger.LogInformation("Recorded the format 1 mapping import ({Rows} rows, {Verses} verses) in SeedImports and SeedVerseState", rows, hashes.Count);
    }

    // ---- planning ------------------------------------------------------------------------------------------------------------

    public async Task<BookPlan> PlanBookAsync(SeedLayout layout, SeedFileEntry file, SeedKind? morphology, CancellationToken token)
    {
        var bookId = int.Parse(file.Path[..2], CultureInfo.InvariantCulture);
        var greek = bookId >= MappingBooks.FirstGreekBookId;
        var bytes = await File.ReadAllBytesAsync(layout.ResolveMapping(file.Path), token);
        var lines = SeedCsv.Read<MappingSeedLine, MappingSeedLineMap>(bytes);

        var words = await _repository.ReadBookWordsAsync(bookId);
        var newTokens = words.Where(w => !AnchorToken.IsBareDash(w.Text)).Select(w => AnchorToken.ForWord(w.Ref, w.Pos, w.Form)).ToList();
        var wordIds = words.ToDictionary(w => (w.Ref, w.Pos), w => w.Id);
        var german = GermanWordResolver.Create(await ReadManifestTokensAsync(layout, morphology, bookId, token), newTokens);

        var sourceKeys = lines.Where(l => l.HasSource).Select(l => l.Source!.Value).Distinct().ToList();
        var stepRows = sourceKeys.Count == 0 ? [] : await _repository.ReadSourceWordsAsync(greek, sourceKeys.Select(k => k.LxxRefId).Distinct().ToArray());
        var stepTokens = stepRows.Select(r => new AnchorToken(r.Ref, r.Pos, SourceKey.Hash(r.Text))).ToList();
        var stepIds = stepTokens.Zip(stepRows).GroupBy(p => p.First).ToDictionary(g => g.Key, g => g.First().Second.Id);
        var source = SourceWordResolver.Create(sourceKeys, stepTokens);

        var placement = MappingPlacer.Place(lines, german, source);
        var rows = ApplyParts(placement.Placed.Select(p => ToRow(p, wordIds, stepIds, greek)).ToList(), words);

        return new BookPlan { BookId = bookId, Greek = greek, Rows = rows, Placement = placement, ChaptersAligned = german.ChaptersAligned.Count };
    }

    /// <summary>
    /// Keeps only real compound parts (older seeds carry the whole word, <c>1</c>, <c>null</c> ... in the part column, see migration 0027) and
    /// derives the part order, which the seed does not carry, from the position of the part inside the word. The seed format stays as it is.
    /// </summary>
    public static List<MappingRowPlan> ApplyParts(List<MappingRowPlan> rows, List<ElbWordRow> words)
    {
        var forms = words.ToDictionary(w => w.Id, w => w.Form ?? string.Empty);
        var cleaned = rows.Select(r => CleanPartRow(r, forms[r.ElbWordId])).ToList();

        foreach (var word in Enumerable.Range(0, cleaned.Count).Where(i => cleaned[i].Part is not null).GroupBy(i => cleaned[i].ElbWordId))
        {
            var indexes = word.ToList();
            var orders = GermanWordParts.DeriveOrders(forms[word.Key], indexes.Select(i => cleaned[i].Part!).ToList());

            for (var n = 0; n < indexes.Count; n++)
            {
                cleaned[indexes[n]] = cleaned[indexes[n]] with { PartOrder = orders[n] };
            }
        }

        return cleaned;
    }

    /// <summary>The cleaned part; a part lemma only stays with a part (check constraint), and a row never keeps a lemma of a dropped part.</summary>
    private static MappingRowPlan CleanPartRow(MappingRowPlan row, string form)
    {
        var part = GermanWordParts.CleanPart(form, row.Part);

        return row with { Part = part, PartLemma = part is null ? null : row.PartLemma };
    }

    private static MappingRowPlan ToRow(PlacedMapping placed, Dictionary<(int, int), int> wordIds, Dictionary<AnchorToken, int> stepIds, bool greek)
    {
        var german = placed.German!.Value;
        var line = placed.Line;

        return new MappingRowPlan(
            german.Ref,
            german.Pos,
            wordIds[(german.Ref, german.Pos)],
            placed.Source is { } src ? stepIds[src] : null,
            placed.Source?.Ref ?? 0,
            placed.Source?.Pos ?? 0,
            string.IsNullOrEmpty(line.Part) ? null : line.Part,
            line.Added,
            placed.Parent is { } parent ? wordIds[(parent.Ref, parent.Pos)] : null,
            placed.Parent?.Ref ?? 0,
            placed.Parent?.Pos ?? 0,
            Role: string.IsNullOrWhiteSpace(line.Role) ? "head" : line.Role,
            Strongs: greek || string.IsNullOrEmpty(line.Strongs) ? null : line.Strongs, // Greek derives the morpheme from the STEP word
            PartLemma: string.IsNullOrEmpty(line.PartLemma) ? null : line.PartLemma,
            MultiHead: line.MultiHead);
    }

    /// <summary>The old side of the German alignment: every German word of the exporting installation, from the morphology seed (empty when it is missing).</summary>
    private async Task<List<AnchorToken>> ReadManifestTokensAsync(SeedLayout layout, SeedKind? morphology, int bookId, CancellationToken token)
    {
        var path = SeedLayout.RelativePath(SeedLayout.MorphologyFolder, bookId);

        if (morphology?.Files.Any(f => f.Path == path) != true || !_fileService.FileExists(layout.Resolve(path)))
        {
            _logger.LogWarning("No morphology seed for book {Book}; German words are only matched at their exact place", bookId);
            return [];
        }

        var bytes = await File.ReadAllBytesAsync(layout.Resolve(path), token);

        return SeedCsv.Read<MorphologySeedLine, MorphologySeedLineMap>(bytes).Select(l => l.ToToken()).ToList();
    }

    private static void Add(int[] counts, BookPlan plan)
    {
        counts[0] += plan.Placement.Exact;
        counts[1] += plan.Placement.Shifted;
        counts[2] += plan.Placement.Modified;
        counts[3] += plan.Placement.Orphaned;
        counts[4] += plan.Rows.Count;
    }

    // ---- reports -------------------------------------------------------------------------------------------------------------

    /// <summary>Report of everything that is not an exact match: class, why a line was dropped, old key, new key, form.</summary>
    private async Task WriteReportAsync(string fileName, List<PlacedMapping> lines, CancellationToken token)
    {
        var builder = new StringBuilder("Class,Reason,Ref,Pos,Form,SrcRef,SrcPos,NewRef,NewPos,NewSrcRef,NewSrcPos,NewParentRef,NewParentPos\n");

        foreach (var placed in lines)
        {
            var line = placed.Line;
            builder.Append(placed.Class).Append(',').Append(placed.Reason).Append(',')
                .Append(line.Ref).Append(',').Append(line.Pos).Append(',').Append(Quote(line.Form)).Append(',')
                .Append(Number(line.SrcRef)).Append(',').Append(Number(line.SrcPos)).Append(',')
                .Append(Number(placed.German?.Ref)).Append(',').Append(Number(placed.German?.Pos)).Append(',')
                .Append(Number(placed.Source?.Ref)).Append(',').Append(Number(placed.Source?.Pos)).Append(',')
                .Append(Number(placed.Parent?.Ref)).Append(',').Append(Number(placed.Parent?.Pos)).Append('\n');
        }

        await WriteFileAsync(fileName, builder.ToString(), token);
        _logger.LogInformation("Mapping seed report ({Lines} lines) written to {Path}", lines.Count, _fileService.Combine(CreateLayout().ReportRoot, fileName));
    }

    private async Task WriteVerseReportAsync(string fileName, Dictionary<int, VerseOutcome> outcomes, CancellationToken token)
    {
        var builder = new StringBuilder("HebRefId,Outcome\n");

        foreach (var (verse, outcome) in outcomes.Where(o => o.Value != VerseOutcome.Unchanged).OrderBy(o => o.Key))
        {
            builder.Append(verse).Append(',').Append(outcome).Append('\n');
        }

        await WriteFileAsync(fileName, builder.ToString(), token);
    }

    private async Task WriteFileAsync(string fileName, string content, CancellationToken token)
    {
        var layout = CreateLayout();
        _fileService.CreateDirectoryIfNotExists(layout.ReportRoot);
        await _fileService.WriteAllTextAsync(_fileService.Combine(layout.ReportRoot, fileName), content, SeedLayout.Utf8NoBom, token);
    }

    private static string Number(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Quote(string value)
    {
        return value.Contains(',') || value.Contains('"') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
}

/// <summary>The rows the new seed wants for one German verse; all of a verse are in one mapping table.</summary>
public sealed record VerseRows(bool Greek, List<MappingRowPlan> Rows);

public enum VerseOutcome
{
    /// <summary>Neither the seed nor the database has rows for the verse.</summary>
    Nothing,

    /// <summary>The database already equals the new seed; only the stored state moves to the new version.</summary>
    Unchanged,

    /// <summary>Untouched since the last import: rows replaced by the new seed.</summary>
    Replaced,

    /// <summary>The verse has rows in the new seed only and none in the database.</summary>
    New,

    /// <summary>Untouched since the last import and gone from the new seed: rows deleted.</summary>
    Removed,

    /// <summary>Changed since the last import: nothing written, listed in the report.</summary>
    KeptLocal
}
