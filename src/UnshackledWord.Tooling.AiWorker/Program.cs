using Google.GenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UnshackledWord.Tooling.AiWorker.Models;
using UnshackledWord.Tooling.WebApi.Extensions;
using GeminiClient = Google.GenAI.Client;

namespace UnshackledWord.Tooling.AiWorker;

public static partial class Program
{
    public static IServiceProvider SetupDependencies(Action<IServiceCollection>? configureServices, Action<IConfigurationBuilder>? configureConfiguration)
    {
        var builder = new ConfigurationBuilder();
        builder.AddLocalSecrets();
        configureConfiguration?.Invoke(builder);
        var configuration = builder.Build();

        var services = new ServiceCollection()
                .AddWebApiServices(configuration)
                .AddGoogleAiClient(configuration)
                .AddSingleton<IConfiguration>(configuration)
                .AddSingleton<MappingRepository>()
                .AddLogging()
            ;

        configureServices?.Invoke(services);

        return services.BuildServiceProvider();
    }

    public static IServiceCollection AddGoogleAiClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GoogleAiOptions>(configuration.GetSection(nameof(GoogleAiOptions)));

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<GoogleAiOptions>>().Value;
            return new GeminiClient(apiKey: options.ApiKey);
        });

        return services;
    }

    static partial void AddLocalSecrets(this IConfigurationBuilder builder);
}
