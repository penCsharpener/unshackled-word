using System.Security.Claims;
using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Lemmas;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Lemmas.BulkRevert;

[RequiresLogin]
public sealed class Endpoint : Endpoint<BulkRevertRequest, BulkRevertResponse>
{
    private readonly ILemmaBulkRepository _repository;

    public Endpoint(ILemmaBulkRepository repository)
    {
        _repository = repository;
    }

    public override void Configure()
    {
        Post("bulk/{changeSetId:int}/revert");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Restores the previous lemma of a bulk change set where the row still carries the new lemma; others are skipped.");
    }

    public override async Task HandleAsync(BulkRevertRequest req, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _repository.RevertAsync(req.ChangeSetId, userId, ct);
        switch (result.Status)
        {
            case RevertStatus.NotFound:
                await Send.NotFoundAsync(ct);
                break;
            case RevertStatus.AlreadyReverted:
                AddError("Change set was already reverted.");
                await Send.ErrorsAsync(409, ct);
                break;
            case RevertStatus.NotRevertable:
                AddError("Only bulk lemma change sets can be reverted.");
                await Send.ErrorsAsync(409, ct);
                break;
            default:
                await Send.OkAsync(new BulkRevertResponse { ChangeSetId = result.ChangeSetId, Restored = result.Restored, Skipped = result.Skipped }, ct);
                break;
        }
    }
}

public sealed class Validator : Validator<BulkRevertRequest>
{
    public Validator()
    {
        RuleFor(x => x.ChangeSetId).GreaterThan(0);
    }
}
