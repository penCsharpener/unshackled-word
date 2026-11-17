using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Seed.Mappings;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Revision;
using UnshackledWord.Application.Features.Seed.Anchoring;
using UnshackledWord.Application.Features.Seed.Revision;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Infrastructure.Seed;
using UnshackledWord.Infrastructure.Seed.Revision;

namespace UnshackledWord.Infrastructure.Seed.Revision;

/// <summary>One variant of the seed, derived on this installation: tokens, base links, placed mapping rows and the content hash the state stores.</summary>
public sealed class VariantPlan
{
    public required RevisionVerseSeedLine Line { get; init; }
    public required string VerseText { get; init; }
    public required List<RevisionToken> Tokens { get; init; }
    public required IReadOnlyList<RevisionCarryWord> Words { get; init; }
    public int? ParentLevel { get; init; }
    public required RevisionMappingPlacement Placement { get; init; }
    public required List<RevisionMappingWrite> Rows { get; init; }
    public required string Hash { get; init; }
    public (int Ref, int Level) Key => (Line.Ref, Line.Level);
}

/// <summary>A variant that could not be placed at all (unknown level, verse missing in the base text, empty text, circular base).</summary>
public sealed record OrphanVariant(RevisionVerseSeedLine Line, string Reason);

public sealed class RevisionBookPlan
{
    /// <summary>Parents before the variants that were started from them.</summary>
    public required List<VariantPlan> Variants { get; init; }
    public required List<OrphanVariant> Orphans { get; init; }
}

/// <summary>What a seed run decided per variant; same vocabulary as the mapping update.</summary>
public enum RevisionOutcome
{
    Unchanged,
    Replaced,
    New,
    Removed,
    KeptLocal
}

/// <summary>
/// Imports and updates the revision seed (roadmap 0011.12). A variant is re-created exactly like the editor creates it: the text is
/// tokenised by <see cref="RevisionTokenizer"/> and the base links and change flags come from <see cref="RevisionCarry"/>; only the
/// mapping rows come from the seed (German side by position and form inside the variant's own text, original side by
/// <c>SrcRef</c>/<c>SrcPos</c>/<c>SrcHash</c> with the 0010.9 rules). Whatever cannot be placed is reported, never guessed.
/// </summary>
public sealed class RevisionSeedImporter
{
    private static readonly HashSet<string> Origins = ["carried", "manual", "seed"];

    private readonly RevisionSeedRepository _repository;
    private readonly RevisionSeedSourceReader _sourceReader;
    private readonly MappingSeedRepository _mappingRepository;
    private readonly RevisionExportRepository _exportRepository;
    private readonly IFileService _fileService;
    private readonly DatabaseSeedSettings _settings;
    private readonly ILogger<RevisionSeedImporter> _logger;

    public RevisionSeedImporter(
        RevisionSeedRepository repository,
        RevisionSeedSourceReader sourceReader,
        MappingSeedRepository mappingRepository,
        RevisionExportRepository exportRepository,
        IFileService fileService,
        IOptions<AppSettings> options,
        ILogger<RevisionSeedImporter> logger)
    {
        _repository = repository;
        _sourceReader = sourceReader;
        _mappingRepository = mappingRepository;
        _exportRepository = exportRepository;
        _fileService = fileService;
        _settings = options.Value.DatabaseSeeding;
        _logger = logger;
    }

    public SeedLayout CreateLayout(string? seedRootOverride = null) => new(_fileService, _settings, seedRootOverride);

    public Task<RevisionSeedSource?> LoadSourceAsync(SeedLayout layout, CancellationToken token = default) => _sourceReader.LoadAsync(layout, token);

    // ---- first import ----------------------------------------------------------------------------------------------------------

