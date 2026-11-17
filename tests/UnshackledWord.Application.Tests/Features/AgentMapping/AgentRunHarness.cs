using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Application.Tests.Features.AgentMapping;

/// <summary>Everything the run loop talks to, in memory: verses of one chapter, a builder that packages them, a proposal service, a run store and a scripted model client.</summary>
public sealed class AgentRunHarness
{
    public const int Book = 41;

    public AgentRunHarness(int verses = 6, int versesPerPackage = 2, string profileSettings = "")
    {
        Verses = verses;
        VersesPerPackage = versesPerPackage;
        Builder = new FakeBuilder(this);
        Proposals = new FakeProposalService(this);
        Runs = new FakeRunRepository(this);
        Store = Substitute.For<IAgentStoreRepository>();
        Client = new ScriptedClient(this);
        Prompts = Substitute.For<IAgentPromptProvider>();
        Prompts.GetVersion(Arg.Any<string>(), Arg.Any<string>()).Returns(new AgentPrompt("greek-v1", "greek", new string('p', 400)));
        Prompts.Get(Arg.Any<string>()).Returns(new AgentPrompt("greek-v1", "greek", new string('p', 400)));
        Clients = Substitute.For<IAgentModelClientFactory>();
        Clients.Create(Arg.Any<AgentProfile>()).Returns(Client);

        var settings = new Dictionary<string, string?>
        {
            ["AgentMapping:Profiles:p:Kind"] = "openai",
            ["AgentMapping:Profiles:p:BaseUrl"] = "http://localhost:9999/v1",
            ["AgentMapping:Profiles:p:Model"] = "m",
            ["AgentMapping:Profiles:p:MaxOutputTokens"] = "1000",
        };

        foreach (var pair in profileSettings.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=');
            settings[$"AgentMapping:Profiles:p:{kv[0]}"] = kv[1];
        }

        Configuration = new TestConfiguration(settings);
    }

    public int Verses { get; }
    public int VersesPerPackage { get; }
    public IConfiguration Configuration { get; }
    public FakeBuilder Builder { get; }
    public FakeProposalService Proposals { get; }
    public FakeRunRepository Runs { get; }
    public IAgentStoreRepository Store { get; }
    public ScriptedClient Client { get; }
    public IAgentPromptProvider Prompts { get; }
    public IAgentModelClientFactory Clients { get; }

    public static int RefOf(int verse) => AgentRange.RefId(Book, 1, verse);

    public AgentRunExecutor Executor() => new(Builder, Proposals, Runs, Store, Clients, Prompts, Configuration, NullLogger<AgentRunExecutor>.Instance);

    public AgentRunService Service() => new(Builder, Runs, Store, Prompts, Configuration);

    /// <summary>A run over all verses in the store, <c>queued</c>, with the given caps.</summary>
    public int NewRun(long? maxTokens = null, decimal? maxCost = null)
    {
        return Runs.Add(new AgentRunRecord
        {
            CreatedByUserId = 1,
            AiUserId = 2,
            Profile = "p",
            Model = "m",
            PromptVersion = "greek-v1",
            BibleBookId = Book,
            FromRefId = RefOf(1),
            ToRefId = RefOf(Verses),
            Mode = "redo",
            Status = AgentRunStatuses.Queued,
            MaxTokens = maxTokens,
            MaxCostUsd = maxCost,
        });
    }

    public sealed class PackageInfo
    {
        public Guid Id { get; init; }
        public List<(string V, int RefId)> Verses { get; init; } = [];
    }

    public sealed class FakeBuilder : IAgentPackageBuilder
    {
        private readonly AgentRunHarness _h;

        public FakeBuilder(AgentRunHarness harness)
        {
            _h = harness;
        }

        public ConcurrentDictionary<Guid, PackageInfo> Packages { get; } = new();
        public List<AgentBuildRequest> Requests { get; } = [];

        public Task<GetAgentPackageResponse?> BuildAsync(AgentRange range, AgentMode mode, int? tokenBudget, int userId, CancellationToken token = default) =>
            BuildAsync(new AgentBuildRequest { Range = range, Mode = mode, TokenBudget = tokenBudget, UserId = userId }, token);

