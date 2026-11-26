using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Extensions;
using UnshackledWord.Infrastructure.Observability;
using UnshackledWord.Infrastructure.Repositories;
using UnshackledWord.Persistence.Postgres.Extensions;

namespace UnshackledWord.Tooling.AgentWorker;

/// <summary>
/// Thin command line for the agent worker (roadmap 0016.6): the same services as the API's <c>api/maintainer/agent-runs</c> endpoints
/// (<see cref="IAgentRunService"/> and <see cref="AgentRunExecutor"/>), run in this process. Replaces <c>Tooling.AiWorker</c>.
/// <code>
/// estimate --book 41 --from 1:1 --to 1:8 --profile ollama [--model tag] [--mode redo|fill-open|lemma|lxx-tag]
/// run      --book 41 --from 1:1 --to 1:8 --profile ollama --user 4 [--model tag] [--mode ...] [--max-tokens N] [--max-cost USD]
/// lxx-tag (roadmap 0028.7): an OT book, chapters and verses in Rahlfs numbering, e.g. --book 8 --from 1:1 --to 4:22 --mode lxx-tag
///          [--include-confirmed] also tags units whose words are all confirmed; [--text-form og|th] one Daniel text form (0028.7f)
/// resume   --run 12 [--max-tokens N] [--max-cost USD]
/// list
/// </code>
/// </summary>
public partial class Program
{
    private const string ServiceName = "unshackledword-agentworker";

    public static async Task<int> Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Configuration.AddEnvironmentVariables("UNSHACKLEDWORD_");
        AddLocalSecrets(builder.Configuration);
        builder.Services.AddSerilog((_, logger) => logger.ReadFrom.Configuration(builder.Configuration).WriteToTelemetry(builder.Configuration, ServiceName));
        builder.AddUnshackledTelemetry(ServiceName);
        builder.Services.AddPostgresPersistence();
        builder.Services.AddInfrastructureServices();
        builder.Services.AddRepositories();

        using var host = builder.Build();
        var options = new Options(args);

        try
        {
            using var scope = host.Services.CreateScope();

            return await RunAsync(options, scope.ServiceProvider);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e.Message);

