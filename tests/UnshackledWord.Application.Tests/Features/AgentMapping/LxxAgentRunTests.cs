using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.AgentMapping.Lxx;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Application.Tests.Features.AgentMapping;

/// <summary>
/// End to end without any provider (roadmap 0028.7): the real run loop, package builder, prompt, proposal service and LXX submitter over Ruth 1:1-2 of
/// the fixture; the model is <see cref="FakeModel"/> behind the harness's scripted client, the store an in-memory one with the SQL's supersede key.
/// </summary>
public class LxxAgentRunTests
{
    [Fact]
    public async Task ARunOverTwoVerses_StoresOneLxxProposalPerVerse_DropsBadWordsWithFlags_AndLogsUsage()
    {
        var harness = new AgentRunHarness();
        var data = new LxxAgentTestData();
        var store = new MemoryStore();
        var configuration = harness.Configuration;
        var lxxBuilder = new LxxAgentPackageBuilder(data, store, configuration, TimeProvider.System);
        var builder = new AgentPackageBuilder(Substitute.For<IAgentDataRepository>(), store, configuration, TimeProvider.System, lxxBuilder);
        var submitter = new LxxProposalSubmitter(data, store, NullLogger<LxxProposalSubmitter>.Instance);
        var proposals = new AgentProposalService(store, Substitute.For<IAgentDataRepository>(), Substitute.For<ISttsTagRepository>(), new AgentProposalStore(store), configuration,
            TimeProvider.System, NullLogger<AgentProposalService>.Instance, submitter);
        var prompts = new AgentPromptProvider(AgentPromptProvider.ResolveDirectory(new TestConfiguration(new Dictionary<string, string?>())));
        var executor = new AgentRunExecutor(builder, proposals, harness.Runs, store, harness.Clients, prompts, configuration, NullLogger<AgentRunExecutor>.Instance);

        // The fake model: prefills and first candidates, plus one Strong's TBESG lacks in verse 1 and one bad code in verse 2.
        harness.Client.Script = (request, _) =>
        {
            var answer = FakeModel.Answer(JsonNode.Parse(request.PackageJson)!);
            answer["verses"]![0]!["words"]![2]![2] = "G99999";
            answer["verses"]![1]!["words"]![1]![3] = "V-XYZ";
            return Task.FromResult(new AgentModelResult { Json = answer.ToJsonString(), InputTokens = 4000, CachedInputTokens = 3000, OutputTokens = 900, Model = "m" });
        };

        var runId = harness.Runs.Add(new AgentRunRecord
        {
            CreatedByUserId = 1, AiUserId = 2, Profile = "p", Model = "m", PromptVersion = "lxx-tag-v1", BibleBookId = LxxAgentTestData.Ruth,
            FromRefId = 8_001_001, ToRefId = 8_001_002, Mode = AgentModes.LxxTag, Status = AgentRunStatuses.Queued,
        });

        await executor.RunAsync(runId);

        harness.Runs.Get(runId).Status.ShouldBe(AgentRunStatuses.Completed, harness.Runs.Get(runId).StatusNote);
        var request = harness.Client.Requests.Single();
        request.JsonSchema.ShouldBe(AgentAnswerSchema.LxxJson);
        request.StaticPrefix.ShouldBe(prompts.GetVersion(AgentPromptProvider.LxxTagLanguage, "lxx-tag-v1").StaticPrefix);
        request.PackageJson.ShouldContain("\"lang\":\"lxx\"");
        request.PackageJson.ShouldNotContain("\"de\":");

        store.Proposals.Count.ShouldBe(2);
        store.Proposals.ShouldAllBe(p => p.Insert.Language == "lxx" && p.Status == "open" && p.Insert.TextForm == "" && p.Insert.Subverse == 0);
        store.Proposals.Select(p => (p.Insert.BibleBookId, p.Insert.Chapter, p.Insert.Verse)).ShouldBe([(8, 1, 1), (8, 1, 2)]);

        var first = Flags(store.Proposals[0].Insert);
        first.LxxVerseId.ShouldBe(data.Unit(8, 1, 1).Id);
        first.HebRefId.ShouldBe(8_001_001);
        first.Flags.Select(f => (f.Code, f.Position)).ShouldBe([(LxxFlagCodes.StrongsUnknown, 3), (LxxFlagCodes.NoStrongs, 25)]);
        Flags(store.Proposals[1].Insert).Flags.Select(f => (f.Code, f.Position)).ShouldBe([(LxxFlagCodes.MorphInvalid, 2), (LxxFlagCodes.NoStrongs, 24)]);

        var words = JsonSerializer.Deserialize<List<LxxWordProposal>>(store.Proposals[0].Insert.LemmasJson!, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        words.Count.ShouldBe(32, "34 words, ἐν (3) and Μωάβ (25) dropped");
        words.Select(w => w.Position).ShouldNotContain(3);
        var links = JsonSerializer.Deserialize<List<LxxLinkProposal>>(store.Proposals[0].Insert.LinksJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        links.Single().StepHebrewWordId.ShouldBe(data.Hebrew.Single(h => h.RefId == 8_001_001 && h.Position == 1).Id);

        store.Packages.Values.Single().Status.ShouldBe(AgentPackageStatuses.Completed);
        harness.Runs.Usage.Single().ShouldBe((runId, store.Packages.Keys.Single(), 4000L, 3000L, 900L, 0m));
        data.Words.ShouldAllBe(w => w.TagSource != "ai", "an AI run never writes LxxWordTags");
    }

    [Fact]
    public async Task ASecondRun_SupersedesOnlyTheLxxProposalOfTheSameUnit()
    {
        var store = new MemoryStore();
        var hebrew = new AgentProposalInsert { BibleBookId = 8, Chapter = 1, Verse = 1, Language = "hebrew", FlagsJson = "[]" };
        AgentProposalInsert Lxx(int subverse = 0) => new()
        {
            BibleBookId = 8, Chapter = 1, Verse = 1, Language = "lxx", Subverse = subverse,
            FlagsJson = JsonSerializer.Serialize(new LxxProposalFlags { Subverse = subverse }, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
        };

        (await store.StoreProposalsAsync(1, Guid.NewGuid(), [hebrew])).Superseded.ShouldBe(0);
        (await store.StoreProposalsAsync(2, Guid.NewGuid(), [Lxx()])).Superseded.ShouldBe(0);
        (await store.StoreProposalsAsync(3, Guid.NewGuid(), [Lxx(1)])).Superseded.ShouldBe(0);
        (await store.StoreProposalsAsync(4, Guid.NewGuid(), [Lxx()])).Superseded.ShouldBe(1);

        store.Proposals.Where(p => p.Status == "open").Select(p => (p.Insert.Language, p.Insert.Subverse)).ShouldBe([("hebrew", 0), ("lxx", 1), ("lxx", 0)]);
    }

    private static LxxProposalFlags Flags(AgentProposalInsert insert) =>
        JsonSerializer.Deserialize<LxxProposalFlags>(insert.FlagsJson!, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    /// <summary>Packages and proposals in memory; the supersede key is the SQL's (book, chapter, verse, language, text form, subverse).</summary>
    private sealed class MemoryStore : IAgentStoreRepository
    {
        public Dictionary<Guid, AgentPackageRecord> Packages { get; } = [];
        public List<(long Id, Guid PackageId, AgentProposalInsert Insert, string Status)> Proposals { get; } = [];

        public Task<int> CreateRunAsync(AgentRunInsert run, CancellationToken token = default) => Task.FromResult(99);

        public Task InsertPackageAsync(AgentPackageInsert package, CancellationToken token = default)
        {
            Packages[package.Id] = new AgentPackageRecord
            {
                Id = package.Id, RunId = package.RunId, WordIdsHash = package.WordIdsHash, LocalIdMap = package.LocalIdMapJson, Status = AgentPackageStatuses.Open,
                ExpiresAt = package.ExpiresAt.UtcDateTime,
            };
            return Task.CompletedTask;
        }

        public Task<AgentPackageRecord?> GetPackageAsync(Guid packageId, CancellationToken token = default) => Task.FromResult(Packages.GetValueOrDefault(packageId));

        public Task SetPackageStatusAsync(Guid packageId, string status, bool countAttempt, CancellationToken token = default)
        {
            if (Packages.TryGetValue(packageId, out var p))
            {
                p.Status = status;
                p.Attempts += countAttempt ? 1 : 0;
            }

            return Task.CompletedTask;
        }

        public Task<int> CountProposedVersesAsync(Guid packageId, CancellationToken token = default) =>
            Task.FromResult(Proposals.Where(p => p.PackageId == packageId).Select(p => Key(p.Insert)).Distinct().Count());

        public Task<AgentProposalStoreResult> StoreProposalsAsync(int runId, Guid packageId, IReadOnlyList<AgentProposalInsert> proposals, CancellationToken token = default)
        {
            var superseded = 0;
            var ids = new List<long>();
            foreach (var proposal in proposals)
            {
                for (var i = 0; i < Proposals.Count; i++)
                {
                    if (Proposals[i].Status == "open" && Key(Proposals[i].Insert) == Key(proposal))
                    {
                        Proposals[i] = Proposals[i] with { Status = "superseded" };
                        superseded++;
                    }
                }

                var id = Proposals.Count + 1L;
                Proposals.Add((id, packageId, proposal, "open"));
                ids.Add(id);
            }

            return Task.FromResult(new AgentProposalStoreResult { ProposalIds = ids, Superseded = superseded });
        }

        private static (int, int, int, string, string, int) Key(AgentProposalInsert p) => (p.BibleBookId, p.Chapter, p.Verse, p.Language, p.TextForm, p.Subverse);
    }
}
