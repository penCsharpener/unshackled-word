using Microsoft.Extensions.DependencyInjection;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Persistence.Postgres.Services;

namespace UnshackledWord.Persistence.Postgres.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPostgresPersistence(this IServiceCollection services)
    {
        services.AddSingleton<IDbConnectionFactory, PostgresDbConnectionFactory>();

        return services;
    }
}
