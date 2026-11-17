namespace UnshackledWord.Domain.WebApi.Maintainer;

/// <summary>GET <c>maintainer/agent-runs/{runId}/score</c>; <see cref="Reference"/> picks a reference file (<c>reference-v1</c>), default the newest.</summary>
public sealed class AgentRunScoreRequest
{
    public int RunId { get; set; }
    public string? Reference { get; set; }
}

/// <summary>One accuracy figure: <see cref="Accuracy"/> is null (omitted) while <see cref="Total"/> is 0.</summary>
public sealed class AgentScoreRatio
{
    public int Total { get; set; }
    public int Correct { get; set; }
    public double? Accuracy { get; set; }
}

/// <summary>Precision and recall of a set of links (or added words) against the reference; null while the denominator is 0.</summary>
public sealed class AgentScoreOverlap
{
    public int Expected { get; set; }
    public int Actual { get; set; }
    public int Matched { get; set; }
    public double? Precision { get; set; }
    public double? Recall { get; set; }
}

public sealed class AgentScoreVerseCounts
{
    /// <summary>Verses in the reference file.</summary>
    public int Reference { get; set; }

    /// <summary>Reference verses the run covers (inside its range, in a package or with a proposal); only these are scored.</summary>
    public int Scored { get; set; }

    /// <summary>Reference verses outside the run (other book or range).</summary>
    public int NotInRun { get; set; }

    /// <summary>Scored verses with a proposal.</summary>
    public int WithProposal { get; set; }

    /// <summary>Scored verses without a proposal that were in no package (skipped as "no work", silence keeps the stored state).</summary>
    public int Silent { get; set; }

    /// <summary>Scored verses that were in a package but have no proposal: rejected by the validator or not answered. Counts as a hard failure.</summary>
    public int HardRejections { get; set; }

    /// <summary>Verses the run proposed that are not in the reference (not scored).</summary>
    public int ExtraRunVerses { get; set; }
}

public sealed class AgentScoreFlags
{
    public int Total { get; set; }
    public int VersesFlagged { get; set; }

    /// <summary>Flags per scored verse that has a proposal.</summary>
    public double? PerVerse { get; set; }

    public Dictionary<string, int> ByCode { get; set; } = [];
}

public sealed class AgentScoreCost
{
    public long InputTokens { get; set; }
    public long CachedInputTokens { get; set; }
    public long OutputTokens { get; set; }
    public decimal CostUsd { get; set; }

    /// <summary>Scored verses that sit in a package (the share of a package is split evenly over its verses).</summary>
    public int Verses { get; set; }

    public double? InputTokensPerVerse { get; set; }
    public double? OutputTokensPerVerse { get; set; }
    public decimal? CostUsdPerVerse { get; set; }
}

public sealed class AgentScoreCheck
{
    public string Code { get; set; } = string.Empty;
    public double? Value { get; set; }
    public double Required { get; set; }
    public bool Ok { get; set; }
}

/// <summary>The bulk-merge bar of 0018 02-decisions.md: lemmas, roles/morphemes, no hard-validator failure.</summary>
public sealed class AgentScoreBulkMerge
{
    public bool Meets { get; set; }
    public List<AgentScoreCheck> Checks { get; set; } = [];
    public List<string> Reasons { get; set; } = [];
}

public sealed class AgentScoreError
{
    /// <summary>German word position (<c>g</c>) or the link as <c>g→sRef.s</c>.</summary>
    public string Item { get; set; } = string.Empty;

    public string? Text { get; set; }
    public string? Expected { get; set; }
    public string? Actual { get; set; }
}

public sealed class AgentScoreVerse
{
    public string Ref { get; set; } = string.Empty;
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }

    /// <summary><c>proposal</c>, <c>silent</c> (no work, stored state) or <c>no-proposal</c> (in a package, rejected or unanswered).</summary>
    public string Status { get; set; } = string.Empty;

    public List<AgentScoreError> LemmaErrors { get; set; } = [];
    public List<AgentScoreError> RoleErrors { get; set; } = [];
    public List<AgentScoreError> MissingLinks { get; set; } = [];
    public List<AgentScoreError> ExtraLinks { get; set; } = [];
    public List<string> Flags { get; set; } = [];
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public decimal CostUsd { get; set; }
}

/// <summary>Result of the scorer, stored with the run (<c>AgentRuns.Score</c>) and returned by GET <c>agent-runs/{runId}/score</c>.</summary>
public sealed class AgentScoreResponse
{
    public int RunId { get; set; }
    public string Mode { get; set; } = string.Empty;
    public string Profile { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = string.Empty;
    public string RunStatus { get; set; } = string.Empty;
    public string ReferenceVersion { get; set; } = string.Empty;
    public DateTimeOffset ComputedAt { get; set; }

    public AgentScoreVerseCounts Verses { get; set; } = new();
    public AgentScoreRatio Lemmas { get; set; } = new();
    public AgentScoreRatio Stts { get; set; } = new();
    public AgentScoreRatio PartLemmas { get; set; } = new();

    /// <summary>Per expected link: the role is the reference's.</summary>
    public AgentScoreRatio Roles { get; set; } = new();

    /// <summary>Per expected Hebrew link: the morpheme is the reference's (Greek links have none).</summary>
    public AgentScoreRatio Morphemes { get; set; } = new();

    /// <summary>Per expected link: role and morpheme both right (the figure of the bar).</summary>
    public AgentScoreRatio RoleMorphemes { get; set; } = new();

    /// <summary>Real links (a German word to a source word, with its part). Meaningful for redo/fill-open runs; a lemma run keeps the links.</summary>
    public AgentScoreOverlap Links { get; set; } = new();

    /// <summary>Added words (German word + parent).</summary>
    public AgentScoreOverlap AddedWords { get; set; } = new();

    public AgentScoreFlags Flags { get; set; } = new();
    public AgentScoreCost Cost { get; set; } = new();
    public AgentScoreBulkMerge BulkMerge { get; set; } = new();
    public List<AgentScoreVerse> PerVerse { get; set; } = [];
}

/// <summary>POST <c>maintainer/agent-reference/freeze</c>: the current database state of these verses becomes the file content.</summary>
public sealed class FreezeAgentReferenceRequest
{
    /// <summary>File version name, e.g. <c>v1</c> (becomes <c>reference-v1.json</c>).</summary>
    public string Version { get; set; } = "v1";

    public List<AgentReferenceVerseKey> Verses { get; set; } = [];
}

public sealed class AgentReferenceVerseKey
{
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
}

public sealed class FreezeAgentReferenceResponse
{
    /// <summary>The file content (save it as <c>assets/agent-reference/reference-&lt;version&gt;.json</c>).</summary>
    public System.Text.Json.JsonElement Reference { get; set; }

    public string FileName { get; set; } = string.Empty;
    public int Verses { get; set; }

    /// <summary>Things the maintainer should check before committing the file (words without lemma, unlinked words, unknown verses).</summary>
    public List<string> Warnings { get; set; } = [];
}
