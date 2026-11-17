using System.Text.Json;
using Microsoft.Extensions.Logging;
using UnshackledWord.Application.Features.Lemmas;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Application.Features.AgentMapping;

public enum AgentSubmitOutcome
{
    Ok,
    NotFound,

    /// <summary>The package is older than its expiry (or was marked expired).</summary>
    Expired,

    /// <summary>The text of a package verse changed since the package was built; build a new package.</summary>
    Stale,

    /// <summary>The answer is unusable as a whole (not JSON of the expected shape, other package id).</summary>
    Invalid,
}

public sealed class AgentSubmitResult
{
    public AgentSubmitOutcome Outcome { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
    public SubmitAgentProposalResponse? Response { get; init; }
}

public interface IAgentProposalService
{
    /// <summary>
    /// Binds the answer to the package it was built for (expiry, word-id hash), validates every verse and stores accepted/flagged ones as open
    /// proposals. Rejected verses can be resubmitted with the same package until it expires.
    /// </summary>
    Task<AgentSubmitResult> SubmitAsync(string packageId, JsonElement answer, CancellationToken token = default);
}

public sealed class AgentProposalService : IAgentProposalService
{
    private readonly IAgentStoreRepository _store;
    private readonly IAgentDataRepository _data;
    private readonly ISttsTagRepository _sttsTags;
    private readonly AgentProposalStore _proposals;
    private readonly AgentProposalValidator _validator = new();
    private readonly AgentMappingOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<AgentProposalService> _logger;

    public AgentProposalService(IAgentStoreRepository store, IAgentDataRepository data, ISttsTagRepository sttsTags, AgentProposalStore proposals, Microsoft.Extensions.Configuration.IConfiguration configuration, TimeProvider time, ILogger<AgentProposalService> logger)
    {
        _store = store;
        _data = data;
        _sttsTags = sttsTags;
        _proposals = proposals;
        _options = AgentMappingOptions.From(configuration);
        _time = time;
        _logger = logger;
    }

    public async Task<AgentSubmitResult> SubmitAsync(string packageId, JsonElement answerJson, CancellationToken token = default)
    {
        if (!Guid.TryParse(packageId, out var id) || await _store.GetPackageAsync(id, token) is not { } package)
        {
            return new AgentSubmitResult { Outcome = AgentSubmitOutcome.NotFound, Errors = [$"packageId '{packageId}' does not exist."] };
        }

        if (package.Status == AgentPackageStatuses.Expired || package.ExpiresAt.ToUniversalTime() <= _time.GetUtcNow().UtcDateTime)
        {
            if (package.Status != AgentPackageStatuses.Expired)
            {
                await _store.SetPackageStatusAsync(id, AgentPackageStatuses.Expired, false, token);
            }

            return new AgentSubmitResult { Outcome = AgentSubmitOutcome.Expired, Errors = [$"The package expired at {package.ExpiresAt.ToUniversalTime():u}; build a new one."] };
        }

        var map = AgentPackageMap.FromJson(package.LocalIdMap);

        var staleVerses = package.Status == AgentPackageStatuses.Stale ? ["the package was already marked stale"] : await FindStaleVersesAsync(map, package.WordIdsHash, token);
        if (staleVerses.Count > 0)
        {
            await _store.SetPackageStatusAsync(id, AgentPackageStatuses.Stale, false, token);
            _logger.LogWarning("Agent package {PackageId} is stale: {Verses}", id, string.Join("; ", staleVerses));

            return new AgentSubmitResult { Outcome = AgentSubmitOutcome.Stale, Errors = [.. staleVerses.Select(s => $"{s}; the text changed since the package was built, build a new package.")] };
        }

        var answer = AgentAnswerParser.Parse(answerJson);
        if (answer.PackageId is not null && !string.Equals(answer.PackageId, packageId, StringComparison.OrdinalIgnoreCase))
        {
            answer.Errors.Add($"the answer names package '{answer.PackageId}' but was submitted for '{packageId}'.");
        }

        if (answer.Errors.Count > 0)
        {
            return new AgentSubmitResult { Outcome = AgentSubmitOutcome.Invalid, Errors = answer.Errors };
        }

        var context = await LoadContextAsync(map, answer, token);
        var results = _validator.Validate(map, answer, context);
        var (ids, superseded) = await _proposals.StoreAsync(package.RunId, id, map, results, token);

        var response = new SubmitAgentProposalResponse
        {
            PackageId = packageId,
            Superseded = superseded,
            Accepted = results.Count(r => r.Status == AgentVerseStatus.Accepted),
            Flagged = results.Count(r => r.Status == AgentVerseStatus.Flagged),
            Rejected = results.Count(r => r.Status == AgentVerseStatus.Rejected),
        };

        foreach (var r in results)
        {
            response.Verses.Add(new AgentVerseResultDto
            {
                V = r.V,
                Ref = r.Ref,
                Status = r.Status,
                Messages = r.Messages,
                Flags = r.Flags.Select(f => new AgentFlagDto { Code = f.Code, Message = f.Message }).ToList(),
                ProposalId = ids.TryGetValue(r.V, out var proposalId) ? proposalId : null,
            });
        }

        response.NotAnswered = map.Verses.Keys.Where(v => answer.Verses.All(a => a.V != v)).OrderBy(v => v.Length).ThenBy(v => v, StringComparer.Ordinal).ToList();
        var proposed = await _store.CountProposedVersesAsync(id, token);
        await _store.SetPackageStatusAsync(id, proposed >= map.Verses.Count ? AgentPackageStatuses.Completed : AgentPackageStatuses.Submitted, true, token);
        _logger.LogInformation("Agent package {PackageId}: {Accepted} accepted, {Flagged} flagged, {Rejected} rejected, {Superseded} superseded", id, response.Accepted, response.Flagged, response.Rejected, superseded);

        return new AgentSubmitResult { Outcome = AgentSubmitOutcome.Ok, Response = response };
    }

