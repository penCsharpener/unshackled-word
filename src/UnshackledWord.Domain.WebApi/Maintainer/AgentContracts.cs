using System.Text.Json;

namespace UnshackledWord.Domain.WebApi.Maintainer;

/// <summary>Query of <c>GET api/maintainer/agent/package</c> (roadmap 0016.3): the verses <see cref="FromVerse"/>..<see cref="ToVerse"/> of one chapter.</summary>
public sealed class GetAgentPackageRequest
{
    public int Book { get; set; }
    public int Chapter { get; set; }
    public int FromVerse { get; set; }

    /// <summary>Last verse (inclusive); defaults to <see cref="FromVerse"/>.</summary>
    public int? ToVerse { get; set; }

    /// <summary><c>redo</c> (default: all links are re-decided, human-confirmed ones become hints) or <c>fill-open</c> (only words without a mapping are asked).</summary>
    public string? Mode { get; set; }

    /// <summary>Overrides the configured token budget per package (<c>AgentMapping:TokenBudget</c>).</summary>
    public int? TokenBudget { get; set; }
}

/// <summary>What the model reads: ids are local to the package (verses <c>v1</c>, German <c>g1</c>, source <c>s1</c>), rows are arrays (see 04-work-package.md).</summary>
public sealed class AgentPackageDto
{
    public string PackageId { get; set; } = string.Empty;
    public string Lang { get; set; } = string.Empty;
    public List<AgentVerseDto> Verses { get; set; } = [];
}

public sealed class AgentVerseDto
{
    public string V { get; set; } = string.Empty;
    public string Ref { get; set; } = string.Empty;

    /// <summary><c>[id, text, lemma, stts, lemmaState?, candidates?]</c>; lemmaState is <c>fixed-lemma</c> or <c>hint-lemma</c>, candidates lists further lemmas of the form.</summary>
    public List<object?[]> De { get; set; } = [];

    /// <summary><c>[id, text, lemma, strongs, gloss, morphology]</c>.</summary>
    public List<object?[]> Src { get; set; } = [];

    /// <summary><c>[german, source]</c>: decided by the algorithm, the model must not change them. Second element <c>g…</c> = added word with that parent.</summary>
    public List<string[]> Fixed { get; set; } = [];

    /// <summary>Same shape as <see cref="Fixed"/>: algorithm guesses the model may change.</summary>
    public List<string[]> Hints { get; set; } = [];

    /// <summary>
    /// Run mode <c>lemma</c> (roadmap 0018): the fixed links with their role, <c>[german, source, role, morpheme?, "open"?, partOrder?, part?, partLemma?]</c>
    /// (trailing nulls left out; <c>open</c> = the role rules could not decide it). Null in the mapping modes.
    /// </summary>
    public List<object?[]>? Links { get; set; }

    /// <summary>Run mode <c>lemma</c>: why the verse is sent (<c>roles</c>, <c>ptkvz</c>, <c>homograph</c>, <c>lemmasharp</c>, <c>lexicon</c>). Null in the mapping modes.</summary>
    public List<string>? Work { get; set; }
}

public sealed class AgentTokenStats
{
    /// <summary>German tokens that count (punctuation-only tokens are removed before).</summary>
    public int GermanTokens { get; set; }

    public int RemovedPunctuation { get; set; }
    public int Fixed { get; set; }
    public int Hint { get; set; }
    public int Open { get; set; }
    public int LemmaFixed { get; set; }
    public int LemmaHint { get; set; }
    public int LemmaOpen { get; set; }
    public int Verses { get; set; }

    /// <summary>Verses where everything is fixed (links and lemmas): not sent to the model.</summary>
    public int SkippedVerses { get; set; }

    public double FixedShare => GermanTokens == 0 ? 0 : Math.Round(100.0 * Fixed / GermanTokens, 1);
    public double HintShare => GermanTokens == 0 ? 0 : Math.Round(100.0 * Hint / GermanTokens, 1);
    public double OpenShare => GermanTokens == 0 ? 0 : Math.Round(100.0 * Open / GermanTokens, 1);
}

