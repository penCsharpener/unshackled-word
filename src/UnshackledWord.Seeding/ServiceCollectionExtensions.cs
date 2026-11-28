using Microsoft.Extensions.DependencyInjection.Extensions;
using UnshackledWord.Seeding.AiMappingImport;
using UnshackledWord.Seeding.Catalog;
using UnshackledWord.Seeding.ElberfelderParser;
using UnshackledWord.Seeding.Morphology;
using UnshackledWord.Seeding.RevisionSeed;
using UnshackledWord.Seeding.SBL;
using UnshackledWord.Seeding.StatisticalRestorationGnt;
using UnshackledWord.Seeding.StepBible;
using UnshackledWord.Seeding.SttsSeed;
using UnshackledWord.Seeding.Swete;
using UnshackledWord.Seeding.Tsk;

namespace UnshackledWord.Seeding;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the base seed runners (Stts tags, revision levels, Elberfelder 1871 text and verses, the STEP Bible importers) and their helpers.
    /// </summary>
    public static IServiceCollection AddSeedingServices(this IServiceCollection services)
    {
        services.AddScoped<SttsSeedRunner>();
        services.AddScoped<RevisionLevelSeedRunner>();
        services.AddElberfelder1871Services();
        services.AddSingleton<ParseHelper>();
        services.AddStepRunner();
        services.AddOtherSeedRunners();
        services.AddSetupStatus();
        return services;
    }

    /// <summary>
    /// Registers the runners of the <c>other</c> group (morphology, TSK, Statistical Restoration, SBL, AI mappings, LXX, revision seed) with their
    /// strategies, downloaders and repositories (roadmap 0031.8).
    /// </summary>
    public static IServiceCollection AddOtherSeedRunners(this IServiceCollection services)
    {
        services.AddScoped<MorphologySeedRunner>();
        services.AddScoped<TskRunner>();
        services.AddScoped<TskStrategy>();
        services.AddScoped<TskTextReader>();
        services.AddScoped<TskCurationSeedRunner>();
        services.AddScoped<SrRunner>();
        services.AddScoped<SrTsvParserStrategy>();
        services.AddScoped<SrTxtParserStrategy>();
        services.AddHttpClient<GithubFileDownloader>(client =>
        {
            client.BaseAddress = new Uri("https://github.com/");
        });
        services.AddScoped<SblTextDownloader>();
        services.AddScoped<SblApparatusDownloader>();
        services.AddScoped<SblGntTextStrategy>();
        services.AddScoped<SblGntApparatusStrategy>();
        services.AddScoped<SblRunner>();
        services.AddHttpClient("Github", client =>
        {
            client.BaseAddress = new Uri("https://github.com/");
        });
        services.AddScoped<AiMappingImportRunner>();
        services.AddScoped<AiMappingImportRunnerRepository>();
        services.TryAddScoped<UnshackledWord.Application.Features.Lxx.ILxxTextRepository, UnshackledWord.Infrastructure.Lxx.LxxTextRepository>();
        services.AddScoped<LxxSourceLoader>();
        services.AddScoped<LxxTextImporter>();
        services.AddScoped<LxxTagSeedRunner>();
        services.AddHttpClient<SweteDownloader>();
        services.AddScoped<RevisionSeedRunner>();
        return services;
    }

    /// <summary>Registers the service behind <c>GET api/admin/setup</c> (table counts per catalog step).</summary>
    public static IServiceCollection AddSetupStatus(this IServiceCollection services)
    {
        services.AddScoped<SetupStatusService>();
        services.AddSingleton<SetupRunState>();
        services.AddSingleton<SeedStepRunner>();
        services.AddOptions<SeedingOptions>().BindConfiguration(SeedingOptions.SectionName);
        return services;
    }
}
