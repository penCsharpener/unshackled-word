using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VocabFormRuns;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VocabRootRuns;

namespace UnshackledWord.Tooling.WebApi.Tests;

/// <summary>The vocab agent runs (form 0022.13, root 0030.4): limit optional (1..2000), batch size optional (1..200, default 25).</summary>
public class VocabAgentRunValidatorTests
{
    [Theory]
    [InlineData(null, null, true)]
    [InlineData(1, 1, true)]
    [InlineData(2000, 200, true)]
    [InlineData(0, null, false)]
    [InlineData(2001, null, false)]
    [InlineData(null, 0, false)]
    [InlineData(null, 201, false)]
    public void FormRun_LimitAndBatchSize_Validate(int? limit, int? batchSize, bool valid) =>
        new VocabFormRunRangeValidator()
            .Validate(new VocabFormRunRangeRequest { Course = "nt-greek", FromRank = 1, ToRank = 400, Limit = limit, BatchSize = batchSize, Profile = "p" })
            .IsValid.ShouldBe(valid);

    [Theory]
    [InlineData(null, null, true)]
    [InlineData(1, 1, true)]
    [InlineData(2000, 200, true)]
    [InlineData(0, null, false)]
    [InlineData(2001, null, false)]
    [InlineData(null, 0, false)]
    [InlineData(null, 201, false)]
    public void RootRun_LimitAndBatchSize_Validate(int? limit, int? batchSize, bool valid) =>
        new VocabRootRunRangeValidator()
            .Validate(new VocabRootRunRangeRequest { Course = "nt-greek", FromRank = 1, ToRank = 400, Limit = limit, BatchSize = batchSize, Profile = "p" })
            .IsValid.ShouldBe(valid);

    [Fact]
    public void Limit_Omitted_CoversUpToMaxLimit()
    {
        VocabFormRunService.EffectiveLimit(null).ShouldBe(VocabFormRunService.MaxLimit);
        VocabFormRunService.EffectiveLimit(300).ShouldBe(300);
        VocabRootRunService.EffectiveLimit(null).ShouldBe(VocabRootRunService.MaxLimit);
        VocabRootRunService.EffectiveLimit(300).ShouldBe(300);
    }

    [Theory]
    [InlineData(null, 25)]
    [InlineData(0, 25)]
    [InlineData(201, 25)]
    [InlineData(1, 1)]
    [InlineData(100, 100)]
    [InlineData(200, 200)]
    public void BatchSize_FallsBackTo25(int? stored, int effective)
    {
        VocabFormRunExecutor.EffectiveBatchSize(stored).ShouldBe(effective);
        VocabRootRunExecutor.EffectiveBatchSize(stored).ShouldBe(effective);
    }
}
