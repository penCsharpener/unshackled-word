using FastEndpoints;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder;

public sealed class RouteGroupConfig : Group
{
    public RouteGroupConfig()
    {
        Configure("elberfelder", ep =>
        {
        });
    }
}
