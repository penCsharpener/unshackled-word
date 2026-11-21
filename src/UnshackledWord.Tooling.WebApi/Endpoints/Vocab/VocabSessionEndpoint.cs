using System.Security.Claims;
using FastEndpoints;
using UnshackledWord.Infrastructure.Vocab;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Vocab;

/// <summary>Base of the study session endpoints (roadmap 0022.8): the signed-in user and the outcome-to-HTTP mapping (404, 409, 400).</summary>
public abstract class VocabSessionEndpoint<TRequest, TResponse> : Endpoint<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : class
{
    protected int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    protected async Task SendOutcomeAsync(VocabSessionOutcome<TResponse> outcome, CancellationToken ct)
    {
        if (outcome.Status == VocabSessionStatus.Ok)
        {
            await Send.OkAsync(outcome.Response!, ct);

            return;
        }

        AddError(outcome.Message ?? "The request was refused.");
        var status = outcome.Status switch
        {
            VocabSessionStatus.NotFound => StatusCodes.Status404NotFound,
            VocabSessionStatus.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
        await Send.ErrorsAsync(status, ct);
    }
}
