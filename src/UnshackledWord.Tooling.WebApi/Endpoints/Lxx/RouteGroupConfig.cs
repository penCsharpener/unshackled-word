using FastEndpoints;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Lxx;

public sealed class RouteGroupConfig : Group
{
    public RouteGroupConfig()
    {
        Configure("lxx", ep =>
        {
        });
    }
}