    /// <summary>Imports the seed into empty revision tables: all variants in one transaction, then the verse states and the SeedImports row.</summary>
    public async Task<SeedStepReport> ImportAsync(RevisionSeedSource source, CancellationToken token = default)
    {
        using var session = await _repository.BeginWriteAsync(token);

        if (await _repository.CountVariantRowsAsync() > 0)
        {
            _logger.LogInformation("Revision seed skipped: RevisionVerses is not empty");

            return SeedStepReport.Skip(RevisionSeedRepository.Kind, "import", SeedSkipReason.TablesNotEmpty, source.SeedVersion);
        }

        var levels = await _repository.ReadLevelsAsync();
        var plans = await PlanAllAsync(source, levels, token);

        foreach (var plan in plans.Variants)
        {
            await session.WriteVariantAsync(ToWrite(plan));
            await session.UpsertStateAsync(source.SeedVersion, plan.Line.Ref, plan.Line.Level, plan.Hash);
        }

        var counts = Count(plans);
        await session.CompleteAsync(new RevisionImportRecord(source.SeedVersion, source.ExportedAt, counts.Rows, counts.Exact, counts.Shifted, counts.Modified, counts.Orphaned, null, null));

        _logger.LogInformation(
            "Revision seed imported: {Variants} variants, {Rows} mapping rows (exact {Exact}, shifted {Shifted}, modified {Modified}, orphaned {Orphaned} lines/variants)",
            plans.Variants.Count, counts.Rows, counts.Exact, counts.Shifted, counts.Modified, counts.Orphaned);

        var reportFile = $"revision-{source.SeedVersion[..12]}.csv";
        await WriteReportAsync(reportFile, plans, token);

        return BuildReport("import", source.SeedVersion, plans, counts)
            .WithCount("variants", plans.Variants.Count)
            .WithReportFile(reportFile);
    }

    private static SeedStepReport BuildReport(string action, string seedVersion, RevisionBookPlan plans, (int Rows, int Exact, int Shifted, int Modified, int Orphaned) counts)
    {
        var report = new SeedStepReport { Kind = RevisionSeedRepository.Kind, Action = action, SeedVersion = seedVersion };
        report.Counts["rows"] = counts.Rows;
        report.Counts["exact"] = counts.Exact;
        report.Counts["shifted"] = counts.Shifted;
        report.Counts["modified"] = counts.Modified;
        report.Counts["orphaned"] = counts.Orphaned;
        report.AddList("orphans", plans.Orphans, o => $"{SeedVerseFormat.Verse(o.Line.Ref, o.Line.Level)} ({o.Reason})");

        return report;
    }

    // ---- later starts ----------------------------------------------------------------------------------------------------------

