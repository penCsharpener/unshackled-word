using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace UnshackledWord.Tooling.WebApi.Infrastructure;

public class ClassNameEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (logEvent.Properties.TryGetValue("SourceContext", out var sourceContext))
        {
            var fullClassName = sourceContext.ToString().Trim('"');
            var className = fullClassName[(fullClassName.LastIndexOf('.') + 1)..];
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Class", className));
        }
    }
}

public static class LoggingExtensions
{
    public static LoggerConfiguration WithClassName(this LoggerEnrichmentConfiguration enrichmentConfiguration)
    {
        return enrichmentConfiguration.With<ClassNameEnricher>();
    }
}
