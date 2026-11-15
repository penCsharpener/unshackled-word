using FastEndpoints;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Revision;

public sealed class RouteGroupConfig : Group
{
    public RouteGroupConfig()
    {
        Configure("revision", ep =>
        {
        });
    }
}
