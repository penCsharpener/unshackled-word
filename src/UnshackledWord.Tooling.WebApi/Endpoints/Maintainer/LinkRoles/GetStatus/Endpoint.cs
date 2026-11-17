using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.LinkRoles.Backfill;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.LinkRoles.GetStatus;

[RequiresLogin]
public sealed class Endpoint : EndpointWithoutRequest<LinkRoleStatusResponse>
{
    private readonly LinkRoleJobRunner _runner;
    private readonly ILinkRoleBackfillRepository _repository;

    public Endpoint(LinkRoleJobRunner runner, ILinkRoleBackfillRepository repository)
    {
        _runner = runner;
        _repository = repository;
    }

    public override void Configure()
    {
        Get("link-roles/status");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "The running or last link-roles job (log, report, samples), the applied change set undo would restore, and DqRefreshState.LinkRolesAppliedAt.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var applied = await _repository.GetAppliedAsync(ct);
        var appliedAt = await _repository.GetLinkRolesAppliedAtAsync(ct);

        await Send.OkAsync(new LinkRoleStatusResponse
        {
            Job = _runner.Snapshot(),
            Applied = applied is null
                ? null
                : new LinkRoleChangeSetDto
                {
                    Id = applied.Id,
                    UserId = applied.UserId,
                    CreatedAt = Utc(applied.CreatedAt),
                    MappingRows = applied.MappingRows,
                    LemmaRows = applied.LemmaRows,
                },
            LinkRolesAppliedAt = appliedAt is { } at ? Utc(at) : null,
        }, ct);
    }

    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