public sealed class AgentPackageEnvelope
{
    public AgentPackageDto Package { get; set; } = new();
    public int EstimatedTokens { get; set; }

    /// <summary>First and last verse of the package as BBCCCVVV (used by the worker to rebuild a package smaller).</summary>
    public int FromRefId { get; set; }

    public int ToRefId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public AgentTokenStats Stats { get; set; } = new();
}

public sealed class GetAgentPackageResponse
{
    public int RunId { get; set; }
    public string Mode { get; set; } = string.Empty;
    public int TokenBudget { get; set; }
    public List<AgentPackageEnvelope> Packages { get; set; } = [];
    public AgentTokenStats Stats { get; set; } = new();
}

/// <summary>Body of <c>POST api/maintainer/agent/proposal</c>: the answer JSON of 04-work-package.md ("Answer format") for one package.</summary>
public sealed class SubmitAgentProposalRequest
{
    public string PackageId { get; set; } = string.Empty;
    public JsonElement Answer { get; set; }
}

public static class AgentVerseStatus
{
    public const string Accepted = "accepted";
    public const string Rejected = "rejected";
    public const string Flagged = "flagged";
}

public sealed class AgentFlagDto
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class AgentVerseResultDto
{
    public string V { get; set; } = string.Empty;
    public string? Ref { get; set; }

    /// <see cref="AgentVerseStatus"/>
    public string Status { get; set; } = string.Empty;

    /// <summary>Hard-rule messages of a rejected verse, one problem per entry, in local ids (<c>v1 g3: source s9 belongs to v2</c>).</summary>
    public List<string> Messages { get; set; } = [];

    public List<AgentFlagDto> Flags { get; set; } = [];

    /// <summary>Id of the stored proposal; null for a rejected verse.</summary>
    public long? ProposalId { get; set; }
}

public sealed class SubmitAgentProposalResponse
{
    public string PackageId { get; set; } = string.Empty;
    public List<AgentVerseResultDto> Verses { get; set; } = [];
    public int Accepted { get; set; }
    public int Flagged { get; set; }
    public int Rejected { get; set; }

    /// <summary>Verses of the package that this answer did not mention (not judged, not stored); send them in a later submission with the same package.</summary>
    public List<string> NotAnswered { get; set; } = [];

    /// <summary>Older open proposals of the same verses that this submission replaced.</summary>
    public int Superseded { get; set; }
}

/// <summary>Query of <c>GET api/maintainer/agent/example</c>: turns one stored proposal into a worked example for the prompt files.</summary>
public sealed class GetAgentExampleRequest
{
    /// <summary>Id of the proposal (<c>AgentVerseProposals.Id</c>).</summary>
    public long Id { get; set; }

    /// <summary><c>redo</c> (default) or <c>fill-open</c>; the mode the example package is built in. Lemma mode is not supported.</summary>
    public string? Mode { get; set; }

    /// <summary>Prompt version the example is for (<c>hebrew-v2</c>); default <c>{language}-v2</c>. From version 2 on links carry role and morpheme.</summary>
    public string? PromptVersion { get; set; }

    /// <summary>True when the maintainer has confirmed the example: the Markdown heading then has no <c>[UNCONFIRMED]</c> tag.</summary>
    public bool Confirmed { get; set; }
}

/// <summary>A worked example for <c>assets/agent-prompts/*.md</c>: package and answer as compact JSON plus a ready-to-paste Markdown block.</summary>
public sealed class AgentExampleResponse
{
    public long ProposalId { get; set; }
    public string ProposalStatus { get; set; } = string.Empty;
    public string Ref { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = string.Empty;
    public string PackageJson { get; set; } = string.Empty;
    public string AnswerJson { get; set; } = string.Empty;

    /// <summary><c>## Example: ref</c> heading (with <c>[UNCONFIRMED]</c> unless <c>confirmed=true</c>), the <c>Package:</c> and <c>Answer:</c> blocks; the "What to notice" text is yours to write.</summary>
    public string Markdown { get; set; } = string.Empty;

    /// <summary>Things to check before pasting (proposal already merged, <c>TODO</c> reasons for unmapped words, unknown ids).</summary>
    public List<string> Warnings { get; set; } = [];
}
