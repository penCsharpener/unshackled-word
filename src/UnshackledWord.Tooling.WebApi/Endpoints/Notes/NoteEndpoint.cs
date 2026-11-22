using System.Security.Claims;
using FastEndpoints;
using UnshackledWord.Application.Features.Notes;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Notes;

/// <summary>Base of the note endpoints (roadmap 0023): the caller's user id (null = anonymous) and the outcome-to-HTTP mapping (404, 400).</summary>
public abstract class NoteEndpoint<TRequest, TResponse> : Endpoint<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : class
{
    /// <summary>The signed-in user from the session; null for anonymous callers. Never taken from the request body.</summary>
    protected int? ViewerId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    /// <summary>For endpoints marked [RequiresLogin].</summary>
    protected int UserId => ViewerId ?? throw new InvalidOperationException("The endpoint requires a signed-in user.");

    protected async Task SendOutcomeAsync(NoteOutcome<TResponse> outcome, int successStatus, CancellationToken ct)
    {
        if (outcome.Status == NoteStatus.Ok)
        {
            await Send.ResponseAsync(outcome.Response!, successStatus, ct);

            return;
        }

        AddError(outcome.Message ?? "The request was refused.");
        var status = outcome.Status == NoteStatus.NotFound ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest;
        await Send.ErrorsAsync(status, ct);
    }
}
