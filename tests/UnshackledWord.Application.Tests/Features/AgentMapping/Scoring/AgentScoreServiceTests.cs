using System.Text.Json;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.AgentMapping.Scoring;
using UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping;
using static UnshackledWord.Application.Tests.Features.AgentMapping.Scoring.ScoringTestData;

namespace UnshackledWord.Application.Tests.Features.AgentMapping.Scoring;

public class AgentScoreServiceTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    private readonly IAgentRunRepository _runs = Substitute.For<IAgentRunRepository>();
    private readonly IAgentScoreRepository _scores = Substitute.For<IAgentScoreRepository>();
    private readonly IAgentDataRepository _data = Substitute.For<IAgentDataRepository>();
    private readonly IAgentReferenceStore _references = Substitute.For<IAgentReferenceStore>();

    private AgentScoreService Service()
    {
        _runs.GetRunAsync(7, Arg.Any<CancellationToken>()).Returns(new AgentRunRecord { Id = 7, BibleBookId = Book, Mode = AgentModes.Lemma, Profile = "gemma", Model = "gemma4", PromptVersion = "lemma-greek-v1", Status = "completed", FromRefId = 41001001, ToRefId = 41001040 });
        _data.GetRangeAsync(Book, 1, 17, 1, 17, Arg.Any<CancellationToken>()).Returns(Range(lemma2: "Jesu"));

        return new AgentScoreService(_runs, _scores, _data, _references, TimeProvider.System);
    }

    private static ReferenceSet Set() => new() { Version = "v1", Verses = [Reference()] };

    private static string PackageMap() => new AgentPackageMap { BookId = Book, Verses = new Dictionary<string, AgentVerseMap> { ["v1"] = new() { Chapter = 1, Verse = 17 } } }.ToJson();

    [Fact]
    public async Task ScoreRun_AppliesTheProposal_ScoresItAndStoresTheJson()
    {
        _references.Load(null).Returns(Set());
        _scores.GetProposalsAsync(7, Arg.Any<CancellationToken>()).Returns([new ScoredProposal
        {
            Id = 5, BibleBookId = Book, Chapter = 1, Verse = 17, Status = "open",
            LinksJson = JsonSerializer.Serialize(Array.Empty<VerseMappingLink>(), Web),
            LemmasJson = JsonSerializer.Serialize(new[] { new AgentLemmaChange { ElbWordId = 102, Lemma = "Jesus" } }, Web),
            FlagsJson = JsonSerializer.Serialize(new[] { new AgentFlag("lemma-unknown", "m") }, Web),
        }]);
        _scores.GetPackagesAsync(7, Arg.Any<CancellationToken>()).Returns([new ScorePackageRow { LocalIdMap = PackageMap(), InputTokens = 900, OutputTokens = 90, CostUsd = 0.03m }]);

        var result = await Service().ScoreRunAsync(7, null);

        result.Outcome.ShouldBe(AgentScoreOutcome.Ok);
        var score = result.Score!;
        score.Lemmas.Accuracy.ShouldBe(1.0); // the stored "Jesu" was corrected by the proposal
        score.Verses.WithProposal.ShouldBe(1);
        score.Flags.ByCode["lemma-unknown"].ShouldBe(1);
        score.Cost.InputTokens.ShouldBe(900);
        score.Model.ShouldBe("gemma4");
        score.BulkMerge.Meets.ShouldBeTrue();
        await _scores.Received(1).SaveScoreAsync(7, Arg.Is<string>(json => json.Contains("\"bulkMerge\"") && json.Contains("\"referenceVersion\":\"v1\"")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetStoredScore_ReturnsTheStoredJson_WithoutRecomputing()
    {
        _scores.GetStoredScoreAsync(7, Arg.Any<CancellationToken>()).Returns("{\"runId\":7,\"model\":\"gemma4\",\"computedAt\":\"2026-11-17T10:00:00+00:00\"}");

        var result = await Service().GetStoredScoreAsync(7);

        result.Outcome.ShouldBe(AgentScoreOutcome.Ok);
        result.Score!.Model.ShouldBe("gemma4");
        result.Score.ComputedAt.ShouldBe(new DateTimeOffset(2026, 11, 17, 10, 0, 0, TimeSpan.Zero));
        await _scores.DidNotReceive().GetProposalsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetStoredScore_WithoutStoredScore_IsNotFound()
    {
        _scores.GetStoredScoreAsync(7, Arg.Any<CancellationToken>()).Returns((string?)null);

        (await Service().GetStoredScoreAsync(7)).Outcome.ShouldBe(AgentScoreOutcome.RunNotFound);
    }

    [Fact]
    public async Task ScoreRun_WithoutProposalOutsidePackages_KeepsTheStoredState()
    {
        _references.Load(null).Returns(Set());
        _scores.GetProposalsAsync(7, Arg.Any<CancellationToken>()).Returns([]);
        _scores.GetPackagesAsync(7, Arg.Any<CancellationToken>()).Returns([]);

        var score = (await Service().ScoreRunAsync(7, null)).Score!;

        score.Verses.Silent.ShouldBe(1);
        score.Lemmas.Correct.ShouldBe(3); // stored "Jesu" is still wrong
        score.BulkMerge.Meets.ShouldBeFalse();
    }

    [Fact]
    public async Task ScoreRun_VerseOfAnotherBook_IsNotScored()
    {
        var set = Set();
        set.Verses.Add(new ReferenceVerse { Book = 8, Chapter = 2, Verse = 3, Language = "hebrew", Words = [new ReferenceWord { Pos = 1, Text = "x", Lemma = "x" }] });
        _references.Load(null).Returns(set);
        _scores.GetProposalsAsync(7, Arg.Any<CancellationToken>()).Returns([]);
        _scores.GetPackagesAsync(7, Arg.Any<CancellationToken>()).Returns([]);

        var score = (await Service().ScoreRunAsync(7, null)).Score!;

        score.Verses.Reference.ShouldBe(2);
        score.Verses.Scored.ShouldBe(1);
        score.Verses.NotInRun.ShouldBe(1);
    }

    [Fact]
    public async Task ScoreRun_PackageVerseWithoutProposal_IsAHardRejection()
    {
        _references.Load(null).Returns(Set());
        _scores.GetProposalsAsync(7, Arg.Any<CancellationToken>()).Returns([]);
        _scores.GetPackagesAsync(7, Arg.Any<CancellationToken>()).Returns([new ScorePackageRow { LocalIdMap = PackageMap() }]);

        var score = (await Service().ScoreRunAsync(7, null)).Score!;

        score.Verses.HardRejections.ShouldBe(1);
        score.BulkMerge.Meets.ShouldBeFalse();
    }

    [Fact]
    public async Task ScoreRun_Failures_AreReportedWithoutStoring()
    {
        var service = Service();
        _runs.GetRunAsync(8, Arg.Any<CancellationToken>()).Returns((AgentRunRecord?)null);
        (await service.ScoreRunAsync(8, null)).Outcome.ShouldBe(AgentScoreOutcome.RunNotFound);

        _references.Load(null).Returns((ReferenceSet?)null);
        (await service.ScoreRunAsync(7, null)).Outcome.ShouldBe(AgentScoreOutcome.NoReference);

        var withExample = Set();
        withExample.Verses.Add(new ReferenceVerse { Book = 41, Chapter = 1, Verse = 35, Language = "greek", Words = [new ReferenceWord { Pos = 1, Text = "x" }] });
        _references.Load("v2").Returns(withExample);
        var invalid = await service.ScoreRunAsync(7, "v2");
        invalid.Outcome.ShouldBe(AgentScoreOutcome.InvalidReference);
        invalid.Errors.ShouldContain(e => e.Contains("worked example"));

        await _scores.DidNotReceive().SaveScoreAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
