using FastEndpoints;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Lemmas;

public sealed class RouteGroupConfig : Group
{
    public RouteGroupConfig()
    {
        Configure("lemmas", ep =>
        {
        });
    }
}
