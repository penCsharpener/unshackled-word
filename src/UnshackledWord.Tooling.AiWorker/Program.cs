using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Options;
using Serilog;
using UnshackledWord.Tooling.AiWorker.Models;
using UnshackledWord.Tooling.WebApi.Extensions;
using GeminiClient = Google.GenAI.Client;

namespace UnshackledWord.Tooling.AiWorker;

public static partial class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddSerilog((sp, loggerConfig) => loggerConfig.ReadFrom.Configuration(builder.Configuration));
        builder.Services.RegisterServices(builder.Configuration);
        builder.Configuration.AddEnvironmentVariables("UNSHACKLEDWORD_");
        builder.Configuration.AddLocalSecrets();

        var host = builder.Build();
        host.Run();
    }

    public static IServiceCollection RegisterServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddWebApiServices(configuration)
            .AddGoogleAiClient(configuration)
            .AddSingleton<GreekMappingRepository>()
            .AddSingleton<GreekGeminiFlashClient>()
            .AddSingleton<GreekMappingService>()
            .AddSingleton<HebrewMappingRepository>()
            .AddSingleton<HebrewGeminiFlashClient>()
            .AddSingleton<HebrewMappingService>()
            .AddHostedService<Worker>()
            ;
        return services;
    }

    public static IServiceCollection AddGoogleAiClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GoogleAiOptions>(configuration.GetSection(nameof(GoogleAiOptions)));

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<GoogleAiOptions>>().Value;
            var httpOptions = new HttpOptions
            {
                Timeout = 10 * 60 * 1000
            };
            return new GeminiClient(apiKey: options.ApiKey, httpOptions: httpOptions);
        });

        return services;
    }

    static partial void AddLocalSecrets(this IConfigurationBuilder builder);
}
