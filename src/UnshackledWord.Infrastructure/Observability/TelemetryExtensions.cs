using System.Reflection;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Sinks.OpenTelemetry;
using UnshackledWord.Application.Features.AgentMapping;

namespace UnshackledWord.Infrastructure.Observability;

public static class TelemetryExtensions
{
    /// <summary>Registers traces, metrics and OTLP export when <c>Telemetry:Enabled</c> is true; otherwise registers nothing. Call after the configuration sources are added.</summary>
    public static IHostApplicationBuilder AddUnshackledTelemetry(this IHostApplicationBuilder builder, string serviceName)
    {
        builder.Services.AddUnshackledTelemetry(builder.Configuration, serviceName);

        return builder;
    }

    public static IServiceCollection AddUnshackledTelemetry(this IServiceCollection services, IConfiguration configuration, string serviceName)
    {
        var options = Read(configuration);
        if (!options.Enabled)
        {
            return services;
        }

        var endpoint = new Uri(options.OtlpEndpoint);
        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName, serviceVersion: ServiceVersion()))
            .WithTracing(t => t
                .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(options.TraceSampleRatio)))
                .AddAspNetCoreInstrumentation(o => o.Filter = ctx => !IsUntraced(ctx.Request.Path))
                .AddHttpClientInstrumentation()
                .AddNpgsql()
                .AddSource(AgentTelemetry.SourceName)
                .AddProcessor(new SpanSanitizer())
                .AddOtlpExporter(o =>
                {
                    o.Endpoint = endpoint;
                    o.Protocol = OtlpExportProtocol.Grpc;
                }))
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddNpgsqlInstrumentation()
                .AddOtlpExporter(o =>
                {
                    o.Endpoint = endpoint;
                    o.Protocol = OtlpExportProtocol.Grpc;
                }));

        return services;
    }

    /// <summary>Adds the OTLP log sink to Serilog when telemetry is enabled; console and file sinks stay untouched. Events carry trace and span id.</summary>
    public static LoggerConfiguration WriteToTelemetry(this LoggerConfiguration logger, IConfiguration configuration, string serviceName)
    {
        var options = Read(configuration);
        if (!options.Enabled)
        {
            return logger;
        }

        return logger.WriteTo.OpenTelemetry(o =>
        {
            o.Endpoint = options.OtlpEndpoint;
            o.Protocol = OtlpProtocol.Grpc;
            o.ResourceAttributes = new Dictionary<string, object>
            {
                ["service.name"] = serviceName,
                ["service.version"] = ServiceVersion(),
            };
        });
    }

    /// <summary>Health, OpenAPI and Swagger requests are never traced.</summary>
    public static bool IsUntraced(PathString path) =>
        path.StartsWithSegments("/api/health") || path.StartsWithSegments("/health")
        || path.StartsWithSegments("/openapi") || path.StartsWithSegments("/swagger");

    public static string ServiceVersion()
    {
        var info = (Assembly.GetEntryAssembly() ?? typeof(TelemetryExtensions).Assembly)
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        return string.IsNullOrEmpty(info) ? "0.0.0" : info.Split('+')[0];
    }

    private static TelemetryOptions Read(IConfiguration configuration)
    {
        var options = configuration.GetSection(TelemetryOptions.SectionName).Get<TelemetryOptions>() ?? new TelemetryOptions();
        new TelemetryOptionsValidator().ValidateAndThrow(options);

        return options;
    }
}
