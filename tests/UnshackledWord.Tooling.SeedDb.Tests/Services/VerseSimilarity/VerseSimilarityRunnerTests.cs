using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Features.VerseSimilarity;
using UnshackledWord.Tooling.SeedDb.Services.VerseSimilarity;

namespace UnshackledWord.Tooling.SeedDb.Tests.Services.VerseSimilarity;

/// <summary>
/// Roadmap 0027.2: the SeedDb pipeline rebuilds the verse similarity only after a STEP word import or when the table is empty (same gate as the
/// Greek editions refresh), and the config section <c>VerseSimilarity</c> of SeedDb and WebApi binds to the documented defaults.
/// </summary>
public class VerseSimilarityRunnerTests
{
    private readonly IVerseSimilarityRepository _repository = Substitute.For<IVerseSimilarityRepository>();
    private readonly IVerseTokenRepository _tokens = Substitute.For<IVerseTokenRepository>();

    public VerseSimilarityRunnerTests()
    {
        _repository.TryAcquireRebuildLockAsync(Arg.Any<CancellationToken>()).Returns(Substitute.For<IAsyncDisposable>());
        _tokens.LoadAsync(Arg.Any<Testament>(), Arg.Any<CancellationToken>()).Returns(new List<VerseTokenSequence>());
    }

    private VerseSimilarityRunner NewRunner()
    {
        var rebuild = new VerseSimilarityRebuildService(_tokens, _repository, Options.Create(new VerseSimilarityOptions()),
            NullLogger<VerseSimilarityRebuildService>.Instance);
        return new VerseSimilarityRunner(new(), _repository, rebuild, NullLogger<VerseSimilarityRunner>.Instance);
    }

    [Fact]
    public async Task NoNewWords_WithPairs_IsUpToDate()
    {
        _repository.PairsMissingAsync(Arg.Any<CancellationToken>()).Returns(false);

        var result = await NewRunner().RebuildIfNeededAsync(stepWordsInserted: false, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
        await _repository.DidNotReceive().TryAcquireRebuildLockAsync(Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().CreateRunAsync(Arg.Any<VerseSimilarityParameters>(), Arg.Any<int?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InsertedWords_Rebuild_WithoutAskingForPairs()
    {
        var result = await NewRunner().RebuildIfNeededAsync(stepWordsInserted: true, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        await _repository.Received(1).TryAcquireRebuildLockAsync(Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().PairsMissingAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NoNewWords_EmptyTable_Rebuilds()
    {
        _repository.PairsMissingAsync(Arg.Any<CancellationToken>()).Returns(true);

        var result = await NewRunner().RebuildIfNeededAsync(stepWordsInserted: false, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        await _repository.Received(1).TryAcquireRebuildLockAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("UnshackledWord.Tooling.SeedDb")]
    [InlineData("UnshackledWord.Tooling.WebApi")]
    public void AppSettings_VerseSimilaritySection_BindsToTheDefaults(string project)
    {
        var root = AppContext.BaseDirectory;
        while (root is not null && !File.Exists(Path.Combine(root, "UnshackledWord.slnx")))
        {
            root = Path.GetDirectoryName(root);
        }

        root.ShouldNotBeNull();
        var configuration = new ConfigurationBuilder().AddJsonFile(Path.Combine(root, "src", project, "appsettings.json")).Build();
        var section = configuration.GetSection(VerseSimilarityOptions.SectionName);
        section.Exists().ShouldBeTrue();

        section.Get<VerseSimilarityOptions>().ShouldNotBeNull().ToParameters().ShouldBe(VerseSimilarityParameters.Default);
    }
}
