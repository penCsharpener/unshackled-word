using Microsoft.Extensions.DependencyInjection;
using UnshackledWord.Application.Features.Backup;
using UnshackledWord.Application.Repositories;

namespace UnshackledWord.Infrastructure.Repositories;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IBibleBookRepository, BibleBookRepository>();
        services.AddScoped<IElb1871TaggingRepository, Elb1871TaggingRepository>();
        services.AddScoped<IElbDashboardRepository, ElbDashboardRepository>();
        services.AddScoped<IElb1871WordRepository, Elb1871WordRepository>();
        services.AddScoped<ISrWordRepository, SrWordRepository>();
        return services;
    }
}
