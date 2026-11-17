using System.Text.Json;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Application.Features.AgentMapping;

/// <summary>
/// Writes accepted and flagged verses to <c>AgentVerseProposals</c> (status open, real ids); an older open proposal of the same verse is superseded.
/// Rejected verses are not stored. Nothing here touches live mapping or lemma data.
/// </summary>
public sealed class AgentProposalStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    private readonly IAgentStoreRepository _repository;

    public AgentProposalStore(IAgentStoreRepository repository)
    {
        _repository = repository;
    }

    /// <summary>Stores every verse that has a proposal; returns the verses in the same order with their proposal id and the number of superseded proposals.</summary>
    public async Task<(IReadOnlyDictionary<string, long> ProposalIds, int Superseded)> StoreAsync(int runId, Guid packageId, AgentPackageMap package, IReadOnlyList<AgentVerseValidation> results, CancellationToken token = default)
    {
        var stored = results.Where(r => r.Status != AgentVerseStatus.Rejected && r.Proposal is not null).ToList();
        if (stored.Count == 0)
        {
            return (new Dictionary<string, long>(), 0);
        }

        var inserts = stored.Select(r => ToInsert(package, r)).ToList();
        var result = await _repository.StoreProposalsAsync(runId, packageId, inserts, token);
        var ids = stored.Select((r, i) => (r.V, Id: result.ProposalIds[i])).ToDictionary(x => x.V, x => x.Id);

        return (ids, result.Superseded);
    }

    public static AgentProposalInsert ToInsert(AgentPackageMap package, AgentVerseValidation result)
    {
        var proposal = result.Proposal!;
        return new AgentProposalInsert
        {
            BibleBookId = package.BookId,
            Chapter = proposal.Chapter,
            Verse = proposal.Verse,
            Language = package.Language,
            LinksJson = JsonSerializer.Serialize(proposal.Links, Json),
            LemmasJson = proposal.Lemmas.Count == 0 ? null : JsonSerializer.Serialize(proposal.Lemmas, Json),
            PartsJson = proposal.Parts.Count == 0 ? null : JsonSerializer.Serialize(proposal.Parts, Json),
            RolesJson = proposal.Roles.Count == 0 ? null : JsonSerializer.Serialize(proposal.Roles, Json),
            FlagsJson = result.Flags.Count == 0 ? null : JsonSerializer.Serialize(result.Flags, Json),
            Note = proposal.Note,
        };
    }
}
