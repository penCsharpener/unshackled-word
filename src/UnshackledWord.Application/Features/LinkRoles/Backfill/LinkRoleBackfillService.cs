using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Application.Features.LinkRoles.Backfill;

/// <summary>Dry run, apply and undo of the link-roles backfill (roadmap 0018.4, 05-data-model.md "Deterministic backfill").</summary>
public sealed class LinkRoleBackfillService
{
    private readonly ILinkRoleBackfillRepository _repository;

    public LinkRoleBackfillService(ILinkRoleBackfillRepository repository)
    {
        _repository = repository;
    }

    /// <summary>Why the job must not run now (an agent run is active), or null.</summary>
    public async Task<string?> CheckBlockedAsync(CancellationToken ct)
    {
        var active = await _repository.CountActiveAgentRunsAsync(ct);
        return active > 0 ? $"{active} agent run(s) are queued, running or waiting for a provider; finish or cancel them first." : null;
    }

    public Task<LinkRoleJobResult> RunAsync(string mode, int userId, IProgress<string> progress, CancellationToken ct) => mode switch
    {
        LinkRoleJobModes.DryRun => DryRunAsync(progress, ct),
        LinkRoleJobModes.Apply => ApplyAsync(userId, progress, ct),
        LinkRoleJobModes.Undo => UndoAsync(userId, progress, ct),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown link-roles mode."),
    };

    /// <summary>Loads every book and builds the change set against the current data.</summary>
    public async Task<LinkRoleChangeSet> PlanAsync(IProgress<string> progress, CancellationToken ct)
    {
        var known = await _repository.LoadKnownLemmasAsync(ct);
        progress.Report($"{known.Count} known lemmas loaded");
        var builder = new LinkRoleChangeSetBuilder(known);
        foreach (var bookId in await _repository.GetBookIdsAsync(ct))
        {
            ct.ThrowIfCancellationRequested();
            var book = await _repository.LoadBookAsync(bookId, ct);
            builder.AddBook(book);
            progress.Report($"{book.BookName}: {book.Words.Count} words, {book.GreekRows.Count + book.HebrewRows.Count} mapping rows");
        }

        var plan = builder.Build();
        progress.Report($"Change set: {plan.Mappings.Count} mapping rows, {plan.Lemmas.Count} lemmas, {plan.Report.Undecided} undecided links");
        return plan;
    }

    private async Task<LinkRoleJobResult> DryRunAsync(IProgress<string> progress, CancellationToken ct)
    {
        var plan = await PlanAsync(progress, ct);
        return new LinkRoleJobResult { Report = plan.Report, MappingRows = plan.Mappings.Count, LemmaRows = plan.Lemmas.Count };
    }

    private async Task<LinkRoleJobResult> ApplyAsync(int userId, IProgress<string> progress, CancellationToken ct)
    {
        var plan = await PlanAsync(progress, ct);
        if (await CheckBlockedAsync(ct) is { } blocked)
        {
            throw new InvalidOperationException(blocked);
        }

        progress.Report("Writing the change set");
        var note = $"link-roles backfill: {plan.Mappings.Count} mapping rows, {plan.Lemmas.Count} lemmas";
        var applied = await _repository.ApplyAsync(plan.Mappings, plan.Lemmas, userId, note, ct);
        progress.Report($"Change set {applied.ChangeSetId} written: {applied.MappingRows} mapping rows, {applied.LemmaRows} lemmas");
        return new LinkRoleJobResult { Report = plan.Report, ChangeSetId = applied.ChangeSetId, MappingRows = applied.MappingRows, LemmaRows = applied.LemmaRows };
    }

    private async Task<LinkRoleJobResult> UndoAsync(int userId, IProgress<string> progress, CancellationToken ct)
    {
        var applied = await _repository.GetAppliedAsync(ct) ?? throw new InvalidOperationException("No applied link-roles change set to undo.");
        progress.Report($"Undoing change set {applied.Id} ({applied.MappingRows} mapping rows, {applied.LemmaRows} lemmas)");
        var undo = await _repository.UndoAsync(applied.Id, userId, ct);
        progress.Report($"Restored {undo.MappingRestored} mapping rows ({undo.MappingSkipped} changed since), {undo.LemmasRestored} lemmas ({undo.LemmasSkipped} changed since)");
        return new LinkRoleJobResult
        {
            ChangeSetId = undo.ChangeSetId,
            UndoChangeSetId = undo.UndoChangeSetId,
            MappingRows = undo.MappingRestored,
            LemmaRows = undo.LemmasRestored,
            MappingSkipped = undo.MappingSkipped,
            LemmasSkipped = undo.LemmasSkipped,
        };
    }
}
