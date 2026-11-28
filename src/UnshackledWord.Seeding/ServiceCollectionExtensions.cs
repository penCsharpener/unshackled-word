using UnshackledWord.Seeding.ElberfelderParser;
using UnshackledWord.Seeding.RevisionSeed;
using UnshackledWord.Seeding.SttsSeed;

namespace UnshackledWord.Seeding;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the base seed runners (Stts tags, revision levels, Elberfelder 1871 text and verses) and their helpers.
    /// </summary>
    public static IServiceCollection AddSeedingServices(this IServiceCollection services)
    {
        services.AddScoped<SttsSeedRunner>();
        services.AddScoped<RevisionLevelSeedRunner>();
        services.AddElberfelder1871Services();
        services.AddSingleton<ParseHelper>();
        return services;
    }
}