    /// <summary>
    /// On a populated database: when the repo's revision seed is newer than the last import, replaces every variant nobody changed
    /// since (database hash equals the stored one) with the seed's version, adds new ones, ends withdrawn ones, and keeps and reports the
    /// rest. Nothing happens when updates are switched off, when the seed is the one already imported, or without an import record.
    /// </summary>
    public async Task<SeedStepReport> UpdateAsync(RevisionSeedSource source, CancellationToken token = default)
    {
        if (!_settings.UpdateRevisionSeed)
        {
            _logger.LogInformation("Revision seed update skipped: AppSettings:DatabaseSeeding:UpdateRevisionSeed is false");

            return SeedStepReport.Skip(RevisionSeedRepository.Kind, "update", SeedSkipReason.UpdateDisabled, source.SeedVersion);
        }

        var latest = await _repository.ReadLatestImportAsync();

        if (latest is null)
        {
            _logger.LogInformation("Revision seed update skipped: the revision was not imported from a seed (no SeedImports record), so there is no baseline to tell local changes from seed variants");

            return SeedStepReport.Skip(RevisionSeedRepository.Kind, "update", SeedSkipReason.NoBaseline, source.SeedVersion);
        }

        if (latest.SeedVersion == source.SeedVersion)
        {
            _logger.LogInformation("Revision seed is up to date (version {Version}), nothing to update", source.SeedVersion[..12]);

            return SeedStepReport.Skip(RevisionSeedRepository.Kind, "update", SeedSkipReason.UpToDate, source.SeedVersion);
        }

        var levels = await _repository.ReadLevelsAsync();
        var plans = await PlanAllAsync(source, levels, token);
        var state = await _repository.ReadStateAsync();
        var desired = plans.Variants.ToDictionary(v => v.Key);
        var current = await ReadDatabaseHashesAsync(desired.Keys.Concat(state.Keys).Select(k => k.Item1 / 1_000_000).Distinct());
        var outcomes = Decide(desired.ToDictionary(d => d.Key, d => d.Value.Hash), state, current);

        // A seed variant that could not be placed is not "gone from the seed": the local one stays whatever it is.
        foreach (var orphan in plans.Orphans)
        {
            outcomes.Remove((orphan.Line.Ref, orphan.Line.Level));
        }

        using var session = await _repository.BeginWriteAsync(token);

        foreach (var plan in plans.Variants.Where(p => outcomes.TryGetValue(p.Key, out var o) && o is RevisionOutcome.Replaced or RevisionOutcome.New))
        {
            await session.WriteVariantAsync(ToWrite(plan));
        }

        foreach (var (key, outcome) in outcomes)
        {
            switch (outcome)
            {
                case RevisionOutcome.Removed:
                    await session.EndActiveAsync(key.Ref, key.Level);
                    await session.DeleteStateAsync(key.Ref, key.Level);
                    break;
                case RevisionOutcome.Replaced or RevisionOutcome.New or RevisionOutcome.Unchanged:
                    await session.UpsertStateAsync(source.SeedVersion, key.Ref, key.Level, desired[key].Hash);
                    break;
            }
        }

        var updated = outcomes.Count(o => o.Value is RevisionOutcome.Replaced or RevisionOutcome.New or RevisionOutcome.Removed);
        var kept = outcomes.Count(o => o.Value == RevisionOutcome.KeptLocal);
        var counts = Count(plans);
        await session.CompleteAsync(new RevisionImportRecord(source.SeedVersion, source.ExportedAt, counts.Rows, counts.Exact, counts.Shifted, counts.Modified, counts.Orphaned, updated, kept));

        _logger.LogInformation(
            "Revision seed updated {Previous} -> {Version}: {Updated} variants changed, {Kept} kept because they were changed locally, {Unchanged} already equal",
            latest.SeedVersion[..12], source.SeedVersion[..12], updated, kept, outcomes.Count(o => o.Value == RevisionOutcome.Unchanged));

        var reportFile = $"revision-update-{source.SeedVersion[..12]}.csv";
        var variantsFile = $"revision-update-{source.SeedVersion[..12]}-variants.csv";
        await WriteReportAsync(reportFile, plans, token);
        await WriteOutcomeReportAsync(variantsFile, outcomes, token);

        var report = BuildReport("update", source.SeedVersion, plans, counts)
            .WithCount("variants", plans.Variants.Count)
            .WithCount("replaced", outcomes.Count(o => o.Value == RevisionOutcome.Replaced))
            .WithCount("new", outcomes.Count(o => o.Value == RevisionOutcome.New))
            .WithCount("removed", outcomes.Count(o => o.Value == RevisionOutcome.Removed))
            .WithCount("kept", kept)
            .WithCount("unchanged", outcomes.Count(o => o.Value == RevisionOutcome.Unchanged))
            .WithReportFile(reportFile)
            .WithReportFile(variantsFile);

        foreach (var (name, outcome) in new[] { ("replaced", RevisionOutcome.Replaced), ("kept", RevisionOutcome.KeptLocal), ("new", RevisionOutcome.New), ("removed", RevisionOutcome.Removed) })
        {
            report.AddList(name, outcomes.Where(o => o.Value == outcome).Select(o => o.Key).Order().ToList(), k => SeedVerseFormat.Verse(k.Ref, k.Level));
        }

        return report;
    }

