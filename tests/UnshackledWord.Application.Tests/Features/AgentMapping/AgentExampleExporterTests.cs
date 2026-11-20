using System.Text.Json;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Application.Tests.Features.AgentMapping;

public class AgentExampleExporterTests
{
    [Fact]
    public async Task AnswerKeepsHintsSilent_AndWritesChangedTokensAndLemmas()
    {
        var record = new AgentProposalRecord
        {
            Id = 7,
            BibleBookId = 1,
            Chapter = 1,
            Verse = 1,
            Language = "hebrew",
            Status = AgentProposalStatuses.Open,
            // Word 10 keeps its hint (101), word 11 moves from hint 102 to 103, word 12 is new (lemma changes too).
            LinksJson = """[{"elbWordId":10,"stepWordId":101},{"elbWordId":11,"stepWordId":103},{"elbWordId":12,"stepWordId":102}]""",
            LemmasJson = """[{"elbWordId":12,"lemma":"Gott","stts":"NN"}]""",
        };

        var map = new AgentPackageMap { Language = "hebrew", Mode = AgentModes.Redo, BookId = 1 };
        var verse = new AgentVerseMap { Ref = "1Mo 1:1", Chapter = 1, Verse = 1 };
        verse.German["g1"] = new AgentGermanMap { Id = 10, Text = "Im" };
        verse.German["g2"] = new AgentGermanMap { Id = 11, Text = "Anfang" };
        verse.German["g3"] = new AgentGermanMap { Id = 12, Text = "Gott", Lemma = "Got", Stts = "NN" };
        verse.Source["s1"] = new AgentSourceMap { Id = 101 };
        verse.Source["s2"] = new AgentSourceMap { Id = 102 };
        verse.Source["s3"] = new AgentSourceMap { Id = 103 };
        verse.Hints.Add(new AgentLinkMap { G = "g1", S = "s1" });
        verse.Hints.Add(new AgentLinkMap { G = "g2", S = "s2" });
        map.Verses["v1"] = verse;

        var exporter = new AgentExampleExporter(new FakeReview(record), new FakeBuilder(map));
        var result = await exporter.ExportAsync(new GetAgentExampleRequest { Id = 7 }, 1);

        result.Outcome.ShouldBe(AgentExampleOutcome.Ok);
        var example = result.Example!;
        example.Ref.ShouldBe("1Mo 1:1");
        example.PromptVersion.ShouldBe("hebrew-v2");
        example.AnswerJson.ShouldBe("""{"packageId":"p_example","verses":[{"v":"v1","links":[["g2","s3"],["g3","s2"]],"added":[],"lemmas":[["g3","Gott"]],"parts":[],"unmapped":[]}]}""");
        example.Markdown.ShouldContain("## Example: 1Mo 1:1 [UNCONFIRMED]");
        using var package = JsonDocument.Parse(example.PackageJson);
        package.RootElement.GetProperty("packageId").GetString().ShouldBe("p_example");
    }

    [Fact]
    public async Task LemmaMode_IsRejected_AndUnknownProposalIsNotFound()
    {
        var exporter = new AgentExampleExporter(new FakeReview(null), new FakeBuilder(new AgentPackageMap()));

        (await exporter.ExportAsync(new GetAgentExampleRequest { Id = 1, Mode = "lemma" }, 1)).Outcome.ShouldBe(AgentExampleOutcome.BadRequest);
        (await exporter.ExportAsync(new GetAgentExampleRequest { Id = 1 }, 1)).Outcome.ShouldBe(AgentExampleOutcome.NotFound);
    }

    private sealed class FakeReview(AgentProposalRecord? record) : IAgentReviewRepository
    {
        public Task<IReadOnlyList<AgentProposalRecord>> ListAsync(int runId, string? status, CancellationToken token = default) => throw new NotSupportedException();

        public Task<AgentProposalRecord?> GetAsync(long id, CancellationToken token = default) => Task.FromResult(record);

        public Task<bool> TryCloseAsync(long id, string status, int reviewerUserId, CancellationToken token = default) => throw new NotSupportedException();
    }

    private sealed class FakeBuilder(AgentPackageMap map) : IAgentPackageBuilder
    {
        public Task<GetAgentPackageResponse?> BuildAsync(AgentBuildRequest request, CancellationToken token = default)
        {
            request.Maps?.Add(map);
            var response = new GetAgentPackageResponse();
            response.Packages.Add(new AgentPackageEnvelope { Package = new AgentPackageDto { PackageId = "x", Lang = "hebrew", Verses = [new AgentVerseDto { V = "v1", Ref = "1Mo 1:1" }] } });

            return Task.FromResult<GetAgentPackageResponse?>(response);
        }

        public int EstimateTokens(string text) => text.Length / 4;

        public Task<GetAgentPackageResponse?> BuildAsync(AgentRange range, AgentMode mode, int? tokenBudget, int userId, CancellationToken token = default) => throw new NotSupportedException();
    }
}
