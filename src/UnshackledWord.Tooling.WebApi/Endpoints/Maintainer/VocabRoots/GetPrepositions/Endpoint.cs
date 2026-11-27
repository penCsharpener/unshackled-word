using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.Vocab;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VocabRoots.GetPrepositions;

[RequiresLogin]
public sealed class Endpoint : EndpointWithoutRequest<VocabPrepositionsResponse>
{
    public override void Configure()
    {
        Get("vocab/roots/prepositions");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "The closed preposition list a root prefix may use (Strong's key and dictionary form, ordered by form); the editor's prefix picker and the validator share it.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(new VocabPrepositionsResponse
        {
            Items = VocabRootPrefill.PrepositionList().Select(p => new VocabPrepositionResponse { Key = p.Key, Lemma = p.Dictionary }).ToList(),
        }, ct);
    }
}
