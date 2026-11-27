using FastEndpoints;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VocabRootRuns.Report;

public sealed class ReportRequest
{
    public int RunId { get; set; }
}

/// <summary>Run report of a vocab root agent run (roadmap 0030.4): the rejected, flagged and replaced entries, linked to the editor by the client.</summary>
[RequiresLogin]
public sealed class Endpoint : Endpoint<ReportRequest, VocabRootRunReportResponse>
{
    private readonly VocabRootRunRepository _roots;
    private readonly IAgentRunRepository _runs;

    public Endpoint(VocabRootRunRepository roots, IAgentRunRepository runs)
    {
        _roots = roots;
        _runs = runs;
    }

    public override void Configure()
    {
        Get("vocab/root-runs/{runId}/report");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Run report of a vocab root agent run: entries the run rejected (rule broken), flagged (unsure, not written) or replaced (prefill row). 404 for an unknown run or a run of another mode.");
    }

    public override async Task HandleAsync(ReportRequest req, CancellationToken ct)
    {
        var run = await _runs.GetRunAsync(req.RunId, ct);

        if (run is null || run.Mode != AgentModes.VocabRoot)
        {
            await Send.NotFoundAsync(ct);

            return;
        }

        await Send.OkAsync(await _roots.GetReportAsync(req.RunId, ct), ct);
    }
}
