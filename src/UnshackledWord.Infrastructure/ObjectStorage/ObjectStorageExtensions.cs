using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Infrastructure.Observability;

namespace UnshackledWord.Infrastructure.ObjectStorage;

/// <summary>Readiness of the media bucket: a HEAD-like request within two seconds.</summary>
public sealed class StorageHealthCheck(IObjectStorage storage) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(2));
        try
        {
            return await storage.BucketReachableAsync(cts.Token) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
        }
        catch (Exception)
        {
            return HealthCheckResult.Unhealthy();
        }
    }
}

public static class ObjectStorageExtensions
{
    /// <summary>Registers <see cref="IObjectStorage"/> and the <c>storage</c> ready check only when <c>ObjectStorage:ServiceUrl</c> is set; invalid options fail at startup.</summary>
    public static IServiceCollection AddObjectStorageIfConfigured(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(ObjectStorageOptions.SectionName).Get<ObjectStorageOptions>();
        if (options is not { IsConfigured: true })
        {
            return services;
        }

        FluentValidation.DefaultValidatorExtensions.ValidateAndThrow(new ObjectStorageOptionsValidator(), options);
        services.Configure<ObjectStorageOptions>(configuration.GetSection(ObjectStorageOptions.SectionName));
        services.AddSingleton<IObjectStorage, S3ObjectStorage>();
        services.AddHealthChecks().AddCheck<StorageHealthCheck>("storage", tags: [HealthExtensions.ReadyTag]);

        return services;
    }
}
