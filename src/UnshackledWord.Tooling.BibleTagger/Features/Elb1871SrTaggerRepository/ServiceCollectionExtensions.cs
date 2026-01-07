using Microsoft.Extensions.Options;
using UnshackledWord.Tooling.BibleTagger.Features.Configuration;

namespace UnshackledWord.Tooling.BibleTagger.Features.Elb1871SrTaggerRepository;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddElb1871Tagging(this IServiceCollection services)
    {
        services.AddScoped<IElbSrTaggerRepository, ElbSrTaggerRepository>();
        services.AddScoped<IMetaBibleRepository, MetaBibleRepository>();

        services.AddHttpClient("core-api", (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<AppSettings>>();
            client.BaseAddress = new Uri(options.Value.CoreApi.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.Value.CoreApi.Timeout);
        });
        return services;
    }
}
