using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.AgentMapping.Lxx;

namespace UnshackledWord.Application.Features.Lxx.Review;

/// <summary>Open review work of one Rahlfs chapter (roadmap 0028.7f).</summary>
public sealed class LxxReviewChapterCountRow
{
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int OpenUnits { get; set; }
    public int AiUnits { get; set; }
    public int LowWords { get; set; }
}

/// <summary>A stored <c>LxxHebrewLinks</c> row.</summary>
public sealed class LxxReviewLinkRow
{
    public int LxxWordId { get; set; }
    public int StepHebrewWordId { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>An open LXX proposal (<c>AgentVerseProposals</c>, language <c>lxx</c>) with its raw JSON columns.</summary>
public sealed class LxxReviewProposalRow
{
    public long Id { get; set; }
    public int RunId { get; set; }
    public int LxxVerseId { get; set; }
    public string? LemmasJson { get; set; }
    public string? LinksJson { get; set; }
    public string? FlagsJson { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Units with their words (current tag), stored links, Hebrew words of their <c>HebRefId</c>s and open AI proposals.</summary>
public sealed class LxxReviewState
{
    public IReadOnlyList<LxxAgentUnitRow> Units { get; init; } = [];
    public IReadOnlyList<LxxAgentWordRow> Words { get; init; } = [];
    public IReadOnlyList<AgentSourceWordRow> HebrewWords { get; init; } = [];
    public IReadOnlyList<LxxReviewLinkRow> Links { get; init; } = [];
    public IReadOnlyList<LxxReviewProposalRow> Proposals { get; init; } = [];
}

/// <summary>A confirmed tag to upsert (<c>Status = confirmed</c>, user = the reviewer).</summary>
public sealed record LxxTagWrite(int LxxWordId, string Lemma, string Strongs, string Morphology, string Source);

/// <summary>One <c>LxxTagAudit</c> row; JSON already serialized.</summary>
public sealed record LxxAuditWrite(int LxxVerseId, string Action, string? BeforeJson, string AfterJson);

/// <summary>An AI proposal to close (<c>merged</c>, <c>edited-merged</c>, <c>discarded</c>); only an open one is closed.</summary>
public sealed record LxxProposalClose(long Id, string Status);

/// <summary>Everything one review action writes, in one transaction.</summary>
public sealed class LxxReviewWrite
{
    public List<LxxTagWrite> Tags { get; } = [];

    /// <summary>Words whose links are replaced by <see cref="Links"/> (confirmed).</summary>
    public List<int> LinkWordIds { get; } = [];

    public List<(int LxxWordId, int StepHebrewWordId)> Links { get; } = [];
    public List<LxxAuditWrite> Audits { get; } = [];
    public List<LxxProposalClose> ProposalCloses { get; } = [];

    public bool IsEmpty => Tags.Count == 0 && LinkWordIds.Count == 0 && Audits.Count == 0 && ProposalCloses.Count == 0;
}

/// <summary>What the plan callback decided: <see cref="Write"/> null writes nothing (the transaction is rolled back).</summary>
public sealed record LxxReviewPlan<T>(T Result, LxxReviewWrite? Write);

/// <summary><see cref="Conflict"/>: a proposal to close was no longer open, nothing was written.</summary>
public sealed record LxxReviewApplied<T>(T Result, bool Conflict);

/// <summary>Reads and the one write path of the LXX review page (roadmap 0028.7f), implemented in Infrastructure.</summary>
public interface ILxxReviewRepository
{
    /// <summary>Per book and Rahlfs chapter: units with a proposed tag or an open AI proposal; chapters without open work are absent.</summary>
    Task<IReadOnlyList<LxxReviewChapterCountRow>> GetChapterCountsAsync(CancellationToken token = default);

    /// <summary>Every unit of one Rahlfs chapter (all subverses, additions and Daniel text forms), Swete order.</summary>
    Task<LxxReviewState> GetChapterAsync(int bibleBookId, int chapter, CancellationToken token = default);

    /// <summary>
    /// In one transaction: locks the units, reads their state (as <see cref="GetChapterAsync"/> does), lets <paramref name="plan"/> decide and writes
    /// its <see cref="LxxReviewPlan{T}.Write"/> (tags, links, audit rows, proposal closes) with <paramref name="userId"/>.
    /// </summary>
    Task<LxxReviewApplied<T>> ApplyAsync<T>(IReadOnlyCollection<int> lxxVerseIds, Func<LxxReviewState, LxxReviewPlan<T>> plan, int userId, CancellationToken token = default);
}

/// <summary>The Greek TBESG lexicon, loaded once per process (it only changes with a SeedDb re-import).</summary>
public interface ILxxLexiconSource
{
    Task<LxxLexicon> GetAsync(CancellationToken token = default);
}