        public Task<GetAgentPackageResponse?> BuildAsync(AgentBuildRequest request, CancellationToken token = default)
        {
            lock (Requests)
            {
                Requests.Add(request);
            }

            var from = AgentRange.RefId(Book, request.Range.FromChapter, request.Range.FromVerse);
            var to = AgentRange.RefId(Book, request.Range.ToChapter, request.Range.ToVerse);
            var per = Math.Min(_h.VersesPerPackage, request.MaxVersesPerPackage ?? int.MaxValue);
            var refs = Enumerable.Range(1, _h.Verses).Select(RefOf).Where(r => r >= from && r <= to && request.SkipRefIds?.Contains(r) != true).ToList();
            var response = new GetAgentPackageResponse { RunId = request.ExistingRunId ?? 0, Stats = new AgentTokenStats { Verses = refs.Count } };

            foreach (var chunk in refs.Chunk(per))
            {
                var id = Guid.NewGuid();
                var package = new AgentPackageDto { PackageId = id.ToString(), Lang = "greek" };
                var info = new PackageInfo { Id = id };
                for (var i = 0; i < chunk.Length; i++)
                {
                    package.Verses.Add(new AgentVerseDto { V = $"v{i + 1}", Ref = $"Mk 1:{chunk[i] % 1000}" });
                    info.Verses.Add(($"v{i + 1}", chunk[i]));
                }

                if (!request.DryRun)
                {
                    Packages[id] = info;
                }

                response.Packages.Add(new AgentPackageEnvelope { Package = package, EstimatedTokens = 100 * chunk.Length, FromRefId = chunk[0], ToRefId = chunk[^1] });
            }

            return Task.FromResult<GetAgentPackageResponse?>(response.Packages.Count == 0 ? null : response);
        }

        public int EstimateTokens(string text) => text.Length / 4;
    }

    public sealed class FakeProposalService : IAgentProposalService
    {
        private readonly AgentRunHarness _h;

        public FakeProposalService(AgentRunHarness harness)
        {
            _h = harness;
        }

        /// <summary>Decides per verse and attempt (1-based) whether it is rejected; default accepts everything.</summary>
        public Func<int, int, bool> Rejects { get; set; } = (_, _) => false;

        public ConcurrentDictionary<int, int> Attempts { get; } = new();
        public ConcurrentBag<int> Proposed { get; } = [];

        public Task<AgentSubmitResult> SubmitAsync(string packageId, JsonElement answer, CancellationToken token = default)
        {
            var info = _h.Builder.Packages[Guid.Parse(packageId)];
            var response = new SubmitAgentProposalResponse { PackageId = packageId };
            var answered = answer.GetProperty("verses").EnumerateArray().Select(e => e.GetProperty("v").GetString()!).ToHashSet();

            foreach (var (v, refId) in info.Verses)
            {
                if (!answered.Contains(v))
                {
                    response.NotAnswered.Add(v);
                    continue;
                }

                var attempt = Attempts.AddOrUpdate(refId, 1, (_, n) => n + 1);
                if (Rejects(refId, attempt))
                {
                    response.Rejected++;
                    response.Verses.Add(new AgentVerseResultDto { V = v, Status = AgentVerseStatus.Rejected, Messages = [$"{v} g1: source s9 belongs to another verse"] });
                }
                else
                {
                    response.Accepted++;
                    response.Verses.Add(new AgentVerseResultDto { V = v, Status = AgentVerseStatus.Accepted, ProposalId = refId });
                    Proposed.Add(refId);
                    _h.Runs.Proposed.Add(refId);
                }
            }

            return Task.FromResult(new AgentSubmitResult { Outcome = AgentSubmitOutcome.Ok, Response = response });
        }
    }

    public sealed class FakeRunRepository : IAgentRunRepository
    {
        private readonly AgentRunHarness _h;
        private readonly Dictionary<int, AgentRunRecord> _runs = [];
        private readonly object _gate = new();

        public FakeRunRepository(AgentRunHarness harness)
        {
            _h = harness;
        }

        public ConcurrentBag<int> Proposed { get; } = [];
        public List<(int RunId, Guid PackageId, long In, long Cached, long Out, decimal Cost)> Usage { get; } = [];
        public List<string> Statuses { get; } = [];

        public int Add(AgentRunRecord run)
        {
            lock (_gate)
            {
                run.Id = _runs.Count + 1;
                _runs[run.Id] = run;

                return run.Id;
            }
        }

        public AgentRunRecord Get(int id)
        {
            lock (_gate)
            {
                return _runs[id];
            }
        }

        public Task<AgentRunRecord?> GetRunAsync(int runId, CancellationToken token = default)
        {
            lock (_gate)
            {
                return Task.FromResult(_runs.GetValueOrDefault(runId));
            }
        }

