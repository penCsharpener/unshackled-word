using System.Security.Claims;
using FastEndpoints;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Vocab;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VocabEditor;

/// <summary>Base of the vocab editor write endpoints (roadmap 0022.4): the signed-in user and the outcome-to-HTTP mapping (404, 409, 400).</summary>
public abstract class VocabEditEndpoint<TRequest> : Endpoint<TRequest, VocabEditResponse>
    where TRequest : notnull
{
    protected int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    protected async Task SendOutcomeAsync(VocabEditOutcome outcome, CancellationToken ct)
    {
        if (outcome.Status == VocabEditStatus.Ok)
        {
            await Send.OkAsync(outcome.Response!, ct);

            return;
        }

        AddError(outcome.Message ?? "The edit was refused.");
        var status = outcome.Status switch
        {
            VocabEditStatus.NotFound => StatusCodes.Status404NotFound,
            VocabEditStatus.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
        await Send.ErrorsAsync(status, ct);
    }
}
