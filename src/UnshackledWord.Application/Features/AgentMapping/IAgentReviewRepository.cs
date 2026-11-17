using UnshackledWord.Domain.WebApi.Lemmas;

namespace UnshackledWord.Application.Features.AgentMapping;

/// <summary>A row of <c>AgentVerseProposals</c> with the run facts a review needs (implemented in Infrastructure).</summary>
public sealed class AgentProposalRecord
{
    public long Id { get; set; }
    public int RunId { get; set; }
    public Guid PackageId { get; set; }
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public string Language { get; set; } = string.Empty;
    public string LinksJson { get; set; } = "[]";
    public string? LemmasJson { get; set; }
    public string? PartsJson { get; set; }

    /// <summary>Run mode <c>lemma</c>: <see cref="AgentRoleChange"/> list (roadmap 0018).</summary>
    public string? RolesJson { get; set; }

    public string? FlagsJson { get; set; }
    public string? Note { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }

    /// <summary>The AI user of the run: the author of everything a merge writes.</summary>
    public int AiUserId { get; set; }

    public string RunProfile { get; set; } = string.Empty;
    public string RunModel { get; set; } = string.Empty;
}

public interface IAgentReviewRepository
{
    /// <summary>Proposals of a run in reading order; <paramref name="status"/> null = all.</summary>
    Task<IReadOnlyList<AgentProposalRecord>> ListAsync(int runId, string? status, CancellationToken token = default);

    Task<AgentProposalRecord?> GetAsync(long id, CancellationToken token = default);

    /// <summary>Moves an <c>open</c> proposal to <paramref name="status"/> and records the reviewer; false when it was not open any more (somebody else was faster).</summary>
    Task<bool> TryCloseAsync(long id, string status, int reviewerUserId, CancellationToken token = default);
}

/// <summary>
/// What a merge needs from the German lemma editor (<c>ILemmaRepository</c> lives in the WebApi project): the current rows of a verse, the STTS check
/// the editor applies, and the audited write. Implemented in Tooling.WebApi.
/// </summary>
public interface IAgentLemmaWriter
{
    Task<IReadOnlyList<LemmaWordRow>> GetVerseAsync(int bibleBookId, int chapter, int verse, CancellationToken token = default);

    /// <summary>Messages of the editor's STTS guard (tag exists and fits the part of speech); empty when the changes are acceptable.</summary>
    Task<IReadOnlyList<string>> CheckAsync(IReadOnlyList<LemmaWordChange> changes, CancellationToken token = default);

    /// <summary>
    /// One transaction, one audit row per word in <c>Elb1871MorphologyEdits</c> with <paramref name="authorUserId"/> as <c>UserId</c> and
    /// <paramref name="reviewerUserId"/> as <c>ReviewedByUserId</c>. Returns the word ids that do not exist (then nothing was written).
    /// </summary>
    Task<IReadOnlyList<int>> ApplyAsync(IReadOnlyList<LemmaWordChange> changes, int authorUserId, int reviewerUserId, CancellationToken token = default);
}
