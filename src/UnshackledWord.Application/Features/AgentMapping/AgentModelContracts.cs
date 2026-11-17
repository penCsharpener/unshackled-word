namespace UnshackledWord.Application.Features.AgentMapping;

/// <summary>An extra conversation turn after the package (the retry loop appends the answer and the validator messages).</summary>
public sealed record AgentModelTurn(string Role, string Text)
{
    public const string User = "user";
    public const string Assistant = "assistant";
}

/// <summary>
/// One model call (07-providers-and-caching.md). The order is always <see cref="StaticPrefix"/> (system, byte-stable, cacheable),
/// the package as first user message, then the optional <see cref="Turns"/>.
/// </summary>
public sealed class AgentModelRequest
{
    public string Model { get; init; } = string.Empty;

    /// <summary>Instructions, column definitions, answer schema and worked examples: identical for every package of a run.</summary>
    public string StaticPrefix { get; init; } = string.Empty;

    /// <summary>The package JSON (the variable part).</summary>
    public string PackageJson { get; init; } = string.Empty;

    /// <summary>JSON Schema of the answer; the adapter decides how the provider takes it (or whether it does at all, see <see cref="AgentProfile.SchemaMode"/>).</summary>
    public string JsonSchema { get; init; } = string.Empty;

    public int MaxOutputTokens { get; init; } = 8192;

    /// <summary>Follow-up turns (assistant answer, user correction) appended after the package.</summary>
    public IReadOnlyList<AgentModelTurn> Turns { get; init; } = [];
}

public sealed class AgentModelResult
{
    /// <summary>The model's answer text; always valid JSON (an adapter throws a <see cref="AgentModelErrorKind.SchemaFailure"/> otherwise).</summary>
    public string Json { get; init; } = string.Empty;

    /// <summary>All prompt tokens including the cached ones (adapters normalise provider differences).</summary>
    public long InputTokens { get; init; }

    /// <summary>Prompt tokens read from the provider's cache (subset of <see cref="InputTokens"/>); 0 when the provider reports none.</summary>
    public long CachedInputTokens { get; init; }

    /// <summary>Generated tokens including hidden reasoning tokens the provider bills.</summary>
    public long OutputTokens { get; init; }

    public string Model { get; init; } = string.Empty;
    public string? ProviderRequestId { get; init; }
    public string? FinishReason { get; init; }
}

public enum AgentModelErrorKind
{
    /// <summary>Network error, timeout, 5xx or 429 that stayed after the retries.</summary>
    Transient,

    /// <summary>401/403: the key is wrong or lacks the permission; retrying does not help.</summary>
    Auth,

    /// <summary>Other 4xx: the request is wrong (unknown model, schema rejected).</summary>
    BadRequest,

    /// <summary>The answer is not valid JSON (usually cut off at the output limit); the worker shrinks the package.</summary>
    SchemaFailure,
}

public sealed class AgentModelException : Exception
{
    public AgentModelException(AgentModelErrorKind kind, string message, Exception? inner = null, long inputTokens = 0, long cachedInputTokens = 0, long outputTokens = 0)
        : base(message, inner)
    {
        Kind = kind;
        InputTokens = inputTokens;
        CachedInputTokens = cachedInputTokens;
        OutputTokens = outputTokens;
    }

    public AgentModelErrorKind Kind { get; }

    /// <summary>Tokens the failed call still used (a cut-off answer is billed); 0 when unknown.</summary>
    public long InputTokens { get; }

    public long CachedInputTokens { get; }
    public long OutputTokens { get; }
}

public interface IAgentModelClient
{
    Task<AgentModelResult> CompleteAsync(AgentModelRequest request, CancellationToken token);
}

/// <summary>Builds the adapter of a provider profile (<c>anthropic</c>, <c>gemini</c>, <c>openai</c>, <c>ollama</c>).</summary>
public interface IAgentModelClientFactory
{
    IAgentModelClient Create(AgentProfile profile);
}
