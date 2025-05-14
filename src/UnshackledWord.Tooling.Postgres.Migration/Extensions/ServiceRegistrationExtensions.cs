using UnshackledWord.Application.Abstractions;
using UnshackledWord.Persistence.Postgres.Services;

namespace UnshackledWord.Tooling.Postgres.Migration.Extensions;

public static class ServiceRegistrationExtensions
{
    public static IServiceCollection RegisterServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IDbConnectionFactory, PostgresDbConnectionFactory>();

        services.AddHostedService<Worker>();

        return services;
    }
}
