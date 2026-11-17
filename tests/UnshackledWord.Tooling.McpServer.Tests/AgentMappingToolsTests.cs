using System.Text.Json;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.ApiTokens;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.McpServer.Auth;
using UnshackledWord.Tooling.McpServer.Tools;

namespace UnshackledWord.Tooling.McpServer.Tests;

public class AgentMappingToolsTests
{
    private static readonly Guid PackageId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private readonly IAgentPackageBuilder _builder = Substitute.For<IAgentPackageBuilder>();
    private readonly IAgentProposalService _proposals = Substitute.For<IAgentProposalService>();
    private readonly IAgentStoreRepository _store = Substitute.For<IAgentStoreRepository>();
    private readonly IAgentRunRepository _runs = Substitute.For<IAgentRunRepository>();
    private readonly IApiTokenService _tokens = Substitute.For<IApiTokenService>();
    private readonly IMcpCaller _caller = Substitute.For<IMcpCaller>();
    private readonly IConfiguration _configuration = new ConfigurationBuilder().Build();

    private static ApiTokenValidation Valid(int user = 3, int token = 9) => new() { Check = ApiTokenCheck.Valid, UserId = user, TokenId = token };

    private static JsonElement Answer(string json = """{"verses":[{"v":"v1","links":[["g1","s1"]]}]}""") => JsonDocument.Parse(json).RootElement.Clone();

    private void PackageOfRun(int aiUser, int? tokenId)
    {
        _store.GetPackageAsync(PackageId, Arg.Any<CancellationToken>()).Returns(new AgentPackageRecord { Id = PackageId, RunId = 12 });
        _runs.GetRunAsync(12, Arg.Any<CancellationToken>()).Returns(new AgentRunRecord { Id = 12, AiUserId = aiUser, ApiTokenId = tokenId });
    }

    private Task<string> Submit(JsonElement? answer = null, string? packageId = null) =>
        AgentMappingTools.SubmitMappingProposal(_proposals, _store, _runs, _tokens, _caller, _configuration, packageId ?? PackageId.ToString(), answer ?? Answer());

    private static GetAgentPackageResponse OnePackage() => new()
    {
        RunId = 12,
        Mode = "redo",
        Packages = [new AgentPackageEnvelope { Package = new AgentPackageDto { PackageId = PackageId.ToString(), Lang = "greek" }, EstimatedTokens = 800 }],
    };

    // ---- get_agent_instructions -------------------------------------------------------------------------------------------------

    [Fact]
    public void Instructions_ReturnTheSamePromptTextAsTheWorker()
    {
        var prompts = Substitute.For<IAgentPromptProvider>();
        prompts.Get("greek").Returns(new AgentPrompt("greek-v1", "greek", "RULES TEXT"));

        AgentMappingTools.GetAgentInstructions(prompts, " Greek ").ShouldBe("RULES TEXT");
    }

    [Theory]
    [InlineData("latin")]
    [InlineData("")]
    public void Instructions_UnknownLanguage_Throws(string language)
    {
        Should.Throw<McpException>(() => AgentMappingTools.GetAgentInstructions(Substitute.For<IAgentPromptProvider>(), language));
    }

    [Fact]
    public void Instructions_MissingFile_ThrowsReadableError()
    {
        var prompts = Substitute.For<IAgentPromptProvider>();
        prompts.Get("hebrew").Returns(_ => throw new FileNotFoundException());

        Should.Throw<McpException>(() => AgentMappingTools.GetAgentInstructions(prompts, "hebrew")).Message.ShouldContain("hebrew");
    }

