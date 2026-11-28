using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.WebApi.Admin;
using UnshackledWord.Persistence.Postgres.Services;
using UnshackledWord.Seeding;
using UnshackledWord.Seeding.Abstractions;
using UnshackledWord.Seeding.Catalog;

namespace UnshackledWord.Tooling.SeedDb.Tests.Seeding;

public class SeedStepRunnerTests
{
    public sealed class FirstRunner(List<string> calls) : IRunner
    {
        public Task Run(CancellationToken token = default)
        {
            calls.Add($"first:{SeedConnectionContext.Current}");
            return Task.CompletedTask;
        }
    }

    public sealed class SecondRunner(List<string> calls) : IRunner
    {
        public Task Run(CancellationToken token = default)
        {
            calls.Add("second");
            throw new InvalidOperationException("boom");
        }
    }

    public sealed class ThirdRunner(List<string> calls) : IRunner
    {
        public Task Run(CancellationToken token = default)
        {
            calls.Add("third");
            return Task.CompletedTask;
        }
    }

    private sealed class Listener(List<string> seen) : ISeedDataChanged
    {
        public Task SeedDataChangedAsync(string stepKey, CancellationToken token)
        {
            seen.Add(stepKey);
            return Task.CompletedTask;
        }
    }

    private sealed class Lines : IProgress<string>
    {
        public List<string> Items { get; } = [];
        public void Report(string value) => Items.Add(value);
    }

    private static SeedStep Step(string key, Type runner) => new(key, SetupGroups.Base, SetupSources.Assets, null, runner, [], [], true, true);

    private static (SeedStepRunner Runner, SetupRunState State, List<string> Calls, List<string> Notified) Build(int? timeout = null)
    {
        var calls = new List<string>();
        var notified = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(calls);
        services.AddScoped<FirstRunner>();
        services.AddScoped<SecondRunner>();
        services.AddScoped<ThirdRunner>();
        var provider = services.BuildServiceProvider();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:PostgresConnection"] = "Host=h;Database=d;Username=u;Password=p" })
            .Build();
        var state = new SetupRunState();
        var options = Options.Create(timeout is null ? new SeedingOptions() : new SeedingOptions { CommandTimeoutSeconds = timeout.Value });
        var runner = new SeedStepRunner(provider.GetRequiredService<IServiceScopeFactory>(), config, options, state, [new Listener(notified)], NullLogger<SeedStepRunner>.Instance);
        return (runner, state, calls, notified);
    }

    [Fact]
    public async Task Steps_run_in_order_with_progress_notification_and_the_seed_command_timeout()
    {
        var (runner, state, calls, notified) = Build();
        var lines = new Lines();
        SeedStep[] catalog = [Step("a", typeof(FirstRunner)), Step("c", typeof(ThirdRunner))];

        var result = await runner.RunAsync(["a", "c"], lines, TestContext.Current.CancellationToken, catalog);

        calls.Count.ShouldBe(2);
        calls[0].ShouldContain("Command Timeout=900");
        calls[1].ShouldBe("third");
        notified.ShouldBe(["a", "c"]);
        state.CompletedKeys().ShouldBe(["a", "c"], ignoreOrder: true);
        state.FailedKeys().ShouldBeEmpty();
        result.Steps.Select(s => s.Kind).ShouldBe(["a", "c"]);
        lines.Items.Count(l => l.Contains("start")).ShouldBe(2);
        lines.Items.ShouldContain(l => l.StartsWith("[1/2] a: done"));
        SeedConnectionContext.Current.ShouldBeNull("the job's connection string does not leak out of the run");
    }

    [Fact]
    public async Task The_first_failure_stops_the_run_and_marks_the_step_failed()
    {
        var (runner, state, calls, notified) = Build();
        var lines = new Lines();
        SeedStep[] catalog = [Step("a", typeof(FirstRunner)), Step("b", typeof(SecondRunner)), Step("c", typeof(ThirdRunner))];

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => runner.RunAsync(["a", "b", "c"], lines, TestContext.Current.CancellationToken, catalog));

        ex.Message.ShouldBe("boom");
        calls.ShouldNotContain("third");
        notified.ShouldBe(["a"]);
        state.FailedKeys().ShouldBe(["b"]);
        state.CompletedKeys().ShouldBe(["a"]);
        lines.Items.ShouldContain(l => l.Contains("[2/3] b: failed") && l.Contains("boom"));
    }

    [Fact]
    public async Task A_later_success_clears_the_failed_mark()
    {
        var (runner, state, _, _) = Build();
        state.MarkFailed("a");
        await runner.RunAsync(["a"], new Lines(), TestContext.Current.CancellationToken, [Step("a", typeof(FirstRunner))]);
        state.FailedKeys().ShouldBeEmpty();
        state.CompletedKeys().ShouldBe(["a"]);
    }

    [Fact]
    public async Task The_timeout_comes_from_config()
    {
        var (runner, _, calls, _) = Build(timeout: 1200);
        await runner.RunAsync(["a"], new Lines(), TestContext.Current.CancellationToken, [Step("a", typeof(FirstRunner))]);
        calls[0].ShouldContain("Command Timeout=1200");
    }

    [Fact]
    public void The_seed_connection_string_keeps_the_rest_and_sets_the_timeout()
    {
        var cs = SeedStepRunner.BuildSeedConnectionString("Host=localhost;Port=5544;Database=postgres;Username=postgres;Password=test;Command Timeout=30", 900);
        var builder = new NpgsqlConnectionStringBuilder(cs);
        builder.CommandTimeout.ShouldBe(900);
        builder.Host.ShouldBe("localhost");
        builder.Port.ShouldBe(5544);
    }

    [Fact]
    public async Task The_connection_factory_uses_the_job_connection_string_inside_a_job()
    {
        var connectionString = Environment.GetEnvironmentVariable("UNSHACKLEDWORD_TEST_POSTGRES");
        Assert.SkipWhen(string.IsNullOrEmpty(connectionString), "UNSHACKLEDWORD_TEST_POSTGRES is not set.");

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:PostgresConnection"] = connectionString }).Build();
        await using var factory = new PostgresDbConnectionFactory(config, NullLogger<PostgresDbConnectionFactory>.Instance);

        using (var plain = (NpgsqlConnection)await factory.CreateDbConnectionAsync(TestContext.Current.CancellationToken))
        {
            plain.CommandTimeout.ShouldBe(new NpgsqlConnectionStringBuilder(connectionString).CommandTimeout);
        }

        using (SeedConnectionContext.Use(SeedStepRunner.BuildSeedConnectionString(connectionString!, 900)))
        using (var seed = (NpgsqlConnection)await factory.CreateDbConnectionAsync(TestContext.Current.CancellationToken))
        {
            seed.CommandTimeout.ShouldBe(900);
        }
    }
}
