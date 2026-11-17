using FastEndpoints;
using UnshackledWord.Application.Features.AgentMapping.Scoring;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.AgentReference.Freeze;

[RequiresLogin]
public sealed class Endpoint : Endpoint<FreezeAgentReferenceRequest, FreezeAgentReferenceResponse>
{
    private readonly IAgentReferenceFreezer _freezer;

    public Endpoint(IAgentReferenceFreezer freezer)
    {
        _freezer = freezer;
    }

    public override void Configure()
    {
        Post("agent-reference/freeze");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Freezes the current database state of the given verses (links with role/morpheme/part lemma, lemma and STTS per word, added words) into the reference file format and returns it; nothing is written to disk, save the result as assets/agent-reference/reference-<version>.json. The 10 prompt example verses are rejected (400).");
    }

    public override async Task HandleAsync(FreezeAgentReferenceRequest req, CancellationToken ct)
    {
        var result = await _freezer.FreezeAsync(req, ct);
        if (!result.Ok)
        {
            foreach (var error in result.Errors)
            {
                AddError(error);
            }

            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);

            return;
        }

        await Send.OkAsync(new FreezeAgentReferenceResponse
        {
            Reference = AgentReferenceFreezer.ToElement(result.Reference!),
            FileName = ReferenceSetFile.FileName(result.Reference!.Version),
            Verses = result.Reference.Verses.Count,
            Warnings = [.. result.Warnings],
        }, ct);
    }
}
