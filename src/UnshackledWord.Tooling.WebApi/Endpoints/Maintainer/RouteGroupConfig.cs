using FastEndpoints;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer;

public sealed class RouteGroupConfig : Group
{
    public RouteGroupConfig()
    {
        Configure("maintainer", ep =>
        {
        });
    }
}
