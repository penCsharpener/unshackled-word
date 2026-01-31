using UnshackledWord.Application.Abstractions.Step;
using UnshackledWord.Infrastructure.Repositories.Step;
using UnshackledWord.Tooling.SeedDb.Services.StepBible.LexiconParser;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public static class StepRunnerExtensions
{
    public static IServiceCollection AddStepRunner(this IServiceCollection services)
    {
        services.AddScoped<StepRunner>();
        services.AddScoped<StepDataDbImporter>();
        services.AddScoped<StepGithubDownloader>();
        services.AddScoped<StepGreekFileStrategy>();
        services.AddScoped<StepHebrewFileStrategy>();
        services.AddScoped<StepHebrewStrongsStrategy>();
        services.AddScoped<StepHebrewMorphologyStrategy>();
        services.AddScoped<StepGreekStrongsStrategy>();
        services.AddScoped<StepGreekMorphologyStrategy>();
        services.AddScoped<StepStrongsNormalizingStrategy>();
        services.AddScoped<StepBibleStructureStrategy>();
        services.AddScoped<StepLexiconStrategyFactory>();
        services.AddScoped<StepPersonPlaceLexiconStrategy>();
        services.AddScoped<IStepStrongsToVersesRepository, StepStrongsToVersesRepository>();
        services.AddScoped<IStepGreekWordsRepository, StepGreekWordsRepository>();
        services.AddScoped<IStepHebrewWordsRepository, StepHebrewWordsRepository>();
        services.AddScoped<IStepStrongsRepository, StepStrongsRepository>();
        services.AddScoped<IStepHebrewMorphologyRepository, StepHebrewMorphologyRepository>();
        services.AddScoped<IStepGreekMorphologyRepository, StepGreekMorphologyRepository>();
        services.AddScoped<IStepPersonPlaceRepository, StepPersonPlaceRepository>();
        services.AddHttpClient<StepGithubDownloader>(client =>
        {
            client.BaseAddress = new Uri("https://github.com/");
        });
        return services;
    }
}
