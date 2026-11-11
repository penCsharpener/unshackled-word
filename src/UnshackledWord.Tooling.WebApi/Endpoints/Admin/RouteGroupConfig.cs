using FastEndpoints;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Admin;

public sealed class RouteGroupConfig : Group
{
    public RouteGroupConfig()
    {
        Configure("admin", ep =>
        {
        });
    }
}
