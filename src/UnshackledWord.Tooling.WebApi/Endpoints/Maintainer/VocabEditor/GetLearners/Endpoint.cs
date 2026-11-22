using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Domain.WebApi.Vocab;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VocabEditor.GetLearners;

/// <summary>How many users learn an entry, for the editor's warning before a change is saved (roadmap 0022.8b).</summary>
[RequiresLogin]
public sealed class Endpoint : Endpoint<GetVocabEditorEntryRequest, VocabLearnersResponse>
{
    private readonly VocabChangeService _service;

    public Endpoint(VocabChangeService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("vocab/entries/{id}/learners");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Users with a card on the entry (`learners`) and those who have not accepted its latest version (`flagged`).");
    }

    public override async Task HandleAsync(GetVocabEditorEntryRequest req, CancellationToken ct)
    {
        await Send.OkAsync(await _service.LearnersAsync(req.Id, ct), ct);
    }
}
