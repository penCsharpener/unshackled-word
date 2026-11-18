using FastEndpoints;
using UnshackledWord.Application.Features.ScriptureReferences;
using UnshackledWord.Domain.WebApi.MetaBible.ParseReference;

namespace UnshackledWord.Tooling.WebApi.Endpoints.MetaBible.ParseReference;

/// <summary>Public read; counted by the general anonymous rate limit like the other reads.</summary>
public class Endpoint : Ep.Req<ParseReferenceRequest>.Res<ParseReferenceResponse>
{
    private readonly IScriptureReferenceService _service;

    public Endpoint(IScriptureReferenceService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("references/parse");
        AllowAnonymous();
    }

    public override Task<ParseReferenceResponse> ExecuteAsync(ParseReferenceRequest req, CancellationToken ct)
    {
        return Task.FromResult(_service.Parse(req.Q));
    }
}
