using UnshackledWord.Infrastructure.Extensions;
using UnshackledWord.Infrastructure.Repositories;
using UnshackledWord.Persistence.Postgres.Extensions;

namespace UnshackledWord.Tooling.WebApi.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWebApiServices(this IServiceCollection services)
    {
        services.AddPostgresPersistence();
        services.AddInfrastructureServices();
        services.AddRepositories();

        return services;
    }
}
