using FastEndpoints;

namespace UnshackledWord.Tooling.WebApi.Endpoints.DataQuality;

public sealed class RouteGroupConfig : Group
{
    public RouteGroupConfig()
    {
        Configure("dataQuality", ep =>
        {
        });
    }
}
