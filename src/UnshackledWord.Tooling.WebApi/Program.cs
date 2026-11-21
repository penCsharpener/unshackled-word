using System.Text.Json;
using FastEndpoints;
using FastEndpoints.Swagger;
using Serilog;
using UnshackledWord.Infrastructure.ObjectStorage;
using UnshackledWord.Infrastructure.Observability;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Extensions;
using UnshackledWord.Tooling.WebApi.RateLimiting;

namespace UnshackledWord.Tooling.WebApi;

public partial class Program
{
    private const string ServiceName = "unshackledword-webapi";

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddSerilog((_, logger) => logger.ReadFrom.Configuration(builder.Configuration).WriteToTelemetry(builder.Configuration, ServiceName));
        builder.Services.AddFastEndpoints()
            .SwaggerDocument();
        builder.Configuration.AddEnvironmentVariables("UNSHACKLEDWORD_");
        AddLocalSecrets(builder.Configuration);
        builder.Services.AddAuth(builder.Configuration);
        builder.Services.AddWebApiServices(builder.Configuration);
        builder.Services.AddRateLimiting(builder.Configuration);
        builder.AddUnshackledTelemetry(ServiceName);
        builder.Services.AddUnshackledHealthChecks();
        builder.Services.AddObjectStorageIfConfigured(builder.Configuration);

        builder.Services.AddOpenApi();
        builder.Services.AddCors();

        var app = builder.Build();

        var rateLimiting = app.Configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>() ?? new RateLimitingOptions();
        var reverseProxy = app.Configuration.GetSection(ReverseProxyOptions.SectionName).Get<ReverseProxyOptions>() ?? new ReverseProxyOptions();
        if (reverseProxy.HasEntries)
        {
            // Must run first: everything after it (rate limiting, logging) sees the client address instead of the proxy's.
            app.UseForwardedHeaders(reverseProxy.ToForwardedHeadersOptions());
        }
        else if (rateLimiting.Enabled && app.Environment.IsProduction())
        {
            app.Logger.LogWarning("RateLimiting is enabled but ReverseProxy has no KnownProxies/KnownNetworks: behind a proxy every visitor shares the proxy's address and one rate-limit bucket");
        }

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            // The session cookie needs credentialed CORS, which forbids a wildcard origin: reflect the caller instead.
            // (The Vite dev server normally proxies /api same-origin, so this only matters for direct calls.)
            app.UseCors(x => x.SetIsOriginAllowed(_ => true).AllowAnyMethod().AllowAnyHeader().AllowCredentials());
        }

        if (app.Environment.IsProduction())
        {
            app.UseHttpsRedirection();
            app.UseCors(x => x
                .WithOrigins(app.Configuration.GetSection("App").Get<AppOptions>()?.FrontendBaseUrl.TrimEnd('/') ?? string.Empty)
                .AllowAnyMethod().AllowAnyHeader().AllowCredentials());
        }

        app.UseAuthentication();
        if (rateLimiting.Enabled)
        {
            // After authentication (signed-in users are exempt), before authorization.
            app.UseRateLimiter();
        }

        app.UseAuthorization();
        app.UseFastEndpoints(x =>
        {
            x.Endpoints.RoutePrefix = "api";
            x.Serializer.ResponseSerializer = (rsp, dto, cType, jCtx, ct) =>
            {
                rsp.ContentType = cType;
                var options = new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                };
                return rsp.WriteAsync(
                    JsonSerializer.Serialize(dto, dto.GetType(), options),
                    ct
                );
            };
            x.Endpoints.Configurator = ep =>
            {
                // Everything is public unless it opts in with [RequiresLogin] (account endpoints under /api/auth).
                if (!ep.EndpointType.IsDefined(typeof(RequiresLoginAttribute), false))
                {
                    ep.AllowAnonymous();
                }

                // Strict rate-limit tier (search, auth): opted in with [RateLimitGroup("...")].
                if (ep.EndpointType.GetCustomAttributes(typeof(RateLimitGroupAttribute), false).FirstOrDefault() is RateLimitGroupAttribute rateLimitGroup)
                {
                    ep.Options(b => b.WithMetadata(new RateLimitGroupMetadata(rateLimitGroup.Group)));
                }
            };
        }).UseSwaggerGen();
        app.MapUnshackledHealth("/api/health");

        app.Run();
    }

    static partial void AddLocalSecrets(ConfigurationManager builder);
}
