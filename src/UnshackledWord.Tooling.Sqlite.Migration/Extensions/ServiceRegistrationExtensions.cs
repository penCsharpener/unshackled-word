using UnshackledWord.Application.Abstractions;
using UnshackledWord.Persistence.Sqlite.Services;

namespace UnshackledWord.Tooling.Sqlite.Migration.Extensions;

public static class ServiceRegistrationExtensions
{
    public static IServiceCollection RegisterServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IDbConnectionFactory, SqliteDbConnectionFactory>();

        services.AddHostedService<Worker>();

        return services;
    }
}