            return 1;
        }
    }

    private static async Task<int> RunAsync(Options o, IServiceProvider services)
    {
        var service = services.GetRequiredService<IAgentRunService>();
        var runs = services.GetRequiredService<IAgentRunRepository>();

        foreach (var error in AgentProfiles.From(services.GetRequiredService<IConfiguration>()).Validate())
        {
            Console.Error.WriteLine("config: " + error);

            return 2;
        }

        switch (o.Command)
        {
            case "estimate":
            {
                var result = await service.EstimateAsync(o.Range());
                if (result.Estimate is null)
                {
                    Console.Error.WriteLine(result.Error);

                    return 1;
                }

                var e = result.Estimate;
                Console.WriteLine($"{e.Packages} package(s), {e.Verses} verse(s) ({e.SkippedVerses} skipped), prefix {e.PrefixTokens} tokens, prompt {e.PromptVersion}");
                Console.WriteLine($"tokens: input {e.EstimatedInputTokens} (cached {e.EstimatedCachedInputTokens}), output {e.EstimatedOutputTokens}, total {e.EstimatedTotalTokens} (with 30 % margin), cost {e.EstimatedCostUsd} USD");
                e.Warnings.ForEach(w => Console.WriteLine("warning: " + w));

                return 0;
            }

            case "run":
            {
                var request = new StartAgentRunRequest
                {
                    Book = o.Range().Book, FromChapter = o.Range().FromChapter, FromVerse = o.Range().FromVerse, ToChapter = o.Range().ToChapter, ToVerse = o.Range().ToVerse,
                    Profile = o.Range().Profile, Model = o.Range().Model, Mode = o.Range().Mode, MaxTokens = o.Long("max-tokens"), MaxCostUsd = o.Decimal("max-cost"),
                };
                var created = await service.CreateRunAsync(request, o.Int("user") ?? throw new ArgumentException("--user <maintainer user id> is required"));
                if (created.Outcome != AgentRunPrepareOutcome.Ok)
                {
                    Console.Error.WriteLine(created.Error);

                    return 1;
                }

                return await ExecuteAsync(services, runs, created.RunId);
            }

            case "resume":
            {
                var id = o.Int("run") ?? throw new ArgumentException("--run <id> is required");
                var prepared = await service.PrepareResumeAsync(new ResumeAgentRunRequest { RunId = id, MaxTokens = o.Long("max-tokens"), MaxCostUsd = o.Decimal("max-cost") }, activeInProcess: false);
                if (prepared.Outcome != AgentRunPrepareOutcome.Ok)
                {
                    Console.Error.WriteLine(prepared.Error);

                    return 1;
                }

                return await ExecuteAsync(services, runs, id);
            }

            case "list":
                foreach (var r in await runs.ListRunsAsync(20))
                {
                    Console.WriteLine($"#{r.Id} {r.Status,-16} {r.Profile}/{r.Model} {r.FromRefId}-{r.ToRefId} proposed {r.ProposedVerses} tokens {r.InputTokens + r.OutputTokens} cost {r.CostUsd} USD");
                }

                return 0;

            default:
                Console.Error.WriteLine("commands: estimate | run | resume | list (see the comment in Program.cs)");

                return 2;
        }
    }

    private static async Task<int> ExecuteAsync(IServiceProvider services, IAgentRunRepository runs, int runId)
    {
        Console.WriteLine($"run #{runId} started");
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        await services.GetRequiredService<AgentRunExecutor>().RunAsync(runId, cts.Token);
        var run = (await runs.GetRunAsync(runId))!;
        Console.WriteLine($"run #{runId} {run.Status}: {run.ProposedVerses} verse(s) proposed ({run.FlaggedVerses} flagged), tokens in {run.InputTokens} (cached {run.CachedInputTokens}) out {run.OutputTokens}, cost {run.CostUsd} USD");
        if (!string.IsNullOrEmpty(run.StatusNote))
        {
            Console.WriteLine(run.StatusNote);
        }

        return run.Status is AgentRunStatuses.Completed ? 0 : 1;
    }

    static partial void AddLocalSecrets(ConfigurationManager builder);

    private sealed class Options
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

        public Options(string[] args)
        {
            Command = args.FirstOrDefault(a => !a.StartsWith("--")) ?? string.Empty;
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("--"))
                {
                    // A flag without a value (`--include-confirmed`) counts as "true".
                    _values[args[i][2..]] = i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[i + 1] : "true";
                }
            }
        }

        public string Command { get; }

        public int? Int(string key) => _values.TryGetValue(key, out var v) ? int.Parse(v) : null;

        public long? Long(string key) => _values.TryGetValue(key, out var v) ? long.Parse(v) : null;

        public decimal? Decimal(string key) => _values.TryGetValue(key, out var v) ? decimal.Parse(v, System.Globalization.CultureInfo.InvariantCulture) : null;

        public bool Flag(string key) => _values.TryGetValue(key, out var v) && !v.Equals("false", StringComparison.OrdinalIgnoreCase);

        public AgentRunRangeRequest Range()
        {
            var (fromChapter, fromVerse) = Ref(_values.GetValueOrDefault("from") ?? throw new ArgumentException("--from <chapter:verse> is required"));
            var (toChapter, toVerse) = _values.TryGetValue("to", out var to) ? Ref(to) : (fromChapter, fromVerse);

            return new AgentRunRangeRequest
            {
                Book = Int("book") ?? throw new ArgumentException("--book <1..66> is required"),
                FromChapter = fromChapter, FromVerse = fromVerse, ToChapter = toChapter, ToVerse = toVerse,
                Profile = _values.GetValueOrDefault("profile") ?? throw new ArgumentException("--profile <name> is required"),
                Model = _values.GetValueOrDefault("model"),
                Mode = _values.GetValueOrDefault("mode"),
                IncludeConfirmed = Flag("include-confirmed"),
                TextForm = _values.GetValueOrDefault("text-form"),
            };
        }

        private static (int Chapter, int Verse) Ref(string text)
        {
            var parts = text.Split(':');

            return (int.Parse(parts[0]), int.Parse(parts[1]));
        }
    }
}
