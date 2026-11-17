using UnshackledWord.Application.Features.ApiTokens;

namespace UnshackledWord.Tooling.McpServer.Auth;

/// <summary>Who calls the tool: the bearer token of the current HTTP request, checked against <c>ApiTokens</c> (roadmap 0016.8).</summary>
public interface IMcpCaller
{
    /// <summary>The token is mandatory (scope propose): invalid, expired, revoked or missing returns a failed validation.</summary>
    Task<ApiTokenValidation> RequireProposeAsync(CancellationToken token = default);

    /// <summary>Null when the request carries no Authorization header at all; a header with a bad token is still a failed validation (no silent downgrade).</summary>
    Task<ApiTokenValidation?> OptionalProposeAsync(CancellationToken token = default);
}

public sealed class McpCaller : IMcpCaller
{
    private readonly IHttpContextAccessor _http;
    private readonly IApiTokenService _tokens;

    public McpCaller(IHttpContextAccessor http, IApiTokenService tokens)
    {
        _http = http;
        _tokens = tokens;
    }

    public Task<ApiTokenValidation> RequireProposeAsync(CancellationToken token = default) =>
        _tokens.ValidateAsync(Header(), ApiTokenScopes.Propose, token);

    public async Task<ApiTokenValidation?> OptionalProposeAsync(CancellationToken token = default) =>
        string.IsNullOrWhiteSpace(Header()) ? null : await _tokens.ValidateAsync(Header(), ApiTokenScopes.Propose, token);

    private string? Header() => _http.HttpContext?.Request.Headers.Authorization.ToString();
}
