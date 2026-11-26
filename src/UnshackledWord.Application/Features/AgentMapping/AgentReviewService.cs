using System.Text.Json;
using Microsoft.Extensions.Logging;
using UnshackledWord.Application.Features.VerseMapping;
using UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping;
using UnshackledWord.Domain.WebApi.Lemmas;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Application.Features.AgentMapping;

public enum AgentReviewOutcome
{
    Ok,
    NotFound,

    /// <summary>The proposal is not <c>open</c> (already merged, discarded or superseded).</summary>
    NotOpen,

    /// <summary>"veraltet": the verse text or a lemma changed since the proposal was made.</summary>
    Stale,

    /// <summary>A hard rule fails against the live verse (messages in <see cref="AgentReviewResult{T}.Errors"/>).</summary>
    Invalid,

    /// <summary>Mapping editing is switched off on this server.</summary>
    Forbidden,
}

public sealed class AgentReviewResult<T>
{
    public AgentReviewOutcome Outcome { get; init; }
    public T? Value { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];

    public static AgentReviewResult<T> Ok(T value) => new() { Outcome = AgentReviewOutcome.Ok, Value = value };

    public static AgentReviewResult<T> Fail(AgentReviewOutcome outcome, params string[] errors) => new() { Outcome = outcome, Errors = errors };
}

public interface IAgentReviewService
{
    Task<ListAgentProposalsResponse> ListAsync(int runId, string? status, bool flaggedOnly, CancellationToken token = default);

    /// <summary>Null when the proposal does not exist.</summary>
    Task<AgentProposalDiffResponse?> GetDiffAsync(long id, CancellationToken token = default);

    /// <summary>Writes the proposal as it is: author = AI user of the run, reviewer = <paramref name="reviewerUserId"/>; status <c>merged</c>.</summary>
    Task<AgentReviewResult<MergeAgentProposalResponse>> MergeAsync(long id, int reviewerUserId, CancellationToken token = default);

    /// <summary>Writes the maintainer's edited mapping and lemmas instead; author = the reviewer (a human changed the result); status <c>edited-merged</c>.</summary>
    Task<AgentReviewResult<MergeAgentProposalResponse>> EditMergeAsync(EditMergeAgentProposalRequest request, int reviewerUserId, CancellationToken token = default);

    Task<AgentReviewResult<DiscardAgentProposalResponse>> DiscardAsync(long id, int reviewerUserId, CancellationToken token = default);

    /// <summary>Merges every open proposal of the run that has no flag; stale or invalid ones stay open and are reported.</summary>
    Task<AgentReviewResult<BulkMergeAgentProposalsResponse>> BulkMergeUnflaggedAsync(int runId, int reviewerUserId, CancellationToken token = default);
}

