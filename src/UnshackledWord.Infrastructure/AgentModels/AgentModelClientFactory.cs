using UnshackledWord.Application.Features.AgentMapping;

namespace UnshackledWord.Infrastructure.AgentModels;

/// <summary>Picks the adapter of a profile's <see cref="AgentProfile.Kind"/>. One shared <see cref="HttpClient"/> (long timeout: a local model can take minutes per package).</summary>
public sealed class AgentModelClientFactory : IAgentModelClientFactory
{
    private static readonly HttpClient SharedHttp = new() { Timeout = TimeSpan.FromMinutes(10) };

    private readonly HttpClient _http;
    private readonly AgentRetryPolicy? _retry;

    public AgentModelClientFactory(HttpClient? http = null, AgentRetryPolicy? retry = null)
    {
        _http = http ?? SharedHttp;
        _retry = retry;
    }

    public IAgentModelClient Create(AgentProfile profile) => profile.Kind switch
    {
        AgentProviderKinds.Anthropic => new AnthropicAgentClient(_http, profile, _retry),
        AgentProviderKinds.Gemini => new GeminiAgentClient(_http, profile, _retry),
        AgentProviderKinds.OpenAi => new OpenAiAgentClient(_http, profile, _retry),
        AgentProviderKinds.Ollama => new OllamaAgentClient(_http, profile, _retry),
        _ => throw new NotSupportedException($"Unknown provider kind '{profile.Kind}' (profile '{profile.Name}')."),
    };
}
