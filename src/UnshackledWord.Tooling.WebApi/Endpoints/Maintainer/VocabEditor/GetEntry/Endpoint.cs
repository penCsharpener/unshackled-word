using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VocabEditor.GetEntry;

/// <summary>One entry for the vocab editor page (roadmap 0022.4).</summary>
[RequiresLogin]
public sealed class Endpoint : Endpoint<GetVocabEditorEntryRequest, GetVocabEditorEntryResponse>
{
    private readonly VocabEditorRepository _repository;

    public Endpoint(VocabEditorRepository repository)
    {
        _repository = repository;
    }

    public override void Configure()
    {
        Get("vocab/entries/{id}");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "A vocab entry for the editor: all fields (origin of the form, reviewed, retired, content version), every meaning including the unaccepted ones, the Strong's keys with occurrences and lexicon lemma, the number of users holding a card, and the renderings and sample verses of the public entry endpoint. Retired entries are included. 404 for unknown ids.");
    }

    public override async Task HandleAsync(GetVocabEditorEntryRequest req, CancellationToken ct)
    {
        var response = await _repository.GetAsync(req.Id);

        if (response is null)
        {
            await Send.NotFoundAsync(ct);

            return;
        }

        await Send.OkAsync(response, ct);
    }
}
