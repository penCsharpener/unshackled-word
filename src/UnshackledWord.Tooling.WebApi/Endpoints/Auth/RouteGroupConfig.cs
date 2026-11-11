using FastEndpoints;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Auth;

public sealed class RouteGroupConfig : Group
{
    public RouteGroupConfig()
    {
        Configure("auth", ep =>
        {
        });
    }
}
