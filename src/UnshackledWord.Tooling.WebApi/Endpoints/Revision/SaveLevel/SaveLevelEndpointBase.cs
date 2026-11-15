using FastEndpoints;
using UnshackledWord.Domain.WebApi.Revision;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Revision.SaveLevel;

/// <summary>Answers a <see cref="SaveRevisionLevelResult"/> for the create and the update endpoint.</summary>
public abstract class SaveLevelEndpointBase : Endpoint<SaveRevisionLevelRequest, RevisionLevelDetail>
{
    protected async Task SendResultAsync(SaveRevisionLevelResult result, CancellationToken ct)
    {
        switch (result.Outcome)
        {
            case SaveRevisionLevelOutcome.NotFound:
                await Send.NotFoundAsync(ct);
                return;
            case SaveRevisionLevelOutcome.Invalid:
                AddError(result.Error!);
                await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
                return;
            case SaveRevisionLevelOutcome.Conflict:
                AddError(result.Error!);
                await Send.ErrorsAsync(StatusCodes.Status409Conflict, ct);
                return;
        }

        await Send.OkAsync(result.Level!, ct);
    }
}
