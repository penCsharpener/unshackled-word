using FastEndpoints;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VocabEditor.DeleteAudio;

/// <summary>Removes the pronunciation of an entry (roadmap 0022.6): clears the key, then deletes the object. No content version bump.</summary>
[RequiresLogin]
public sealed class Endpoint : VocabEditEndpoint<VocabEntryIdRequest>
{
    private readonly IServiceProvider _services;

    public Endpoint(IServiceProvider services)
    {
        _services = services;
    }

    public override void Configure()
    {
        Delete("vocab/entries/{id}/audio");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Removes the pronunciation of an entry (200 even without one). 404 for an unknown entry, 503 while object storage is not configured. Does not bump the content version.");
    }

    public override async Task HandleAsync(VocabEntryIdRequest req, CancellationToken ct)
    {
        var storage = _services.GetService<IObjectStorage>();

        if (storage is null)
        {
            AddError("Object storage is not configured.");
            await Send.ErrorsAsync(StatusCodes.Status503ServiceUnavailable, ct);

            return;
        }

        var service = new VocabAudioService(_services.GetRequiredService<IDbConnectionFactory>(), storage);
        await SendOutcomeAsync(await service.DeleteAsync(req.Id, UserId, ct), ct);
    }
}
