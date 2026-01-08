using UnshackledWord.Infrastructure.Extensions;
using UnshackledWord.Infrastructure.Repositories;
using UnshackledWord.Persistence.Postgres.Extensions;
using UnshackledWord.Tooling.WebApi.Endpoints.BibleTagger.BackupElbData;
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
        services.AddApiServices();

        return services;
    }

    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddScoped<BackupFileService>();
        return services;
    }
}
