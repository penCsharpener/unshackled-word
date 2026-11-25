using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Features.VerseSimilarity;
using UnshackledWord.Domain.Models.Dbo.VerseSimilarity;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.ExportSeed;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.SeedAdmin;
using StartEndpoint = UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VerseSimilarity.StartRebuild.Endpoint;
using StartValidator = UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VerseSimilarity.StartRebuild.Validator;
using StatusEndpoint = UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VerseSimilarity.GetStatus.Endpoint;

namespace UnshackledWord.Tooling.WebApi.Tests;

/// <summary>Roadmap 0027.5: maintainer endpoints of the verse similarity rebuild (parameters, 409, StartedBy, status).</summary>
public class VerseSimilarityMaintainerTests
{
    private readonly IVerseSimilarityRepository _repository = Substitute.For<IVerseSimilarityRepository>();
    private readonly IVerseTokenRepository _tokens = Substitute.For<IVerseTokenRepository>();
    private readonly SeedJobRunner _runner = new(new SeedExportCoordinator(), TimeProvider.System, NullLogger<SeedJobRunner>.Instance);

    private VerseSimilarityRebuildService Service() =>
        new(_tokens, _repository, Options.Create(new VerseSimilarityOptions()), NullLogger<VerseSimilarityRebuildService>.Instance);

    private IServiceScopeFactory ScopeFactory()
    {
        var services = new ServiceCollection().AddSingleton(Service()).BuildServiceProvider();

        return services.GetRequiredService<IServiceScopeFactory>();
    }

    private StartEndpoint StartEndpointFor(int userId)
    {
        var endpoint = Factory.Create<StartEndpoint>(ctx =>
        {
            ctx.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test"));
        }, _runner, ScopeFactory(), _repository, Service());

        return endpoint;
    }

    [Theory]
    [InlineData(null, null, null, null, true)]
    [InlineData(0.05, 10, 0.2, 0.8, true)]
    [InlineData(0.0, null, null, null, false)]
    [InlineData(1.5, null, null, null, false)]
    [InlineData(null, 0, null, null, false)]
    [InlineData(null, 1001, null, null, false)]
    [InlineData(null, null, -0.1, null, false)]
    [InlineData(null, null, 100.0, null, false)]
    [InlineData(null, null, null, -1.0, false)]
    [InlineData(null, null, null, 100.0, false)]
    public void Validator_ChecksTheServiceBounds(double? share, int? topK, double? minScore, double? phrase, bool valid)
    {
        var request = new StartVerseSimilarityRebuildRequest { StopDfShare = share, TopK = topK, MinScore = minScore, PhraseWeight = phrase };

        new StartValidator().Validate(request).IsValid.ShouldBe(valid);
        // The validator and the service agree on what fits the run row.
        var defaults = new VerseSimilarityOptions().ToParameters();
        var parameters = new VerseSimilarityParameters(share ?? defaults.StopDfShare, topK ?? defaults.TopK, minScore ?? defaults.MinScore, phrase ?? defaults.PhraseWeight);
        (VerseSimilarityRebuildService.Validate(parameters) is null).ShouldBe(valid);
    }

    [Fact]
    public async Task Start_WhenLockHeldElsewhere_Answers409AndStartsNoJob()
    {
        _repository.TryAcquireRebuildLockAsync(Arg.Any<CancellationToken>()).Returns((IAsyncDisposable?)null);
        var endpoint = StartEndpointFor(7);

        await endpoint.HandleAsync(new StartVerseSimilarityRebuildRequest(), CancellationToken.None);

        endpoint.HttpContext.Response.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        _runner.Snapshot().ShouldBeNull();
    }

    [Fact]
    public async Task Start_WhileAJobRuns_Answers409()
    {
        _repository.TryAcquireRebuildLockAsync(Arg.Any<CancellationToken>()).Returns(Substitute.For<IAsyncDisposable>());
        var gate = new TaskCompletionSource();
        _runner.TryStart("other", "all", async (_, _) => { await gate.Task; return new SeedJobResult(); }).ShouldNotBeNull();
        var endpoint = StartEndpointFor(7);

        await endpoint.HandleAsync(new StartVerseSimilarityRebuildRequest(), CancellationToken.None);

        endpoint.HttpContext.Response.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        gate.SetResult();
    }

    [Fact]
    public async Task Start_Answers202AndRebuildsWithUserAndMergedParameters()
    {
        _repository.TryAcquireRebuildLockAsync(Arg.Any<CancellationToken>()).Returns(Substitute.For<IAsyncDisposable>());
        _repository.CreateRunAsync(Arg.Any<VerseSimilarityParameters>(), Arg.Any<int?>(), Arg.Any<CancellationToken>()).Returns(42);
        var endpoint = StartEndpointFor(7);

        await endpoint.HandleAsync(new StartVerseSimilarityRebuildRequest { PhraseWeight = 0.8 }, CancellationToken.None);

        endpoint.HttpContext.Response.StatusCode.ShouldBe(StatusCodes.Status202Accepted);

        for (var i = 0; i < 300 && _runner.Snapshot()?.Status == SeedJobResponse.Running; i++)
        {
            await Task.Delay(10);
        }

        await _repository.Received(1).CreateRunAsync(
            Arg.Is<VerseSimilarityParameters>(p => p.PhraseWeight == 0.8 && p.TopK == VerseSimilarityParameters.Default.TopK), 7, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Status_ListsRunsWithDefaultsAndRunningFlag()
    {
        _repository.GetRecentRunsAsync(5, Arg.Any<CancellationToken>()).Returns(
        [
            new VerseSimilarityRunListItem(new VerseSimilarityRunDbo
            {
                Id = 3, Status = VerseSimilarityRunDbo.StatusSucceeded, StartedBy = 7, StopDfShare = 0.02m, TopK = 30, MinScore = 0.1m, PhraseWeight = 0.8m,
                PairCount = 10, StopWordCount = 2, DurationMs = 1500
            }, "Maria")
        ]);
        _repository.TryAcquireRebuildLockAsync(Arg.Any<CancellationToken>()).Returns((IAsyncDisposable?)null);
        var endpoint = Factory.Create<StatusEndpoint>(_runner, _repository, Service());

        await endpoint.HandleAsync(CancellationToken.None);

        var response = endpoint.Response;
        response.Running.ShouldBeTrue();
        response.Defaults.TopK.ShouldBe(30);
        response.Runs.Single().StartedByName.ShouldBe("Maria");
        response.Runs.Single().Parameters.PhraseWeight.ShouldBe(0.8);
        response.Job.ShouldBeNull();
    }

    [Fact]
    public void Endpoints_RequireLogin()
    {
        foreach (var type in new[] { typeof(StartEndpoint), typeof(StatusEndpoint) })
        {
            var attribute = type.GetCustomAttributes(inherit: true).Select(a => a.GetType().Name);
            attribute.ShouldContain("RequiresLoginAttribute");
        }
    }
}
