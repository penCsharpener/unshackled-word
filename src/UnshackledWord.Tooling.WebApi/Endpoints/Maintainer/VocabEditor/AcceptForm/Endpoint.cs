using FastEndpoints;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Application.Features.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VocabEditor.AcceptForm;

/// <summary>Accepts the stored dictionary form as written: the origin becomes manual, text and ContentVersion stay (no change row). 409 when the entry has no form.</summary>
[RequiresLogin]
public sealed class Endpoint : VocabEditEndpoint<VocabEntryIdRequest>
{
    private readonly VocabEntryEditor _editor;

    public Endpoint(VocabEntryEditor editor)
    {
        _editor = editor;
    }

    public override void Configure()
    {
        Post("vocab/entries/{id}/accept-form");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Accepts the stored dictionary form as written: the origin becomes manual, text and ContentVersion stay (no change row). 409 when the entry has no form.");
    }

    public override async Task HandleAsync(VocabEntryIdRequest req, CancellationToken ct)
    {
        await SendOutcomeAsync(await _editor.AcceptFormAsync(req.Id, UserId, ct), ct);
    }
}