    /// <summary>The startup behaviour: import into an empty <c>RevisionVerses</c>, otherwise the rolling update. Null seed = nothing to do.</summary>
    public async Task<SeedStepReport> RunAsync(SeedLayout layout, CancellationToken token = default)
    {
        var source = await LoadSourceAsync(layout, token);

        if (source is null)
        {
            _logger.LogInformation("No revision seed in assets/seed/revision; nothing to import");

            return SeedStepReport.Skip(RevisionSeedRepository.Kind, "import", SeedSkipReason.NoSeed);
        }

        return await _repository.CountVariantRowsAsync() == 0 ? await ImportAsync(source, token) : await UpdateAsync(source, token);
    }

    /// <summary>Explicit import (maintainer page): only into an empty <c>RevisionVerses</c>.</summary>
    public async Task<SeedStepReport> ImportFromLayoutAsync(SeedLayout layout, CancellationToken token = default)
    {
        var source = await LoadSourceAsync(layout, token);

        return source is null
            ? SeedStepReport.Skip(RevisionSeedRepository.Kind, "import", SeedSkipReason.NoSeed)
            : await ImportAsync(source, token);
    }

    /// <summary>Explicit update (maintainer page): the seed in the layout against a populated database.</summary>
    public async Task<SeedStepReport> UpdateFromLayoutAsync(SeedLayout layout, CancellationToken token = default)
    {
        var source = await LoadSourceAsync(layout, token);

        return source is null
            ? SeedStepReport.Skip(RevisionSeedRepository.Kind, "update", SeedSkipReason.NoSeed)
            : await UpdateAsync(source, token);
    }

    /// <summary>
    /// Per variant: replace where the database still equals the last import, keep where it was changed locally, adopt where it already
    /// equals the new seed. Keys not in the result need nothing (neither seed nor database holds the variant).
    /// </summary>
    public static Dictionary<(int Ref, int Level), RevisionOutcome> Decide(
        IReadOnlyDictionary<(int Ref, int Level), string> desired,
        IReadOnlyDictionary<(int Ref, int Level), string> state,
        IReadOnlyDictionary<(int Ref, int Level), string> database)
    {
        var outcomes = new Dictionary<(int Ref, int Level), RevisionOutcome>();

        foreach (var key in desired.Keys.Union(state.Keys).Order())
        {
            var wanted = desired.GetValueOrDefault(key);
            var current = database.GetValueOrDefault(key);
            var stored = state.GetValueOrDefault(key);

            if (current == wanted)
            {
                if (wanted is not null)
                {
                    outcomes[key] = RevisionOutcome.Unchanged;
                }
            }
            else if (current == stored)
            {
                outcomes[key] = current is null ? RevisionOutcome.New : wanted is null ? RevisionOutcome.Removed : RevisionOutcome.Replaced;
            }
            else
            {
                outcomes[key] = RevisionOutcome.KeptLocal;
            }
        }

        return outcomes;
    }

    private async Task<Dictionary<(int Ref, int Level), string>> ReadDatabaseHashesAsync(IEnumerable<int> bookIds)
    {
        var hashes = new Dictionary<(int, int), string>();

        foreach (var bookId in bookIds.Order())
        {
            var data = await _exportRepository.ReadBookAsync(bookId);
            var mapping = data.Mapping.GroupBy(m => (m.Ref, m.Level)).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var verse in data.Verses)
            {
                hashes[(verse.Ref, verse.Level)] = RevisionVariantHash.Compute(verse, mapping.GetValueOrDefault((verse.Ref, verse.Level)) ?? []);
            }
        }

