using UnshackledWord.Seeding.Catalog;
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
        services.AddSetupStatus();
        return services;
    }

    /// <summary>Registers the service behind <c>GET api/admin/setup</c> (table counts per catalog step).</summary>
    public static IServiceCollection AddSetupStatus(this IServiceCollection services)
    {
        services.AddScoped<SetupStatusService>();
        return services;
    }
}
