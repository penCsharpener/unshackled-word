using FastEndpoints;

namespace UnshackledWord.Tooling.WebApi.Elberfelder;

public class RouteGroupConfig : Group
{
    public RouteGroupConfig()
    {
        Configure("elberfelder", ep =>
        {
        });
    }
}
