using FastEndpoints;

namespace UnshackledWord.Tooling.WebApi.Endpoints.MetaBible;

public sealed class RouteGroupConfig : Group
{
    public RouteGroupConfig()
    {
        Configure("meta-bible", ep =>
        {
        });
    }
}