    // ---- get_mapping_package ----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Package_WithoutToken_IsADryRunPreview_ThatSaysItCannotBeSubmitted()
    {
        _caller.OptionalProposeAsync(Arg.Any<CancellationToken>()).Returns((ApiTokenValidation?)null);
        _builder.BuildAsync(Arg.Any<AgentBuildRequest>(), Arg.Any<CancellationToken>()).Returns(OnePackage());

        var text = await AgentMappingTools.GetMappingPackage(_builder, _caller, _configuration, "41", 2, 1, 5);

        using var json = JsonDocument.Parse(text);
        json.RootElement.GetProperty("submittable").GetBoolean().ShouldBeFalse();
        json.RootElement.GetProperty("packages").GetArrayLength().ShouldBe(1);
        await _builder.Received(1).BuildAsync(Arg.Is<AgentBuildRequest>(r => r.DryRun && r.ApiTokenId == null && r.Range.BibleBookId == 41 && r.Range.FromVerse == 1 && r.Range.ToVerse == 5), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Package_WithToken_IsStoredAndBoundToTheTokensUser()
    {
        _caller.OptionalProposeAsync(Arg.Any<CancellationToken>()).Returns(Valid(user: 3, token: 9));
        _builder.BuildAsync(Arg.Any<AgentBuildRequest>(), Arg.Any<CancellationToken>()).Returns(OnePackage());

        var text = await AgentMappingTools.GetMappingPackage(_builder, _caller, _configuration, "Mk", 2, 1, 5, "fill-open");

        JsonDocument.Parse(text).RootElement.GetProperty("submittable").GetBoolean().ShouldBeTrue();
        await _builder.Received(1).BuildAsync(Arg.Is<AgentBuildRequest>(r => !r.DryRun && r.UserId == 3 && r.ApiTokenId == 9 && r.Mode == AgentMode.FillOpen && r.Range.BibleBookId == 41), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Package_BadToken_IsAnAuthError_NotASilentPreview()
    {
        _caller.OptionalProposeAsync(Arg.Any<CancellationToken>()).Returns(new ApiTokenValidation { Check = ApiTokenCheck.Revoked });

        var e = await Should.ThrowAsync<McpException>(() => AgentMappingTools.GetMappingPackage(_builder, _caller, _configuration, "41", 2, 1, 5));

        e.Message.ShouldStartWith("auth error");
        await _builder.DidNotReceiveWithAnyArgs().BuildAsync(default!, default);
    }

    [Fact]
    public async Task Package_TooManyVerses_IsRefused_ByTheConfiguredLimit()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["AgentMapping:McpMaxVersesPerPackageRequest"] = "10" }).Build();

        await Should.ThrowAsync<McpException>(() => AgentMappingTools.GetMappingPackage(_builder, _caller, configuration, "41", 2, 1, 11));
        await _builder.DidNotReceiveWithAnyArgs().BuildAsync(default!, default);

        _caller.OptionalProposeAsync(Arg.Any<CancellationToken>()).Returns((ApiTokenValidation?)null);
        _builder.BuildAsync(Arg.Any<AgentBuildRequest>(), Arg.Any<CancellationToken>()).Returns(OnePackage());
        (await AgentMappingTools.GetMappingPackage(_builder, _caller, configuration, "41", 2, 1, 10)).ShouldNotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("0", 1, 1, 1)]
    [InlineData("67", 1, 1, 1)]
    [InlineData("41", 0, 1, 1)]
    [InlineData("41", 1, 0, 1)]
    [InlineData("41", 1, 5, 4)]
    [InlineData("NoSuchBook", 1, 1, 1)]
    public async Task Package_InvalidRange_Throws(string book, int chapter, int from, int to)
    {
        await Should.ThrowAsync<McpException>(() => AgentMappingTools.GetMappingPackage(_builder, _caller, _configuration, book, chapter, from, to));
    }

    [Fact]
    public async Task Package_UnknownMode_Throws_AndEmptyRangeThrows()
    {
        await Should.ThrowAsync<McpException>(() => AgentMappingTools.GetMappingPackage(_builder, _caller, _configuration, "41", 1, 1, 1, "everything"));

        _caller.OptionalProposeAsync(Arg.Any<CancellationToken>()).Returns((ApiTokenValidation?)null);
        _builder.BuildAsync(Arg.Any<AgentBuildRequest>(), Arg.Any<CancellationToken>()).Returns((GetAgentPackageResponse?)null);
        await Should.ThrowAsync<McpException>(() => AgentMappingTools.GetMappingPackage(_builder, _caller, _configuration, "41", 99, 1, 1));
    }

    // ---- submit_mapping_proposal ------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(ApiTokenCheck.Missing)]
    [InlineData(ApiTokenCheck.Malformed)]
    [InlineData(ApiTokenCheck.Unknown)]
    [InlineData(ApiTokenCheck.Revoked)]
    [InlineData(ApiTokenCheck.Expired)]
    [InlineData(ApiTokenCheck.UserNotAllowed)]
    public async Task Submit_WithoutValidToken_IsAnAuthError_AndNothingIsSubmitted(ApiTokenCheck check)
    {
        _caller.RequireProposeAsync(Arg.Any<CancellationToken>()).Returns(new ApiTokenValidation { Check = check });

        var e = await Should.ThrowAsync<McpException>(() => Submit());

        e.Message.ShouldStartWith("auth error");
        await _proposals.DidNotReceiveWithAnyArgs().SubmitAsync(default!, default, default);
    }

    [Fact]
    public async Task Submit_PackageBuiltByAnotherToken_Throws()
    {
        _caller.RequireProposeAsync(Arg.Any<CancellationToken>()).Returns(Valid(user: 3, token: 9));
        _tokens.CountProposalsTodayAsync(9, Arg.Any<CancellationToken>()).Returns(0);

        PackageOfRun(aiUser: 4, tokenId: 9);
        await Should.ThrowAsync<McpException>(() => Submit());

        PackageOfRun(aiUser: 3, tokenId: 10);
        await Should.ThrowAsync<McpException>(() => Submit());

        PackageOfRun(aiUser: 3, tokenId: null);
        await Should.ThrowAsync<McpException>(() => Submit());

        await _proposals.DidNotReceiveWithAnyArgs().SubmitAsync(default!, default, default);
    }

    [Fact]
    public async Task Submit_OwnPackage_ReturnsTheValidatorAnswer()
    {
        _caller.RequireProposeAsync(Arg.Any<CancellationToken>()).Returns(Valid(user: 3, token: 9));
        _tokens.CountProposalsTodayAsync(9, Arg.Any<CancellationToken>()).Returns(5);
        PackageOfRun(aiUser: 3, tokenId: 9);
        _proposals.SubmitAsync(PackageId.ToString(), Arg.Any<JsonElement>(), Arg.Any<CancellationToken>()).Returns(new AgentSubmitResult
        {
            Outcome = AgentSubmitOutcome.Ok,
            Response = new SubmitAgentProposalResponse
            {
                PackageId = PackageId.ToString(),
                Rejected = 1,
                Verses = [new AgentVerseResultDto { V = "v1", Status = AgentVerseStatus.Rejected, Messages = ["v1 g7: source s12 belongs to v2"] }],
            },
        });

        var text = await Submit();

        text.ShouldContain("v1 g7: source s12 belongs to v2");
        text.ShouldContain("\"rejected\"");
    }

    [Fact]
    public async Task Submit_DailyCapReached_IsRefused_BeforeValidation()
    {
        _caller.RequireProposeAsync(Arg.Any<CancellationToken>()).Returns(Valid(token: 9));
        _tokens.CountProposalsTodayAsync(9, Arg.Any<CancellationToken>()).Returns(300);
        PackageOfRun(aiUser: 3, tokenId: 9);

        var e = await Should.ThrowAsync<McpException>(() => Submit());

        e.Message.ShouldContain("Daily limit");
        await _proposals.DidNotReceiveWithAnyArgs().SubmitAsync(default!, default, default);
    }

    [Fact]
    public async Task Submit_CapComesFromConfiguration()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["AgentMapping:McpMaxProposalsPerTokenPerDay"] = "2" }).Build();
        _caller.RequireProposeAsync(Arg.Any<CancellationToken>()).Returns(Valid(token: 9));
        _tokens.CountProposalsTodayAsync(9, Arg.Any<CancellationToken>()).Returns(2);
        PackageOfRun(aiUser: 3, tokenId: 9);

        await Should.ThrowAsync<McpException>(() => AgentMappingTools.SubmitMappingProposal(_proposals, _store, _runs, _tokens, _caller, configuration, PackageId.ToString(), Answer()));
    }

    [Theory]
    [InlineData(AgentSubmitOutcome.NotFound)]
    [InlineData(AgentSubmitOutcome.Expired)]
    [InlineData(AgentSubmitOutcome.Stale)]
    [InlineData(AgentSubmitOutcome.Invalid)]
    public async Task Submit_UnusableSubmission_IsAToolError_WithTheMessages(AgentSubmitOutcome outcome)
    {
        _caller.RequireProposeAsync(Arg.Any<CancellationToken>()).Returns(Valid(token: 9));
        _tokens.CountProposalsTodayAsync(9, Arg.Any<CancellationToken>()).Returns(0);
        PackageOfRun(aiUser: 3, tokenId: 9);
        _proposals.SubmitAsync(Arg.Any<string>(), Arg.Any<JsonElement>(), Arg.Any<CancellationToken>()).Returns(new AgentSubmitResult { Outcome = outcome, Errors = ["details here"] });

        var e = await Should.ThrowAsync<McpException>(() => Submit());

        e.Message.ShouldContain("details here");
    }

    [Fact]
    public async Task Submit_PackageIdNotAGuid_Throws()
    {
        _caller.RequireProposeAsync(Arg.Any<CancellationToken>()).Returns(Valid());

        await Should.ThrowAsync<McpException>(() => Submit(packageId: "not-a-guid"));
    }

    // ---- lemma_candidates -------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task LemmaCandidates_ListsTrustedFirst()
    {
        var data = Substitute.For<IAgentDataRepository>();
        data.GetFormLemmasAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns(
        [
            new AgentFormLemmaRow { Form = "gingen", Lemma = "gehen", Count = 40, Trusted = true },
            new AgentFormLemmaRow { Form = "gingen", Lemma = "gingen", Count = 2, Trusted = false },
        ]);

        var text = await AgentMappingTools.LemmaCandidates(data, " Gingen, ");

        using var json = JsonDocument.Parse(text);
        json.RootElement.GetProperty("form").GetString().ShouldBe("gingen");
        json.RootElement.GetProperty("trusted")[0].GetString().ShouldBe("gehen");
        json.RootElement.GetProperty("lemmas")[0].GetProperty("lemma").GetString().ShouldBe("gehen");
        await data.Received(1).GetFormLemmasAsync(Arg.Is<IReadOnlyCollection<string>>(f => f.Single() == "gingen"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LemmaCandidates_WithVerse_AddsTheStoredLemmaOfTheForm()
    {
        var data = Substitute.For<IAgentDataRepository>();
        data.GetFormLemmasAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns([]);
        data.GetRangeAsync(41, 1, 29, 1, 29, Arg.Any<CancellationToken>()).Returns(new AgentRangeData
        {
            GermanWords = [new AgentGermanWordRow { Id = 5, Chapter = 1, Verse = 29, PositionInVerse = 4, Text = "gingen", Plain = "gingen", Lemma = "gehen", Stts = "VVFIN" }],
        });

        var text = await AgentMappingTools.LemmaCandidates(data, "gingen", "Mk 1,29");

        JsonDocument.Parse(text).RootElement.GetProperty("inVerse")[0].GetProperty("storedLemma").GetString().ShouldBe("gehen");
    }

    [Theory]
    [InlineData("")]
    [InlineData("zwei Wörter")]
    public async Task LemmaCandidates_NeedsOneWord(string form)
    {
        await Should.ThrowAsync<McpException>(() => AgentMappingTools.LemmaCandidates(Substitute.For<IAgentDataRepository>(), form));
    }
}