        public Task<IReadOnlyList<AgentRunRecord>> ListRunsAsync(int limit, CancellationToken token = default) => Task.FromResult<IReadOnlyList<AgentRunRecord>>(_runs.Values.ToList());

        public Task SetRunStatusAsync(int runId, string status, string? note, CancellationToken token = default)
        {
            lock (_gate)
            {
                _runs[runId].Status = status;
                _runs[runId].StatusNote = note;
                Statuses.Add(status);
            }

            return Task.CompletedTask;
        }

        public Task<bool> SetRunStatusUnlessCancelledAsync(int runId, string status, string? note, CancellationToken token = default)
        {
            lock (_gate)
            {
                if (_runs[runId].Status == AgentRunStatuses.Cancelled)
                {
                    return Task.FromResult(false);
                }

                _runs[runId].Status = status;
                _runs[runId].StatusNote = note;
                Statuses.Add(status);

                return Task.FromResult(true);
            }
        }

        public Task<string?> GetRunStatusAsync(int runId, CancellationToken token = default)
        {
            lock (_gate)
            {
                return Task.FromResult<string?>(_runs[runId].Status);
            }
        }

        public Task AddUsageAsync(int runId, Guid packageId, long inputTokens, long cachedInputTokens, long outputTokens, decimal costUsd, CancellationToken token = default)
        {
            lock (_gate)
            {
                var run = _runs[runId];
                run.InputTokens += inputTokens;
                run.CachedInputTokens += cachedInputTokens;
                run.OutputTokens += outputTokens;
                run.CostUsd += costUsd;
                Usage.Add((runId, packageId, inputTokens, cachedInputTokens, outputTokens, costUsd));
            }

            return Task.CompletedTask;
        }

        public Task SetCapsAsync(int runId, long? maxTokens, decimal? maxCostUsd, CancellationToken token = default)
        {
            lock (_gate)
            {
                _runs[runId].MaxTokens = maxTokens;
                _runs[runId].MaxCostUsd = maxCostUsd;
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlySet<int>> GetProposedRefIdsAsync(int runId, CancellationToken token = default) => Task.FromResult<IReadOnlySet<int>>(Proposed.ToHashSet());

        public Task<int> GetOrCreateAiUserAsync(string model, CancellationToken token = default) => Task.FromResult(7);

        public Task<int> MarkInterruptedRunsAsync(string note, CancellationToken token = default) => Task.FromResult(0);
    }

    public sealed class ScriptedClient : IAgentModelClient
    {
        private readonly AgentRunHarness _h;
        private int _calls;
        private int _running;

        public ScriptedClient(AgentRunHarness harness)
        {
            _h = harness;
        }

        /// <summary>Called with the request and the 1-based call number; return a result, or throw an <see cref="AgentModelException"/>. Default: answers every verse of the package.</summary>
        public Func<AgentModelRequest, int, Task<AgentModelResult>>? Script { get; set; }

        public ConcurrentQueue<AgentModelRequest> Requests { get; } = new();
        public int Calls => _calls;
        public int MaxConcurrent { get; private set; }
        public long InputPerCall { get; set; } = 300;
        public long OutputPerCall { get; set; } = 50;

        public async Task<AgentModelResult> CompleteAsync(AgentModelRequest request, CancellationToken token)
        {
            var number = Interlocked.Increment(ref _calls);
            Requests.Enqueue(request);
            var running = Interlocked.Increment(ref _running);
            lock (this)
            {
                MaxConcurrent = Math.Max(MaxConcurrent, running);
            }

            try
            {
                return Script is not null ? await Script(request, number) : Answer(request);
            }
            finally
            {
                Interlocked.Decrement(ref _running);
            }
        }

        public AgentModelResult Answer(AgentModelRequest request, string[]? onlyVerses = null)
        {
            var package = JsonDocument.Parse(request.PackageJson).RootElement;
            var ids = package.GetProperty("verses").EnumerateArray().Select(v => v.GetProperty("v").GetString()!).Where(v => onlyVerses is null || onlyVerses.Contains(v));
            var json = JsonSerializer.Serialize(new { packageId = package.GetProperty("packageId").GetString(), verses = ids.Select(v => new { v, links = Array.Empty<string[]>() }) });

            return new AgentModelResult { Json = json, InputTokens = InputPerCall, CachedInputTokens = 100, OutputTokens = OutputPerCall, Model = "m" };
        }
    }
}
