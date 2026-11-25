using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Features.VerseSimilarity;

namespace UnshackledWord.Application.Tests.Features.VerseSimilarity;

/// <summary>Roadmap 0027.2: lock, run row lifecycle and parameter checks of <see cref="VerseSimilarityRebuildService"/>.</summary>
public class VerseSimilarityRebuildServiceTests
{
    private readonly IVerseTokenRepository _tokens = Substitute.For<IVerseTokenRepository>();
    private readonly IVerseSimilarityRepository _repository = Substitute.For<IVerseSimilarityRepository>();

    private VerseSimilarityRebuildService NewService(VerseSimilarityOptions? options = null) =>
        new(_tokens, _repository, Options.Create(options ?? new VerseSimilarityOptions()), NullLogger<VerseSimilarityRebuildService>.Instance);

    private static VerseTokenSequence V(int id, string tokens) => new(id, tokens.Split(' '));

    private void GivenLock() =>
        _repository.TryAcquireRebuildLockAsync(Arg.Any<CancellationToken>()).Returns(Substitute.For<IAsyncDisposable>());

    [Fact]
    public void Options_Defaults_AreTheDocumentedParameters()
    {
        new VerseSimilarityOptions().ToParameters().ShouldBe(new VerseSimilarityParameters(0.02, 30, 0.1, 0.5));
        NewService().DefaultParameters.ShouldBe(VerseSimilarityParameters.Default);
    }

    [Theory]
    [InlineData(0, 30, 0.1, 0.5, "StopDfShare")]
    [InlineData(1.5, 30, 0.1, 0.5, "StopDfShare")]
    [InlineData(0.02, 0, 0.1, 0.5, "TopK")]
    [InlineData(0.02, 40000, 0.1, 0.5, "TopK")]
    [InlineData(0.02, 30, -0.1, 0.5, "MinScore")]
    [InlineData(0.02, 30, 0.1, 100, "PhraseWeight")]
    public async Task InvalidParameters_WriteNothing(double stopDfShare, int topK, double minScore, double phraseWeight, string field)
    {
        var result = await NewService().RebuildAsync(new VerseSimilarityParameters(stopDfShare, topK, minScore, phraseWeight), token: TestContext.Current.CancellationToken);

        result.Status.ShouldBe(VerseSimilarityRebuildStatus.Invalid);
        result.Error.ShouldNotBeNull().ShouldContain(field);
        await _repository.DidNotReceive().TryAcquireRebuildLockAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LockHeld_ReturnsBusy_WithoutRunRow()
    {
        _repository.TryAcquireRebuildLockAsync(Arg.Any<CancellationToken>()).Returns((IAsyncDisposable?)null);

        var result = await NewService().RebuildAsync(token: TestContext.Current.CancellationToken);

        result.Status.ShouldBe(VerseSimilarityRebuildStatus.Busy);
        result.RunId.ShouldBeNull();
        await _repository.DidNotReceive().CreateRunAsync(Arg.Any<VerseSimilarityParameters>(), Arg.Any<int?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Success_ReplacesPairs_AndReportsCounts_AndReleasesTheLock()
    {
        var rebuildLock = Substitute.For<IAsyncDisposable>();
        _repository.TryAcquireRebuildLockAsync(Arg.Any<CancellationToken>()).Returns(rebuildLock);
        _repository.CreateRunAsync(Arg.Any<VerseSimilarityParameters>(), Arg.Any<int?>(), Arg.Any<CancellationToken>()).Returns(7);
        _tokens.LoadAsync(Testament.Old, Arg.Any<CancellationToken>()).Returns([V(1_001_001, "H0001 H0002"), V(1_001_002, "H0001 H0002"), V(1_001_003, "H0003")]);
        _tokens.LoadAsync(Testament.New, Arg.Any<CancellationToken>()).Returns([V(40_001_001, "G0001"), V(40_001_002, "G0001"), V(40_001_003, "G0002")]);

        IReadOnlyCollection<VerseSimilarityHit>? written = null;
        VerseSimilarityRunSuccess? completed = null;
        _repository.ReplacePairsAsync(7, Arg.Any<IReadOnlyCollection<VerseSimilarityHit>>(), Arg.Any<Func<VerseSimilarityRunSuccess>>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                written = ci.ArgAt<IReadOnlyCollection<VerseSimilarityHit>>(1);
                completed = ci.ArgAt<Func<VerseSimilarityRunSuccess>>(2)();
                return Task.CompletedTask;
            });

        var parameters = new VerseSimilarityParameters(StopDfShare: 1, TopK: 5, MinScore: 0, PhraseWeight: 0.5);
        var result = await NewService().RebuildAsync(parameters, startedBy: 3, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(VerseSimilarityRebuildStatus.Succeeded);
        result.RunId.ShouldBe(7);
        written.ShouldNotBeNull().Select(h => (h.SourceVerseId, h.TargetVerseId)).ShouldBe(
            [(1_001_001, 1_001_002), (1_001_002, 1_001_001), (40_001_001, 40_001_002), (40_001_002, 40_001_001)], ignoreOrder: true);
        result.PairCount.ShouldBe(4);
        completed.ShouldNotBeNull().PairCount.ShouldBe(4);
        await _repository.Received(1).CreateRunAsync(parameters, 3, Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().MarkFailedAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        await rebuildLock.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task Exception_MarksTheRunFailed_AndKeepsTheOldPairs()
    {
        GivenLock();
        _repository.CreateRunAsync(Arg.Any<VerseSimilarityParameters>(), Arg.Any<int?>(), Arg.Any<CancellationToken>()).Returns(8);
        _tokens.LoadAsync(Arg.Any<Testament>(), Arg.Any<CancellationToken>()).Returns<IReadOnlyList<VerseTokenSequence>>(_ => throw new InvalidOperationException("boom"));

        var result = await NewService().RebuildAsync(token: TestContext.Current.CancellationToken);

        result.Status.ShouldBe(VerseSimilarityRebuildStatus.Failed);
        result.RunId.ShouldBe(8);
        result.Error.ShouldBe("InvalidOperationException: boom");
        await _repository.Received(1).MarkFailedAsync(8, "InvalidOperationException: boom", Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().ReplacePairsAsync(Arg.Any<int>(), Arg.Any<IReadOnlyCollection<VerseSimilarityHit>>(), Arg.Any<Func<VerseSimilarityRunSuccess>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NullParameters_UseTheConfiguredDefaults()
    {
        GivenLock();
        var options = new VerseSimilarityOptions { StopDfShare = 0.05, TopK = 10, MinScore = 0.2, PhraseWeight = 0.8 };
        _tokens.LoadAsync(Arg.Any<Testament>(), Arg.Any<CancellationToken>()).Returns(new List<VerseTokenSequence>());

        await NewService(options).RebuildAsync(token: TestContext.Current.CancellationToken);

        await _repository.Received(1).CreateRunAsync(new VerseSimilarityParameters(0.05, 10, 0.2, 0.8), null, Arg.Any<CancellationToken>());
    }
}