    /// <summary>Compares the word-id hash of every package verse with the live verse; one message per verse that changed or disappeared.</summary>
    private async Task<List<string>> FindStaleVersesAsync(AgentPackageMap map, string storedHash, CancellationToken token)
    {
        var verses = map.Verses.Values.ToList();
        var first = verses.OrderBy(v => v.Chapter).ThenBy(v => v.Verse).First();
        var last = verses.OrderByDescending(v => v.Chapter).ThenByDescending(v => v.Verse).First();
        var live = await _data.GetRangeAsync(map.BookId, first.Chapter, first.Verse, last.Chapter, last.Verse, token);
        var stale = new List<string>();
        var liveHashes = new List<string>();

        foreach (var (v, verse) in map.Verses.OrderBy(k => k.Key.Length).ThenBy(k => k.Key, StringComparer.Ordinal))
        {
            var german = live.GermanWords.Where(w => w.Chapter == verse.Chapter && w.Verse == verse.Verse).ToList();
            var refIds = german.Select(w => w.HebRefId).ToHashSet();
            var source = live.SourceWords.Where(s => refIds.Contains(s.RefId)).ToList();
            var hash = german.Count == 0 ? string.Empty : AgentPackageBuilder.VerseHash(german, source);
            liveHashes.Add(hash);

            if (hash != verse.Hash)
            {
                stale.Add($"{v} ({verse.Ref}) has other words than in the package");
            }
        }

        // The stored package hash is the binding of record; the per-verse hashes only say which verse changed.
        if (stale.Count == 0 && AgentPackageBuilder.PackageHash(liveHashes) != storedHash)
        {
            stale.Add("the word ids of the package verses do not match the stored package hash");
        }

        return stale;
    }

    private async Task<AgentValidationContext> LoadContextAsync(AgentPackageMap map, AgentAnswer answer, CancellationToken token)
    {
        var lemmaEntries = answer.Verses.SelectMany(a => a.Lemmas.Select(l => (Verse: a.V, Entry: l))).ToList();
        var forms = lemmaEntries
            .Select(x => map.Verses.TryGetValue(x.Verse, out var verse) && verse.German.TryGetValue(x.Entry.G, out var word) ? word.Text.ToLowerInvariant() : null)
            .Where(f => f is not null)
            .Cast<string>()
            .Distinct()
            .ToList();

        var known = lemmaEntries.Count == 0 ? new HashSet<string>() : (await _data.GetKnownLemmasAsync(lemmaEntries.Select(x => x.Entry.Lemma.Trim()).Distinct().ToList(), token)).ToHashSet();
        var lexicon = LemmaLexicon.Build(forms.Count == 0 ? [] : await _data.GetFormLemmasAsync(forms, token));
        var sttsTags = lemmaEntries.Any(x => x.Entry.Stts is not null) ? await _sttsTags.GetAllAsync(token) : [];

        var verseLemmas = map.Verses.Values.SelectMany(v => v.German.Values).Select(g => g.Lemma).Where(l => !string.IsNullOrEmpty(l)).Select(l => l!.ToLowerInvariant()).Distinct().ToList();
        var stats = new ConfirmedStats(await _data.GetConfirmedPairsAsync(map.BookId <= 39, verseLemmas, token));

        return new AgentValidationContext
        {
            Stts = sttsTags.Count == 0 ? null : new SttsCatalog(sttsTags),
            KnownLemmas = known,
            HumanLemmas = lexicon.HumanLemmas,
            Stats = stats,
            Options = _options,
        };
    }
}
