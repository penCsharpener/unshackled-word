using System.Text.Json;
using Microsoft.Extensions.Logging;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Application.Features.AgentMapping.Lxx;

/// <summary>The submit path of <c>lxx-tag</c> packages; <see cref="AgentProposalService"/> hands those packages here after the expiry check.</summary>
public interface ILxxProposalSubmitter
{
    Task<AgentSubmitResult> SubmitAsync(Guid packageId, AgentPackageRecord package, AgentPackageMap map, JsonElement answer, CancellationToken token = default);
}

/// <summary>
/// Stale check (LXX and Hebrew word ids per unit), answer parse, the <c>lxx</c> validator branch and storage in <c>AgentVerseProposals</c>
/// (<c>Language = 'lxx'</c>, word tags in <c>Lemmas</c>, Hebrew links in <c>Links</c>, <c>Flags</c> an <see cref="LxxProposalFlags"/> object), keyed by the
/// Rahlfs book/chapter/verse plus text form and subverse. Never writes <c>LxxWordTags</c>: the maintainer confirms on the review page (0028.7f).
/// </summary>
public sealed class LxxProposalSubmitter : ILxxProposalSubmitter
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    private readonly ILxxAgentDataRepository _data;
    private readonly IAgentStoreRepository _store;
    private readonly ILogger<LxxProposalSubmitter> _logger;
    private readonly AgentProposalValidator _validator = new();
    private LxxLexicon? _lexicon;

    public LxxProposalSubmitter(ILxxAgentDataRepository data, IAgentStoreRepository store, ILogger<LxxProposalSubmitter> logger)
    {
        _data = data;
        _store = store;
        _logger = logger;
    }

    public async Task<AgentSubmitResult> SubmitAsync(Guid packageId, AgentPackageRecord package, AgentPackageMap map, JsonElement answerJson, CancellationToken token = default)
    {
        var units = map.LxxVerses ?? [];
        var stale = package.Status == AgentPackageStatuses.Stale ? ["the package was already marked stale"] : await FindStaleAsync(units, package.WordIdsHash, token);
        if (stale.Count > 0)
        {
            await _store.SetPackageStatusAsync(packageId, AgentPackageStatuses.Stale, false, token);
            _logger.LogWarning("LXX agent package {PackageId} is stale: {Verses}", packageId, string.Join("; ", stale));

            return new AgentSubmitResult { Outcome = AgentSubmitOutcome.Stale, Errors = [.. stale.Select(s => $"{s}; the text changed since the package was built, build a new package.")] };
        }

        var answer = LxxAgentAnswerParser.Parse(answerJson);
        if (answer.PackageId is not null && !string.Equals(answer.PackageId, packageId.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            answer.Errors.Add($"the answer names package '{answer.PackageId}' but was submitted for '{packageId}'.");
        }

        if (answer.Errors.Count > 0)
        {
            return new AgentSubmitResult { Outcome = AgentSubmitOutcome.Invalid, Errors = answer.Errors };
        }

        var lexicon = _lexicon ??= LxxLexicon.Build(await _data.GetLexiconAsync(token));
        var results = _validator.ValidateLxx(map, answer, lexicon);
        var stored = results.Where(r => r.Status != AgentVerseStatus.Rejected && r.Proposal is not null).ToList();
        var ids = new Dictionary<string, long>();
        var superseded = 0;
        if (stored.Count > 0)
        {
            var result = await _store.StoreProposalsAsync(package.RunId, packageId, stored.Select(r => ToInsert(map, r)).ToList(), token);
            superseded = result.Superseded;
            for (var i = 0; i < stored.Count; i++)
            {
                ids[stored[i].V] = result.ProposalIds[i];
            }
        }

        var response = new SubmitAgentProposalResponse
        {
            PackageId = packageId.ToString(),
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
                ProposalId = ids.TryGetValue(r.V, out var id) ? id : null,
            });
        }

        response.NotAnswered = units.Keys.Where(v => answer.Verses.All(a => a.V != v)).OrderBy(v => v.Length).ThenBy(v => v, StringComparer.Ordinal).ToList();
        var proposed = await _store.CountProposedVersesAsync(packageId, token);
        await _store.SetPackageStatusAsync(packageId, proposed >= units.Count ? AgentPackageStatuses.Completed : AgentPackageStatuses.Submitted, true, token);
        _logger.LogInformation("LXX agent package {PackageId}: {Accepted} accepted, {Flagged} flagged ({Dropped} words dropped), {Rejected} rejected, {Superseded} superseded",
            packageId, response.Accepted, response.Flagged, stored.Sum(r => r.Flags.Count(f => LxxFlagCodes.Drops(f.Code))), response.Rejected, superseded);

        return new AgentSubmitResult { Outcome = AgentSubmitOutcome.Ok, Response = response };
    }

    public static AgentProposalInsert ToInsert(AgentPackageMap map, LxxVerseValidation result)
    {
        var proposal = result.Proposal!;
        var unit = proposal.Unit;
        var flags = new LxxProposalFlags { TextForm = unit.TextForm, Subverse = unit.Subverse, LxxVerseId = unit.LxxVerseId, HebRefId = unit.HebRefId, Flags = result.Flags };

        return new AgentProposalInsert
        {
            BibleBookId = map.BookId,
            Chapter = unit.Chapter,
            Verse = unit.Verse,
            Language = LxxAgentPackageBuilder.PackageLanguage,
            TextForm = unit.TextForm,
            Subverse = unit.Subverse,
            LinksJson = JsonSerializer.Serialize(proposal.Links, Json),
            LemmasJson = JsonSerializer.Serialize(proposal.Words, Json),
            FlagsJson = JsonSerializer.Serialize(flags, Json),
            Note = proposal.Note,
        };
    }

    private async Task<List<string>> FindStaleAsync(Dictionary<string, LxxVerseMap> units, string storedHash, CancellationToken token)
    {
        var live = LxxAgentPackageBuilder.Units(await _data.GetUnitsAsync(units.Values.Select(u => u.LxxVerseId).Distinct().ToList(), token)).ToDictionary(d => d.Unit.Id);
        var stale = new List<string>();
        var hashes = new List<string>();
        foreach (var (v, unit) in units.OrderBy(k => k.Key.Length).ThenBy(k => k.Key, StringComparer.Ordinal))
        {
            var hash = live.TryGetValue(unit.LxxVerseId, out var draft) ? draft.Hash : string.Empty;
            hashes.Add(hash);
            if (hash != unit.Hash)
            {
                stale.Add($"{v} ({unit.Ref}) has other words than in the package");
            }
        }

        if (stale.Count == 0 && AgentPackageBuilder.PackageHash(hashes) != storedHash)
        {
            stale.Add("the word ids of the package verses do not match the stored package hash");
        }

        return stale;
    }
}
