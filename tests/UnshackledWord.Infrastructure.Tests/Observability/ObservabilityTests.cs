using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Trace;
using UnshackledWord.Infrastructure.Observability;

namespace UnshackledWord.Infrastructure.Tests.Observability;

public class ObservabilityTests
{
    [Fact]
    public void Disabled_RegistersNoTracerProvider()
    {
        var services = new ServiceCollection();
        services.AddUnshackledTelemetry(new ConfigurationBuilder().Build(), "test");

        using var provider = services.BuildServiceProvider();
        provider.GetService<TracerProvider>().ShouldBeNull();
        services.ShouldBeEmpty();
    }

    [Fact]
    public void Enabled_RegistersTracerProvider()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Telemetry:Enabled"] = "true" }).Build();
        var services = new ServiceCollection();
        services.AddUnshackledTelemetry(config, "test");

        using var provider = services.BuildServiceProvider();
        provider.GetService<TracerProvider>().ShouldNotBeNull();
    }

    [Theory]
    [InlineData("not a uri", "1")]
    [InlineData("http://x:4317", "0")]
    [InlineData("http://x:4317", "1.5")]
    public void Enabled_WithInvalidOptions_Throws(string endpoint, string ratio)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Telemetry:Enabled"] = "true", ["Telemetry:OtlpEndpoint"] = endpoint, ["Telemetry:TraceSampleRatio"] = ratio,
        }).Build();

        Should.Throw<FluentValidation.ValidationException>(() => new ServiceCollection().AddUnshackledTelemetry(config, "test"));
    }

    [Theory]
    [InlineData("https://x/v1/models?key=secret#frag", "https://x/v1/models")]
    [InlineData("https://x/v1/models", "https://x/v1/models")]
    public void SpanSanitizer_StripsQuery(string url, string expected) => SpanSanitizer.StripQuery(url).ShouldBe(expected);

    [Fact]
    public void Migrations_HighestApplied_ReadsScriptNumbers()
    {
        string[] names = ["UnshackledWord.Tooling.Postgres.Migration.scripts.0001_Init.sql", "UnshackledWord.Tooling.Postgres.Migration.scripts.0036_Monitoring.sql", "x.0002_B.sql"];
        MigrationsHealthCheck.HighestApplied(names).ShouldBe(36);
        MigrationsHealthCheck.HighestApplied([]).ShouldBe(0);
    }

    [Fact]
    public void Migrations_Evaluate_UnhealthyWhenBehind()
    {
        string[] names = ["s.scripts.0035_A.sql"];
        MigrationsHealthCheck.Evaluate(names, 36).Status.ShouldBe(HealthStatus.Unhealthy);
        MigrationsHealthCheck.Evaluate(names, 35).Status.ShouldBe(HealthStatus.Healthy);
        MigrationsHealthCheck.Evaluate([], 1).Status.ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public void ExpectedMigration_EqualsHighestScript()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "UnshackledWord.slnx")))
        {
            dir = dir.Parent;
        }

        dir.ShouldNotBeNull();
        var scripts = Directory.GetFiles(Path.Combine(dir.FullName, "src", "UnshackledWord.Tooling.Postgres.Migration", "scripts"), "*.sql", SearchOption.AllDirectories);
        MigrationsHealthCheck.HighestApplied(scripts.Select(Path.GetFileName)!).ShouldBe(MigrationsHealthCheck.ExpectedMigration);
    }
}
