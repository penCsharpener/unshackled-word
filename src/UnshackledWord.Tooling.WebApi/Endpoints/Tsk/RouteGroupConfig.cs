using FastEndpoints;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Tsk;

public sealed class RouteGroupConfig : Group
{
    public RouteGroupConfig()
    {
        Configure("tsk", ep =>
        {
        });
    }
}
