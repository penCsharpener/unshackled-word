using System.Diagnostics;

namespace UnshackledWord.Application.Features.AgentMapping;

/// <summary>Trace source of the agent runs (roadmap 0021): run, package and attempt spans. Attributes never carry API keys or prompt text.</summary>
public static class AgentTelemetry
{
    public const string SourceName = "UnshackledWord.AgentMapping";

    public static readonly ActivitySource Source = new(SourceName);
}
