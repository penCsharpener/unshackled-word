using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public static class StepRunnerExtensions
{
    public static IServiceCollection AddStepRunner(this IServiceCollection services)
    {
        services.AddSingleton<StepRunner>();
        services.AddSingleton<StepGithubDownloader>();
        services.AddSingleton<StepFileStrategy>();
        services.AddHttpClient<StepGithubDownloader>(client =>
        {
            client.BaseAddress = new Uri("https://github.com/");
        });
        return services;
    }
}
