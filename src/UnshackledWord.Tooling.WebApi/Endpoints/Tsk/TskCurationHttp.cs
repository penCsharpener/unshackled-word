using UnshackledWord.Application.Features.TskCuration;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Tsk;

/// <summary>HTTP status of a refused <see cref="TskCurationOutcome"/>.</summary>
public static class TskCurationHttp
{
    public static int StatusCode(TskCurationStatus status) => status switch
    {
        TskCurationStatus.Forbidden => StatusCodes.Status403Forbidden,
        TskCurationStatus.NotFound => StatusCodes.Status404NotFound,
        TskCurationStatus.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest,
    };
}
