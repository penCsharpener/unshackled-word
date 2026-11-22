using FastEndpoints;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Notes;

public sealed class RouteGroupConfig : Group
{
    public RouteGroupConfig()
    {
        Configure("notes", ep =>
        {
        });
    }
}
