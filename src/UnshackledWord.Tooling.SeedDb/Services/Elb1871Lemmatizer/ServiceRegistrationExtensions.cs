using Microsoft.Extensions.Options;
using UnshackledWord.Domain.Models.Settings;

namespace UnshackledWord.Tooling.SeedDb.Services.Elb1871Lemmatizer;

public static class ServiceRegistrationExtensions
{
    public static IServiceCollection AddElberfelder1871Lemmatizer(this IServiceCollection services)
    {
        services.AddScoped<LemmatizerRunner>();
        services.AddScoped<LemmatizerRepository>();
        services.AddScoped<LemmatizerDownloader>();
        // services.AddHttpClient<LemmatizerDownloader>((sp, client) =>
        // {
        //     var url = sp.GetRequiredService<IOptions<AppSettings>>().Value.DatabaseSeeding.Elberfelder1871
        //         .LemmatizerGermanLink;
        //     client.BaseAddress = new Uri(url);
        // });
        services.AddScoped<LemmatizerStrategy>();
        services.AddHttpClient("Github", client =>
        {
            client.BaseAddress = new Uri("https://github.com/");
        });

        return services;
    }
}