/// <summary>
/// Review of agent proposals (roadmap 0016.7). A merge goes through the live write paths: links through <see cref="IVerseMappingService.SaveAsync"/>
/// (which runs the save rules again), lemmas through <see cref="IAgentLemmaWriter"/> (STTS guard, audit in <c>Elb1871MorphologyEdits</c>). The hard rules are
/// checked against the live verse first: a verse whose words changed since the package, or a lemma a human edited since, is "veraltet" (409).
/// The mapping and the lemma write are two transactions: lemmas are validated before anything is written, so only a missing word id could split them.
/// </summary>
public sealed class AgentReviewService : IAgentReviewService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IAgentReviewRepository _proposals;
    private readonly IAgentStoreRepository _store;
    private readonly IAgentDataRepository _data;
    private readonly IVerseMappingService _mapping;
    private readonly IAgentLemmaWriter _lemmas;
    private readonly ILogger<AgentReviewService> _logger;

    public AgentReviewService(IAgentReviewRepository proposals, IAgentStoreRepository store, IAgentDataRepository data, IVerseMappingService mapping, IAgentLemmaWriter lemmas, ILogger<AgentReviewService> logger)
    {
        _proposals = proposals;
        _store = store;
        _data = data;
        _mapping = mapping;
        _lemmas = lemmas;
        _logger = logger;
    }

    public async Task<ListAgentProposalsResponse> ListAsync(int runId, string? status, bool flaggedOnly, CancellationToken token = default)
    {
        var records = await _proposals.ListAsync(runId, string.IsNullOrWhiteSpace(status) ? null : status, token);

        return new ListAgentProposalsResponse
        {
            RunId = runId,
            Proposals = records
                .Select(r => new AgentProposalSummaryDto
                {
                    Id = r.Id,
                    RunId = r.RunId,
                    Book = r.BibleBookId,
                    Chapter = r.Chapter,
                    Verse = r.Verse,
                    Status = r.Status,
                    FlagCount = ParseFlags(r).Count,
                    HasLemmaChanges = ParseLemmas(r).Count > 0,
                    HasRoleChanges = !string.IsNullOrEmpty(r.RolesJson) && ParseRoles(r).Count > 0,
                    HasParts = !string.IsNullOrEmpty(r.PartsJson),
                    HasNote = !string.IsNullOrWhiteSpace(r.Note),
                    CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc)),
                    ReviewedByUserId = r.ReviewedByUserId,
                    ReviewedAt = r.ReviewedAt is null ? null : new DateTimeOffset(DateTime.SpecifyKind(r.ReviewedAt.Value, DateTimeKind.Utc)),
                })
                .Where(p => !flaggedOnly || p.FlagCount > 0)
                .ToList(),
        };
    }

    public async Task<AgentProposalDiffResponse?> GetDiffAsync(long id, CancellationToken token = default)
    {
        var record = await _proposals.GetAsync(id, token);
        if (record is null || IsLxx(record))
        {
            // An LXX proposal has no mapping diff; the LXX review page (0028.7f) reads it.
            return null;
        }

        var live = await _mapping.GetAsync(record.BibleBookId, record.Chapter, record.Verse, callerCanWrite: false, token);
        var proposed = ParseLinks(record);
        var germanText = live?.GermanWords.ToDictionary(w => w.Id, w => w.Text) ?? [];
        var originalText = live?.OriginalWords.ToDictionary(w => w.Id, w => w.Original) ?? [];
        var currentLinks = live?.Links ?? [];

        AgentLinkDiffDto ToDiff(VerseMappingLink l) => new()
        {
            ElbWordId = l.ElbWordId,
            German = germanText.GetValueOrDefault(l.ElbWordId, string.Empty),
            StepWordId = l.StepWordId,
            Original = l.StepWordId is { } s ? originalText.GetValueOrDefault(s) : null,
            IsAddedWord = l.IsAddedWord,
            ParentElbWordId = l.ParentElbWordId,
            ParentGerman = l.ParentElbWordId is { } p ? germanText.GetValueOrDefault(p) : null,
            PartOrder = l.PartOrder,
            GermanWordPart = l.GermanWordPart,
        };

        var currentKeys = currentLinks.ToLookup(LinkKey);
        var proposedKeys = proposed.ToLookup(LinkKey);

        var response = new AgentProposalDiffResponse
        {
            Id = record.Id,
            RunId = record.RunId,
            Profile = record.RunProfile,
            Model = record.RunModel,
            AiUserId = record.AiUserId,
            Book = record.BibleBookId,
            Chapter = record.Chapter,
            Verse = record.Verse,
            Language = record.Language,
            Status = record.Status,
            Note = record.Note,
            ReviewedByUserId = record.ReviewedByUserId,
            ReviewedAt = record.ReviewedAt is null ? null : new DateTimeOffset(DateTime.SpecifyKind(record.ReviewedAt.Value, DateTimeKind.Utc)),
            GermanWords = live?.GermanWords ?? [],
            OriginalWords = live?.OriginalWords ?? [],
            ProposedLinks = proposed,
            CurrentLinks = currentLinks,
            Unchanged = proposed.Where(l => currentKeys.Contains(LinkKey(l))).Select(ToDiff).ToList(),
            Added = proposed.Where(l => !currentKeys.Contains(LinkKey(l))).Select(ToDiff).ToList(),
            Removed = currentLinks.Where(l => !proposedKeys.Contains(LinkKey(l))).Select(ToDiff).ToList(),
            Flags = ParseFlags(record),
        };

        var stale = await StaleReasonAsync(record, token);
        response.Stale = stale is not null;
        response.StaleReason = stale;

        var verseRows = await _lemmas.GetVerseAsync(record.BibleBookId, record.Chapter, record.Verse, token);
        var currentLemmas = verseRows.ToDictionary(r => r.WordId);
        var lemmaChanges = ParseLemmas(record);
        var proposedIds = lemmaChanges.Select(c => c.ElbWordId).ToHashSet();
        response.VerseLemmas.AddRange(verseRows.OrderBy(r => r.PositionInVerse).Select(r => new AgentVerseLemmaDto
        {
            ElbWordId = r.WordId,
            German = germanText.GetValueOrDefault(r.WordId, r.WordInContext),
            Lemma = r.Lemma,
            Stts = r.Stts,
            Proposed = proposedIds.Contains(r.WordId),
        }));
        foreach (var change in lemmaChanges)
        {
            currentLemmas.TryGetValue(change.ElbWordId, out var row);
            response.LemmaChanges.Add(new AgentLemmaDiffDto
            {
                ElbWordId = change.ElbWordId,
                German = germanText.GetValueOrDefault(change.ElbWordId, string.Empty),
                CurrentLemma = row?.Lemma,
                CurrentStts = row?.Stts,
                BeforeLemma = change.BeforeLemma,
                Lemma = change.Lemma,
                Stts = change.Stts,
                PartOfSpeech = change.PartOfSpeech,
                Changed = row is not null && !SameLemma(row.Lemma, change.BeforeLemma),
            });
        }

        foreach (var change in ParseRoles(record))
        {
            response.RoleChanges.Add(new AgentRoleDiffDto
            {
                ElbWordId = change.ElbWordId,
                German = germanText.GetValueOrDefault(change.ElbWordId, string.Empty),
                StepWordId = change.StepWordId,
                Original = change.StepWordId is { } s ? originalText.GetValueOrDefault(s) : null,
                Role = change.Role,
                StrongsNumber = change.StrongsNumber,
                MultiHead = change.MultiHead,
                PartOrder = change.PartOrder,
                PartLemma = change.PartLemma,
                BeforeRole = change.BeforeRole,
                BeforeStrongsNumber = change.BeforeStrongsNumber,
                BeforeMultiHead = change.BeforeMultiHead,
                BeforePartLemma = change.BeforePartLemma,
                AddedParentElbWordId = change.AddedParentElbWordId,
                AddedParentGerman = change.AddedParentElbWordId is { } p ? germanText.GetValueOrDefault(p) : null,
                Source = change.Source,
            });
        }

        return response;
    }

    public async Task<AgentReviewResult<MergeAgentProposalResponse>> MergeAsync(long id, int reviewerUserId, CancellationToken token = default)
    {
        var record = await _proposals.GetAsync(id, token);
        if (record is null)
        {
            return AgentReviewResult<MergeAgentProposalResponse>.Fail(AgentReviewOutcome.NotFound, $"Proposal {id} does not exist.");
        }

        var lemmas = ParseLemmas(record).Select(c => new AgentLemmaEditDto { ElbWordId = c.ElbWordId, Lemma = c.Lemma, Stts = c.Stts, PartOfSpeech = c.PartOfSpeech }).ToList();

        return await WriteAsync(record, ParseLinks(record), lemmas, record.AiUserId, reviewerUserId, clearParts: false, edited: false, token);
    }

    public async Task<AgentReviewResult<MergeAgentProposalResponse>> EditMergeAsync(EditMergeAgentProposalRequest request, int reviewerUserId, CancellationToken token = default)
    {
        var record = await _proposals.GetAsync(request.Id, token);
        if (record is null)
        {
            return AgentReviewResult<MergeAgentProposalResponse>.Fail(AgentReviewOutcome.NotFound, $"Proposal {request.Id} does not exist.");
        }

        return await WriteAsync(record, request.Links, request.Lemmas, reviewerUserId, reviewerUserId, request.ClearParts, edited: true, token);
    }

    public async Task<AgentReviewResult<DiscardAgentProposalResponse>> DiscardAsync(long id, int reviewerUserId, CancellationToken token = default)
    {
        var record = await _proposals.GetAsync(id, token);
        if (record is null)
        {
            return AgentReviewResult<DiscardAgentProposalResponse>.Fail(AgentReviewOutcome.NotFound, $"Proposal {id} does not exist.");
        }

        if (record.Status != AgentProposalStatuses.Open || !await _proposals.TryCloseAsync(id, AgentProposalStatuses.Discarded, reviewerUserId, token))
        {
            return AgentReviewResult<DiscardAgentProposalResponse>.Fail(AgentReviewOutcome.NotOpen, $"Proposal {id} is {record.Status}, not open.");
        }

        return AgentReviewResult<DiscardAgentProposalResponse>.Ok(new DiscardAgentProposalResponse { Id = id, Status = AgentProposalStatuses.Discarded });
    }

    public async Task<AgentReviewResult<BulkMergeAgentProposalsResponse>> BulkMergeUnflaggedAsync(int runId, int reviewerUserId, CancellationToken token = default)
    {
        var open = await _proposals.ListAsync(runId, AgentProposalStatuses.Open, token);
        var response = new BulkMergeAgentProposalsResponse { RunId = runId };

        foreach (var record in open.Where(r => !IsLxx(r)))
        {
            if (ParseFlags(record).Count > 0)
            {
                response.SkippedFlagged++;
                continue;
            }

            var lemmas = ParseLemmas(record).Select(c => new AgentLemmaEditDto { ElbWordId = c.ElbWordId, Lemma = c.Lemma, Stts = c.Stts, PartOfSpeech = c.PartOfSpeech }).ToList();
            var result = await WriteAsync(record, ParseLinks(record), lemmas, record.AiUserId, reviewerUserId, clearParts: false, edited: false, token);

            switch (result.Outcome)
            {
                case AgentReviewOutcome.Ok:
                    response.Merged++;
                    break;
                case AgentReviewOutcome.Forbidden:
                    return AgentReviewResult<BulkMergeAgentProposalsResponse>.Fail(AgentReviewOutcome.Forbidden, [.. result.Errors]);
                default:
                    response.Failed.Add(new BulkMergeFailureDto
                    {
                        Id = record.Id,
                        Chapter = record.Chapter,
                        Verse = record.Verse,
                        Reason = result.Outcome == AgentReviewOutcome.Stale ? "stale" : result.Outcome == AgentReviewOutcome.NotOpen ? "not-open" : "invalid",
                        Messages = [.. result.Errors],
                    });
                    break;
            }
        }

        _logger.LogInformation("Agent run {RunId}: bulk merge by user {Reviewer}: {Merged} merged, {Skipped} flagged skipped, {Failed} failed", runId, reviewerUserId, response.Merged, response.SkippedFlagged, response.Failed.Count);

        return AgentReviewResult<BulkMergeAgentProposalsResponse>.Ok(response);
    }

    private async Task<AgentReviewResult<MergeAgentProposalResponse>> WriteAsync(AgentProposalRecord record, IReadOnlyList<VerseMappingLink> links, IReadOnlyList<AgentLemmaEditDto> lemmaEdits, int authorUserId, int reviewerUserId, bool clearParts, bool edited, CancellationToken token)
    {
        static AgentReviewResult<MergeAgentProposalResponse> Fail(AgentReviewOutcome outcome, IEnumerable<string> errors) => new() { Outcome = outcome, Errors = [.. errors] };

        if (IsLxx(record))
        {
            return Fail(AgentReviewOutcome.Invalid, [$"Proposal {record.Id} is an LXX tagging proposal; it is confirmed on the LXX review page, not merged as a mapping."]);
        }

        if (record.Status != AgentProposalStatuses.Open)
        {
            return Fail(AgentReviewOutcome.NotOpen, [$"Proposal {record.Id} is {record.Status}, not open."]);
        }

        if (await StaleReasonAsync(record, token) is { } staleReason)
        {
            return Fail(AgentReviewOutcome.Stale, [$"veraltet: {staleReason}"]);
        }

        // Lemmas first, without writing: everything that can fail on the lemma side fails before the mapping is touched.
        var verseRows = await _lemmas.GetVerseAsync(record.BibleBookId, record.Chapter, record.Verse, token);
        var rows = verseRows.ToDictionary(r => r.WordId);
        var before = ParseLemmas(record).ToDictionary(c => c.ElbWordId, c => c.BeforeLemma);
        var changes = new List<LemmaWordChange>();
        var errors = new List<string>();
        var stale = new List<string>();

        foreach (var edit in lemmaEdits)
        {
            if (!rows.TryGetValue(edit.ElbWordId, out var row))
            {
                errors.Add($"lemma of word {edit.ElbWordId}: the word is not part of {record.Chapter},{record.Verse}.");
                continue;
            }

            if (!edited && before.TryGetValue(edit.ElbWordId, out var beforeLemma) && !SameLemma(row.Lemma, beforeLemma))
            {
                stale.Add($"the lemma of '{row.PlainWord}' ({edit.ElbWordId}) was '{beforeLemma}' when the agent answered and is '{row.Lemma}' now");
                continue;
            }

            var lemma = edit.Lemma?.Trim() ?? string.Empty;
            var pos = string.IsNullOrWhiteSpace(edit.PartOfSpeech) ? row.PartOfSpeech : edit.PartOfSpeech;
            var stts = string.IsNullOrWhiteSpace(edit.Stts) ? null : edit.Stts;

            if (AgentLemmaRules.CheckWellFormed(lemma) is { } shape)
            {
                errors.Add($"lemma of '{row.PlainWord}' ({edit.ElbWordId}): {shape}");
                continue;
            }

            if (AgentLemmaRules.CheckCapitalisation(lemma, stts ?? row.Stts, pos) is { } capital)
            {
                errors.Add($"lemma of '{row.PlainWord}' ({edit.ElbWordId}): {capital}");
                continue;
            }

            // The editor replaces all morphology fields of a word; everything the agent did not touch is carried over from the live row.
            changes.Add(new LemmaWordChange
            {
                WordId = row.WordId,
                Lemma = lemma,
                PartOfSpeech = pos ?? string.Empty,
                Stts = stts,
                Degree = row.Degree,
                VerbForm = row.VerbForm,
                Tense = row.Tense,
                Person = row.Person,
                Number = row.Number,
                Mood = row.Mood,
                Case = row.Case,
                Gender = row.Gender,
            });
        }

        if (stale.Count > 0)
        {
            return Fail(AgentReviewOutcome.Stale, stale.Select(s => $"veraltet: {s}"));
        }

        if (errors.Count == 0 && changes.Count > 0)
        {
            errors.AddRange(await _lemmas.CheckAsync(changes, token));
        }

        if (errors.Count > 0)
        {
            return Fail(AgentReviewOutcome.Invalid, errors);
        }

        var saved = await _mapping.SaveAsync(record.BibleBookId, record.Chapter, record.Verse, links, authorUserId, clearParts, token);
        switch (saved.Outcome)
        {
            case SaveVerseMappingOutcome.Forbidden:
                return Fail(AgentReviewOutcome.Forbidden, saved.Errors);
            case SaveVerseMappingOutcome.NotFound:
                return Fail(AgentReviewOutcome.Stale, ["veraltet: the verse has no German words any more."]);
            case SaveVerseMappingOutcome.Invalid:
                return Fail(AgentReviewOutcome.Invalid, saved.Errors);
        }

        if (changes.Count > 0)
        {
            var missing = await _lemmas.ApplyAsync(changes, authorUserId, reviewerUserId, token);
            if (missing.Count > 0)
            {
                // The words were read a moment ago from the same verse; this is a race with a text change. The mapping is written already.
                _logger.LogError("Proposal {Id}: lemma write found unknown word ids {Ids} after the mapping was saved", record.Id, string.Join(", ", missing));

                return Fail(AgentReviewOutcome.Stale, [$"veraltet: unknown word ids {string.Join(", ", missing)} while writing lemmas; the mapping of the verse was saved, the lemmas were not."]);
            }
        }

        var status = edited ? AgentProposalStatuses.EditedMerged : AgentProposalStatuses.Merged;
        if (!await _proposals.TryCloseAsync(record.Id, status, reviewerUserId, token))
        {
            _logger.LogWarning("Proposal {Id} was closed by somebody else while it was merged", record.Id);
        }

        _logger.LogInformation("Agent proposal {Id} ({Book} {Chapter}:{Verse}) {Status}: author {Author}, reviewer {Reviewer}, {Links} links, {Lemmas} lemmas", record.Id, record.BibleBookId, record.Chapter, record.Verse, status, authorUserId, reviewerUserId, links.Count, changes.Count);

        return AgentReviewResult<MergeAgentProposalResponse>.Ok(new MergeAgentProposalResponse
        {
            Id = record.Id,
            Status = status,
            AuthorUserId = authorUserId,
            ReviewerUserId = reviewerUserId,
            LinksWritten = links.Count,
            LemmasWritten = changes.Count,
        });
    }

    /// <summary>Null when the verse still has the word ids the package was built from; else why not.</summary>
    private async Task<string?> StaleReasonAsync(AgentProposalRecord record, CancellationToken token)
    {
        var package = await _store.GetPackageAsync(record.PackageId, token);
        if (package is null)
        {
            return "the package of this proposal no longer exists, so the verse cannot be compared";
        }

        var map = AgentPackageMap.FromJson(package.LocalIdMap);
        var verse = map.Verses.Values.FirstOrDefault(v => v.Chapter == record.Chapter && v.Verse == record.Verse);
        if (verse is null)
        {
            return "the verse is not part of the package of this proposal";
        }

        var live = await _data.GetRangeAsync(record.BibleBookId, record.Chapter, record.Verse, record.Chapter, record.Verse, token);
        var german = live.GermanWords.Where(w => w.Chapter == record.Chapter && w.Verse == record.Verse).ToList();
        var refIds = german.Select(w => w.HebRefId).ToHashSet();
        var source = live.SourceWords.Where(s => refIds.Contains(s.RefId)).ToList();
        var hash = german.Count == 0 ? string.Empty : AgentPackageBuilder.VerseHash(german, source);

        if (hash != verse.Hash)
        {
            return "the words of the verse changed since the package was built";
        }

        // Lemma mode writes the whole link set with roles: the links must still be the ones the package was built from.
        return map.Mode == AgentModes.Lemma && !SameLinks(verse, german, source, live.Mappings) ? "the links of the verse changed since the package was built" : null;
    }

    /// <summary>The live mapping rows of the verse equal the stored links of a lemma-mode package (pairs, parents, parts, role, morpheme, part lemma, multi-head).</summary>
    public static bool SameLinks(AgentVerseMap verse, IReadOnlyList<AgentGermanWordRow> german, IReadOnlyList<AgentSourceWordRow> source, IReadOnlyList<AgentMappingRow> mappings)
    {
        var germanIds = german.ToDictionary(w => w.Id);
        var gLocal = verse.German.ToDictionary(g => g.Value.Id, g => g.Key);
        var sLocal = verse.Source.ToDictionary(s => s.Value.Id, s => s.Key);
        var hebrew = verse.Source.Values.Any(s => s.Morphemes is not null);

        static string Key(string g, string? s, string? parent, int? order, string? part, string role, string? strongs, string? partLemma, bool multiHead) =>
            string.Join('|', g, s, parent, order, part, role, strongs, partLemma, multiHead);

        var expected = verse.Fixed
            .Select(l => l.S is null
                ? Key(l.G, null, l.Parent, null, null, MappingRoles.Head, null, null, false)
                : Key(l.G, l.S, null, l.PartOrder, l.Part, l.StoredRole ?? l.Role ?? MappingRoles.Head, l.Decided ? l.StoredStrongs : l.Strongs, l.PartLemma, l.MultiHead))
            .Order(StringComparer.Ordinal)
            .ToList();

        var live = new List<string>();
        var seen = new HashSet<(int, int?, bool)>();
        foreach (var m in mappings.Where(m => germanIds.ContainsKey(m.ElbWordId) && gLocal.ContainsKey(m.ElbWordId)))
        {
            if (m.IsAddedWord)
            {
                if (m.ParentElbWordId is { } p && p != m.ElbWordId && gLocal.TryGetValue(p, out var parent) && seen.Add((m.ElbWordId, null, true)))
                {
                    live.Add(Key(gLocal[m.ElbWordId], null, parent, null, null, MappingRoles.Head, null, null, false));
                }

                continue;
            }

            if (m.StepWordId is not { } stepId || !sLocal.TryGetValue(stepId, out var s) || !seen.Add((m.ElbWordId, stepId, false)))
            {
                continue;
            }

            var hasPart = m.PartOrder is >= 1 && !string.IsNullOrWhiteSpace(m.GermanWordPart);
            live.Add(Key(
                gLocal[m.ElbWordId],
                s,
                null,
                hasPart ? m.PartOrder : null,
                hasPart ? m.GermanWordPart!.Trim() : null,
                m.Role == MappingRoles.Unit ? MappingRoles.Unit : MappingRoles.Head,
                hebrew && !string.IsNullOrWhiteSpace(m.StrongsNumber) ? m.StrongsNumber.Trim() : null,
                hasPart && !string.IsNullOrWhiteSpace(m.PartLemma) ? m.PartLemma.Trim() : null,
                m.MultiHead));
        }

        return expected.SequenceEqual(live.Order(StringComparer.Ordinal));
    }

    private static bool SameLemma(string? a, string? b) => string.Equals(a ?? string.Empty, b ?? string.Empty, StringComparison.Ordinal);

    private static (int, int?, bool, int?, int?, string?) LinkKey(VerseMappingLink l) => (l.ElbWordId, l.IsAddedWord ? null : l.StepWordId, l.IsAddedWord, l.IsAddedWord ? l.ParentElbWordId : null, l.PartOrder, string.IsNullOrWhiteSpace(l.GermanWordPart) ? null : l.GermanWordPart);

    private static List<VerseMappingLink> ParseLinks(AgentProposalRecord r) => JsonSerializer.Deserialize<List<VerseMappingLink>>(r.LinksJson, Json) ?? [];

    private static List<AgentLemmaChange> ParseLemmas(AgentProposalRecord r) => string.IsNullOrEmpty(r.LemmasJson) ? [] : JsonSerializer.Deserialize<List<AgentLemmaChange>>(r.LemmasJson, Json) ?? [];

    private static List<AgentRoleChange> ParseRoles(AgentProposalRecord r) => string.IsNullOrEmpty(r.RolesJson) ? [] : JsonSerializer.Deserialize<List<AgentRoleChange>>(r.RolesJson, Json) ?? [];

    private static List<AgentFlagDiffDto> ParseFlags(AgentProposalRecord r) =>
        AgentProposalFlagsJson.Array(r.FlagsJson) is { } array ? JsonSerializer.Deserialize<List<AgentFlagDiffDto>>(array, Json) ?? [] : [];

    /// <summary>LXX proposals (roadmap 0028.7) are confirmed on the LXX review page (0028.7f), never through the mapping merge.</summary>
    private static bool IsLxx(AgentProposalRecord r) => r.Language == Lxx.LxxAgentPackageBuilder.PackageLanguage;
}
