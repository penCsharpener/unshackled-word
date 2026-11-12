using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Lemmas;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Lemmas.BulkWordIds;

[RequiresLogin]
public sealed class Endpoint : Endpoint<BulkFilterRequest, BulkWordIdsResponse>
{
    private readonly ILemmaBulkRepository _repository;

    public Endpoint(ILemmaBulkRepository repository)
    {
        _repository = repository;
    }

    public override void Configure()
    {
        Get("bulk/word-ids");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "All matching word ids (max 20,000) for select-all-pages / select-group-on-all-pages.");
    }

    public override async Task HandleAsync(BulkFilterRequest req, CancellationToken ct) =>
        await Send.OkAsync(await _repository.GetWordIdsAsync(req, ct), ct);
}

public sealed class Validator : Validator<BulkFilterRequest>
{
    public Validator()
    {
        Include(new BulkFilterValidator());
    }
}
