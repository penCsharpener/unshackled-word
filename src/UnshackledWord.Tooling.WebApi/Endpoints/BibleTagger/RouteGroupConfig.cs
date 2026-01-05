using FastEndpoints;

namespace UnshackledWord.Tooling.WebApi.Endpoints.BibleTagger;

public sealed class RouteGroupConfig : Group
{
    public RouteGroupConfig()
    {
        Configure("bt", ep =>
        {
        });
    }
}
