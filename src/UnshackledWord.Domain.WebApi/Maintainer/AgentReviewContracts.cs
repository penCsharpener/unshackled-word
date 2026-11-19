using UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping;

namespace UnshackledWord.Domain.WebApi.Maintainer;

/// <summary>Status values of <c>AgentVerseProposals</c> (roadmap 0016.7).</summary>
public static class AgentProposalStatuses
{
    public const string Open = "open";
    public const string Merged = "merged";
    public const string EditedMerged = "edited-merged";
    public const string Discarded = "discarded";
    public const string Superseded = "superseded";

    public static readonly IReadOnlyList<string> All = [Open, Merged, EditedMerged, Discarded, Superseded];
}

public sealed class ListAgentProposalsRequest
{
    public int RunId { get; set; }

    /// <summary>One of open, merged, edited-merged, discarded, superseded; all when empty.</summary>
    public string? Status { get; set; }

    /// <summary>Only proposals with at least one soft-rule flag.</summary>
    public bool FlaggedOnly { get; set; }
}

public sealed class AgentProposalSummaryDto
{
    public long Id { get; set; }
    public int RunId { get; set; }
    public int Book { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public string Status { get; set; } = string.Empty;
    public int FlagCount { get; set; }
    public bool HasLemmaChanges { get; set; }

    /// <summary>Run mode <c>lemma</c>: the proposal changes link roles, morphemes, part lemmas or turns a pronoun into an added word.</summary>
    public bool HasRoleChanges { get; set; }

    public bool HasParts { get; set; }
    public bool HasNote { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
}

public sealed class ListAgentProposalsResponse
{
    public int RunId { get; set; }
    public List<AgentProposalSummaryDto> Proposals { get; set; } = [];
}

public sealed class AgentProposalIdRequest
{
    public long Id { get; set; }
}

/// <summary>A link of the proposal compared with the live mapping; the German and the original word are included so a review needs no second request.</summary>
public sealed class AgentLinkDiffDto
{
    public int ElbWordId { get; set; }
    public string German { get; set; } = string.Empty;
    public int? StepWordId { get; set; }
    public string? Original { get; set; }
    public bool IsAddedWord { get; set; }
    public int? ParentElbWordId { get; set; }
    public string? ParentGerman { get; set; }
    public int? PartOrder { get; set; }
    public string? GermanWordPart { get; set; }
}

public sealed class AgentLemmaDiffDto
{
    public int ElbWordId { get; set; }
    public string German { get; set; } = string.Empty;

    /// <summary>Lemma and tag as they are live now.</summary>
    public string? CurrentLemma { get; set; }

    public string? CurrentStts { get; set; }

    /// <summary>What the proposal was built against; differs from <see cref="CurrentLemma"/> when somebody edited the word since.</summary>
    public string? BeforeLemma { get; set; }

    public string Lemma { get; set; } = string.Empty;
    public string? Stts { get; set; }
    public string? PartOfSpeech { get; set; }

    /// <summary>The live lemma is not the one the proposal started from (a human edit since): the plain merge is refused, an edit-merge may still write it.</summary>
    public bool Changed { get; set; }
}

/// <summary>One German word of the verse with its live lemma, so a review can spot a wrong lemma the proposal left alone.</summary>
public sealed class AgentVerseLemmaDto
{
    public int ElbWordId { get; set; }
    public string German { get; set; } = string.Empty;
    public string? Lemma { get; set; }
    public string? Stts { get; set; }

    /// <summary>The proposal changes this word's lemma (it is also in <see cref="AgentProposalDiffResponse.LemmaChanges"/>).</summary>
    public bool Proposed { get; set; }
}

/// <summary>One link change of a lemma-mode proposal; <c>Before*</c> = stored when the package was built, <c>Current*</c> = live now.</summary>
public sealed class AgentRoleDiffDto
{
    public int ElbWordId { get; set; }
    public string German { get; set; } = string.Empty;
    public int? StepWordId { get; set; }
    public string? Original { get; set; }
    public string? Role { get; set; }
    public string? StrongsNumber { get; set; }
    public bool MultiHead { get; set; }
    public int? PartOrder { get; set; }
    public string? PartLemma { get; set; }
    public string? BeforeRole { get; set; }
    public string? BeforeStrongsNumber { get; set; }
    public bool BeforeMultiHead { get; set; }
    public string? BeforePartLemma { get; set; }

    /// <summary>Set when the linked word becomes an added word with this parent.</summary>
    public int? AddedParentElbWordId { get; set; }

    public string? AddedParentGerman { get; set; }

    /// <summary><c>model</c> or <c>rules</c> (filled in by the deterministic role rules).</summary>
    public string Source { get; set; } = "model";
}

public sealed class AgentFlagDiffDto
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int? ElbWordId { get; set; }
    public int? StepWordId { get; set; }
}

/// <summary>One proposed verse against the live verse (roadmap 0016.7).</summary>
public sealed class AgentProposalDiffResponse
{
    public long Id { get; set; }
    public int RunId { get; set; }
    public string Profile { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;

    /// <summary>The AI user the merge writes as author.</summary>
    public int AiUserId { get; set; }

    public int Book { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public string Language { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Note { get; set; }
    public int? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }

    /// <summary>The words of the verse changed since the package was built: the proposal cannot be merged ("veraltet").</summary>
    public bool Stale { get; set; }

    public string? StaleReason { get; set; }

    public List<VerseMappingGermanWord> GermanWords { get; set; } = [];
    public List<VerseMappingOriginalWord> OriginalWords { get; set; } = [];

    /// <summary>Links in both the live mapping and the proposal (compared by German word, original word, added flag, parent and part).</summary>
    public List<AgentLinkDiffDto> Unchanged { get; set; } = [];

    /// <summary>Links only the proposal has (new links, new added words, new or changed parts).</summary>
    public List<AgentLinkDiffDto> Added { get; set; } = [];

    /// <summary>Links only the live mapping has.</summary>
    public List<AgentLinkDiffDto> Removed { get; set; } = [];

    /// <summary>The complete proposed link set, ready to load into an editor for an edit-merge.</summary>
    public List<VerseMappingLink> ProposedLinks { get; set; } = [];

    /// <summary>The live link set with its role fields (roadmap 0018.8f): the editor's base, so a role-aware edit-merge compares against the real roles.</summary>
    public List<VerseMappingLink> CurrentLinks { get; set; } = [];

    public List<AgentLemmaDiffDto> LemmaChanges { get; set; } = [];

    /// <summary>Every word of the verse in reading order with its live lemma; the words in <see cref="LemmaChanges"/> are marked.</summary>
    public List<AgentVerseLemmaDto> VerseLemmas { get; set; } = [];

    /// <summary>Run mode <c>lemma</c> (roadmap 0018): role, morpheme, multi-head and part-lemma changes of links, and links that become added words.</summary>
    public List<AgentRoleDiffDto> RoleChanges { get; set; } = [];
    public List<AgentFlagDiffDto> Flags { get; set; } = [];
}

/// <summary>An edited lemma for an edit-merge: the new lemma and optional STTS tag / part of speech of one German word of the verse.</summary>
public sealed class AgentLemmaEditDto
{
    public int ElbWordId { get; set; }
    public string Lemma { get; set; } = string.Empty;
    public string? Stts { get; set; }
    public string? PartOfSpeech { get; set; }
}

/// <summary>Edit-merge: the maintainer's edited mapping and lemmas replace the proposal's; the same hard rules as a plain merge apply.</summary>
public sealed class EditMergeAgentProposalRequest
{
    public long Id { get; set; }
    public List<VerseMappingLink> Links { get; set; } = [];
    public List<AgentLemmaEditDto> Lemmas { get; set; } = [];

    /// <summary>Allows the save to drop compound parts the live verse has today.</summary>
    public bool ClearParts { get; set; }
}

public sealed class MergeAgentProposalResponse
{
    public long Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public int AuthorUserId { get; set; }
    public int ReviewerUserId { get; set; }
    public int LinksWritten { get; set; }
    public int LemmasWritten { get; set; }
}

public sealed class DiscardAgentProposalResponse
{
    public long Id { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class BulkMergeAgentProposalsRequest
{
    public int RunId { get; set; }

    /// <summary>Must be true: merging a whole run changes the live mapping of every unflagged verse.</summary>
    public bool Confirm { get; set; }
}

public sealed class BulkMergeFailureDto
{
    public long Id { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }

    /// <summary>stale, invalid or forbidden.</summary>
    public string Reason { get; set; } = string.Empty;

    public List<string> Messages { get; set; } = [];
}

public sealed class BulkMergeAgentProposalsResponse
{
    public int RunId { get; set; }
    public int Merged { get; set; }

    /// <summary>Open proposals that were not merged because they carry flags (left open for review).</summary>
    public int SkippedFlagged { get; set; }

    public List<BulkMergeFailureDto> Failed { get; set; } = [];
}

/// <summary>MCP token management (roadmap 0016.8).</summary>
public sealed class CreateApiTokenRequest
{
    /// <summary>The user the token acts as (role ai, maintainer or admin); proposals made with it are attributed to this user. Send either this or <see cref="Model"/>.</summary>
    public int UserId { get; set; }

    /// <summary>Alternative to <see cref="UserId"/>: the model name; the token acts as the user <c>ai:&lt;model&gt;</c>, which is created on first use.</summary>
    public string? Model { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Days until the token expires (default 30, at most 365).</summary>
    public int? ExpiresInDays { get; set; }
}

public sealed class ApiTokenDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>First characters of the token, to tell tokens apart; never enough to use it.</summary>
    public string Prefix { get; set; } = string.Empty;

    public string Scope { get; set; } = string.Empty;
    public int CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public bool Active { get; set; }
}

public sealed class CreateApiTokenResponse
{
    public ApiTokenDto Token { get; set; } = new();

    /// <summary>The token itself, shown only now; only its hash is stored.</summary>
    public string Secret { get; set; } = string.Empty;
}

/// <summary>A user a token can be issued for.</summary>
public sealed class ApiTokenUserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = [];
}

public sealed class ListApiTokenUsersResponse
{
    /// <summary>Users with role ai, maintainer or admin; <c>ai:&lt;model&gt;</c> users first.</summary>
    public List<ApiTokenUserDto> Users { get; set; } = [];
}

public sealed class ListApiTokensResponse
{
    public List<ApiTokenDto> Tokens { get; set; } = [];
}

public sealed class ApiTokenIdRequest
{
    public int Id { get; set; }
}
