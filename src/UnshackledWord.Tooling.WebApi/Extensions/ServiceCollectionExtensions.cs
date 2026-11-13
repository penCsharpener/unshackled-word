using UnshackledWord.Infrastructure.Extensions;
using UnshackledWord.Infrastructure.Repositories;
using UnshackledWord.Infrastructure.Seed;
using UnshackledWord.Persistence.Postgres.Extensions;
using UnshackledWord.Tooling.WebApi.Endpoints.BibleTagger.BackupElbData;
using UnshackledWord.Tooling.WebApi.Endpoints.BibleTagger.BackupElbData.Mappings;
using UnshackledWord.Tooling.WebApi.Models;

namespace UnshackledWord.Tooling.WebApi.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWebApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AppSettings>(configuration.GetSection(nameof(AppSettings)));
        services.AddPostgresPersistence();
        services.AddInfrastructureServices();
        services.AddRepositories();
        services.AddSeedExportServices();
        services.AddApiServices();

        return services;
    }

    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddScoped<BackupFileService>();
        services.AddScoped<Endpoints.Lemmas.ILemmaRepository, Endpoints.Lemmas.LemmaRepository>();
        services.AddScoped<Endpoints.Lemmas.ILemmaBulkRepository, Endpoints.Lemmas.LemmaBulkRepository>();
        services.AddScoped<Endpoints.Lemmas.Stts.ISttsTagRepository, Endpoints.Lemmas.Stts.SttsTagRepository>();
        services.AddScoped<Endpoints.Lemmas.Stts.LemmaSttsGuard>();
        services.AddHostedService<Endpoints.Lemmas.Stts.SttsDriftCheck>();
        services.AddSingleton<Endpoints.DataQuality.StrongsLexiconIndex>();
        services.AddSingleton<Endpoints.DataQuality.DqRefreshCoordinator>();
        services.AddSingleton<UnshackledWord.Application.Features.StrongsStats.IStatsRefreshStatus>(sp => sp.GetRequiredService<Endpoints.DataQuality.DqRefreshCoordinator>());
        services.AddScoped<Endpoints.DataQuality.IDqFactsRepository, Endpoints.DataQuality.DqFactsRepository>();
        services.AddScoped<Endpoints.DataQuality.IDqCheckRepository, Endpoints.DataQuality.DqCheckRepository>();
        services.AddScoped<Endpoints.DataQuality.IDqExplorerRepository, Endpoints.DataQuality.DqExplorerRepository>();
        services.AddScoped<Endpoints.DataQuality.DqOverviewService>();
        services.AddSingleton<Endpoints.Maintainer.ExportSeed.SeedExportCoordinator>();
        services.AddScoped<Endpoints.Maintainer.ExportSeed.SeedZipBuilder>();
        return services;
    }
}
