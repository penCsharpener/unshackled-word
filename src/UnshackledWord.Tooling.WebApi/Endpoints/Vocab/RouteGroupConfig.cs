using FastEndpoints;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Vocab;

public sealed class RouteGroupConfig : Group
{
    public RouteGroupConfig()
    {
        Configure("vocab", ep =>
        {
        });
    }
}
