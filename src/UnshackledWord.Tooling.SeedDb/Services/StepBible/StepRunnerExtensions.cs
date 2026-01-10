namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public static class StepRunnerExtensions
{
    public static IServiceCollection AddStepRunner(this IServiceCollection services)
    {
        services.AddSingleton<StepRunner>();
        services.AddSingleton<StepGithubDownloader>();
        services.AddSingleton<StepGreekFileStrategy>();
        services.AddSingleton<StepHebrewFileStrategy>();
        services.AddSingleton<StepHebrewStrongsStrategy>();
        services.AddSingleton<StepHebrewMorphologyStrategy>();
        services.AddSingleton<StepGreekStrongsStrategy>();
        services.AddSingleton<StepGreekMorphologyStrategy>();
        services.AddHttpClient<StepGithubDownloader>(client =>
        {
            client.BaseAddress = new Uri("https://github.com/");
        });
        return services;
    }
}