        return hashes;
    }

    // ---- planning ------------------------------------------------------------------------------------------------------------

    public async Task<RevisionBookPlan> PlanAllAsync(RevisionSeedSource source, HashSet<int> levelsInDatabase, CancellationToken token)
    {
        var all = new RevisionBookPlan { Variants = [], Orphans = [] };

        foreach (var book in source.Verses.GroupBy(v => v.Ref / 1_000_000).OrderBy(g => g.Key))
        {
            var mapping = source.Mapping.Where(m => m.Ref / 1_000_000 == book.Key).GroupBy(m => (m.Ref, m.Level)).ToDictionary(g => g.Key, g => g.ToList());
            var plan = await PlanBookAsync(book.Key, book.ToList(), mapping, levelsInDatabase);
            all.Variants.AddRange(plan.Variants);
            all.Orphans.AddRange(plan.Orphans);
        }

        return all;
    }

    public async Task<RevisionBookPlan> PlanBookAsync(
        int bookId,
        List<RevisionVerseSeedLine> verses,
        Dictionary<(int Ref, int Level), List<RevisionMappingSeedLine>> mapping,
        HashSet<int> levelsInDatabase)
    {
        var greek = bookId >= 40;
        var baseWords = (await _mappingRepository.ReadBookWordsAsync(bookId))
            .GroupBy(w => w.Ref)
            .ToDictionary(g => g.Key, g => g.OrderBy(w => w.Pos).Select(w => new RevisionCarryParentWord(w.Pos, w.Form, w.Pos, RevisionCarry.Same)).ToList());

        var sourceKeys = mapping.Values.SelectMany(l => l).Where(l => l.HasSource).Select(l => l.Source!.Value).Distinct().ToList();
        var stepRows = sourceKeys.Count == 0 ? [] : await _mappingRepository.ReadSourceWordsAsync(greek, sourceKeys.Select(k => k.LxxRefId).Distinct().ToArray());
        var stepTokens = stepRows.Select(r => new AnchorToken(r.Ref, r.Pos, SourceKey.Hash(r.Text))).ToList();
        var stepIds = stepTokens.Zip(stepRows).GroupBy(p => p.First).ToDictionary(g => g.Key, g => g.First().Second.Id);
        var resolver = SourceWordResolver.Create(sourceKeys, stepTokens);

        var byKey = verses.GroupBy(v => (v.Ref, v.Level)).ToDictionary(g => g.Key, g => g.First());
        var planned = new Dictionary<(int, int), VariantPlan>();
        var orphans = new List<OrphanVariant>();
        var ordered = new List<VariantPlan>();
        var failed = new HashSet<(int, int)>();
        var visiting = new HashSet<(int, int)>();

        VariantPlan? Plan(RevisionVerseSeedLine line)
        {
            var key = (line.Ref, line.Level);

            if (planned.TryGetValue(key, out var done))
            {
                return done;
            }

            if (failed.Contains(key))
            {
                return null;
            }

            if (!levelsInDatabase.Contains(line.Level))
            {
                return Fail(line, "Level");
            }

            var tokens = RevisionTokenizer.Tokenize(line.Text);

            if (tokens.Count == 0)
            {
                return Fail(line, "EmptyText");
            }

            if (!baseWords.TryGetValue(line.Ref, out var parentWords))
            {
                return Fail(line, "Verse");
            }

            VariantPlan? parent = null;

            if (line.BasedOnLevel is { } basedOn && byKey.TryGetValue((line.Ref, basedOn), out var parentLine))
            {
                if (!visiting.Add(key))
                {
                    return Fail(line, "CircularBase");
                }

                parent = Plan(parentLine);
                visiting.Remove(key);
            }

            if (parent is not null)
            {
                parentWords = parent.Tokens
                    .Select(t =>
                    {
                        var word = parent.Words.FirstOrDefault(w => w.Position == t.Position);

                        return new RevisionCarryParentWord(t.Position, t.PlainWord, word?.BasePositionInVerse, word?.Change ?? RevisionCarry.New);
                    })
                    .ToList();
            }

            var carry = RevisionCarry.Carry(parentWords, [], tokens.Select(t => new RevisionCarryToken(t.Position, t.PlainWord)).ToList());
            var lines = mapping.GetValueOrDefault(key) ?? [];
            var placement = RevisionMappingPlacer.Place(tokens, lines, resolver);
            var rows = placement.Placed.Select(p => ToRow(p, stepIds)).ToList();
            var verseText = string.Join(' ', tokens.Select(t => t.WordInContext));
            var note = string.IsNullOrWhiteSpace(line.Note) ? null : line.Note.Trim();

            var plan = new VariantPlan
            {
                Line = line,
                VerseText = verseText,
                Tokens = tokens,
                Words = carry.Words,
                ParentLevel = parent?.Line.Level,
                Placement = placement,
                Rows = rows,
                Hash = ComputeHash(line, verseText, note, parent?.Line.Level, tokens, placement)
            };

            planned[key] = plan;
            ordered.Add(plan);

            return plan;
        }

        VariantPlan? Fail(RevisionVerseSeedLine line, string reason)
        {
            failed.Add((line.Ref, line.Level));
            orphans.Add(new OrphanVariant(line, reason));

            return null;
        }

        foreach (var line in byKey.Values.OrderBy(v => v.Ref).ThenBy(v => v.Level))
        {
            Plan(line);
        }

        return new RevisionBookPlan { Variants = ordered, Orphans = orphans };
    }

    private static string RoleOf(RevisionMappingSeedLine line) => string.IsNullOrWhiteSpace(line.Role) ? "head" : line.Role;

    private static RevisionMappingWrite ToRow(PlacedRevisionMapping placed, Dictionary<AnchorToken, int> stepIds)
    {
        var origin = Origins.Contains(placed.Line.Origin) ? placed.Line.Origin : RevisionOrigin.Seed;

        return new RevisionMappingWrite(
            placed.Pos!.Value,
            placed.Source is { } source ? stepIds[source] : null,
            placed.Line.Added,
            placed.ParentPos,
            string.IsNullOrEmpty(placed.Line.Part) ? null : placed.Line.Part,
            origin,
            RoleOf(placed.Line),
            string.IsNullOrEmpty(placed.Line.Strongs) ? null : placed.Line.Strongs,
            string.IsNullOrEmpty(placed.Line.Part) || string.IsNullOrEmpty(placed.Line.PartLemma) ? null : placed.Line.PartLemma,
            placed.Line.MultiHead);
    }

    /// <summary>The hash of the variant as the database will export it after the import (positions, forms and STEP hashes of this installation).</summary>
    private static string ComputeHash(RevisionVerseSeedLine line, string verseText, string? note, int? parentLevel, List<RevisionToken> tokens, RevisionMappingPlacement placement)
    {
        string Form(int position) => tokens.First(t => t.Position == position).PlainWord ?? string.Empty;

        var verse = new RevisionVerseSeedLine { Ref = line.Ref, Level = line.Level, BasedOnLevel = parentLevel, BaseHash = line.BaseHash, Text = verseText, Note = note, Reviewed = line.Reviewed };
        var lines = placement.Placed.Select(p => new RevisionMappingSeedLine
        {
            Ref = line.Ref,
            Level = line.Level,
            Pos = p.Pos!.Value,
            Form = Form(p.Pos!.Value),
            Part = p.Line.Part ?? string.Empty,
            SrcRef = p.Source?.Ref,
            SrcPos = p.Source?.Pos,
            SrcHash = p.Source?.Text ?? string.Empty,
            Added = p.Line.Added,
            ParentPos = p.ParentPos,
            ParentForm = p.ParentPos is { } parent ? Form(parent) : string.Empty,
            Origin = Origins.Contains(p.Line.Origin) ? p.Line.Origin : RevisionOrigin.Seed,
            Role = RoleOf(p.Line),
            Strongs = MappingBooks.IsGreek(line.Ref) ? string.Empty : p.Line.Strongs ?? string.Empty, // the database keeps the morpheme on Hebrew rows only
            PartLemma = string.IsNullOrEmpty(p.Line.Part) ? string.Empty : p.Line.PartLemma ?? string.Empty,
            MultiHead = p.Line.MultiHead
        });

        return RevisionVariantHash.Compute(verse, lines);
    }

    private static RevisionVariantWrite ToWrite(VariantPlan plan)
    {
        var words = plan.Words.ToDictionary(w => w.Position);

        return new RevisionVariantWrite
        {
            HebRefId = plan.Line.Ref,
            Level = plan.Line.Level,
            VerseText = plan.VerseText,
            BaseHash = plan.Line.BaseHash,
            Note = string.IsNullOrWhiteSpace(plan.Line.Note) ? null : plan.Line.Note.Trim(),
            ParentLevel = plan.ParentLevel,
            Reviewed = plan.Line.Reviewed,
            Lang = RevisionSql.Lang(plan.Line.Ref / 1_000_000),
            Words = plan.Tokens
                .Select(t => words.TryGetValue(t.Position, out var w)
                    ? new RevisionWordWrite(t.Position, t.WordInContext, t.PlainWord, w.BasePositionInVerse, w.Change)
                    : new RevisionWordWrite(t.Position, t.WordInContext, t.PlainWord, null, RevisionCarry.New))
                .ToList(),
            Mapping = plan.Rows
        };
    }

    private static (int Rows, int Exact, int Shifted, int Modified, int Orphaned) Count(RevisionBookPlan plans)
    {
        return (
            plans.Variants.Sum(v => v.Rows.Count),
            plans.Variants.Sum(v => v.Placement.Exact),
            plans.Variants.Sum(v => v.Placement.Shifted),
            plans.Variants.Sum(v => v.Placement.Modified),
            plans.Variants.Sum(v => v.Placement.Orphaned) + plans.Orphans.Count);
    }

    // ---- reports -------------------------------------------------------------------------------------------------------------

    private async Task WriteReportAsync(string fileName, RevisionBookPlan plans, CancellationToken token)
    {
        var builder = new StringBuilder("Class,Reason,Ref,Level,Pos,Form,SrcRef,SrcPos,NewPos,NewSrcRef,NewSrcPos,NewParentPos\n");

        foreach (var orphan in plans.Orphans)
        {
            builder.Append("Orphaned,").Append(orphan.Reason).Append(',').Append(orphan.Line.Ref).Append(',').Append(orphan.Line.Level).Append(",,,,,,,,\n");
        }

        foreach (var plan in plans.Variants)
        {
            foreach (var placed in plan.Placement.Reportable)
            {
                var line = placed.Line;
                builder.Append(placed.Class).Append(',').Append(placed.Reason).Append(',')
                    .Append(line.Ref).Append(',').Append(line.Level).Append(',').Append(line.Pos).Append(',').Append(Quote(line.Form)).Append(',')
                    .Append(Number(line.SrcRef)).Append(',').Append(Number(line.SrcPos)).Append(',')
                    .Append(Number(placed.Pos)).Append(',').Append(Number(placed.Source?.Ref)).Append(',').Append(Number(placed.Source?.Pos)).Append(',')
                    .Append(Number(placed.ParentPos)).Append('\n');
            }
        }

        await WriteFileAsync(fileName, builder.ToString(), token);
        _logger.LogInformation("Revision seed report written to {Path}", _fileService.Combine(CreateLayout().ReportRoot, fileName));
    }

    private async Task WriteOutcomeReportAsync(string fileName, Dictionary<(int Ref, int Level), RevisionOutcome> outcomes, CancellationToken token)
    {
        var builder = new StringBuilder("Ref,Level,Outcome\n");

        foreach (var ((reference, level), outcome) in outcomes.Where(o => o.Value != RevisionOutcome.Unchanged).OrderBy(o => o.Key))
        {
            builder.Append(reference).Append(',').Append(level).Append(',').Append(outcome).Append('\n');
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

    private static string Quote(string value) => value.Contains(',') || value.Contains('"') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
}
