using UnshackledWord.Domain.Models.Dbo.Revision;
using UnshackledWord.Domain.WebApi.Revision;
using UnshackledWord.Tooling.WebApi.Endpoints.Revision;
using UnshackledWord.Tooling.WebApi.Endpoints.Revision.SaveLevel;

namespace UnshackledWord.Tooling.WebApi.Tests;

public class RevisionLevelServiceTests
{
    private readonly IRevisionLevelRepository _levels = Substitute.For<IRevisionLevelRepository>();

    public RevisionLevelServiceTests()
    {
        _levels.GetAllAsync().Returns(
        [
            new RevisionLevelDbo { Id = 1, Level = 200, Code = "elb1871", NameDe = "E", NameEn = "E", IsBase = true },
            new RevisionLevelDbo { Id = 2, Level = 200, Code = "revision", NameDe = "R", NameEn = "R", IsDefault = true },
            new RevisionLevelDbo { Id = 3, Level = 400, Code = "modern", NameDe = "M", NameEn = "M" },
        ]);
        _levels.GetVariantCountsAsync().Returns(new Dictionary<int, int> { [200] = 3 });
    }

    private RevisionLevelService Service() => new(_levels);

    private static SaveRevisionLevelRequest Request(int level = 600, string code = "einfach", bool isDefault = false, int current = 0) =>
        new() { CurrentLevel = current, Level = level, Code = code, NameDe = "Einfach", NameEn = "Simple", IsDefault = isDefault };

    [Fact]
    public async Task CreateAsync_InsertsThePreset_WhenLevelAndCodeAreFree()
    {
        var result = await Service().CreateAsync(Request());

        result.Outcome.ShouldBe(SaveRevisionLevelOutcome.Saved);
        await _levels.Received(1).InsertAsync(Arg.Is<RevisionLevelDbo>(l => l.Level == 600 && l.Code == "einfach" && !l.IsBase && !l.IsDefault));
    }

    [Theory]
    [InlineData(200, "neu")]
    [InlineData(600, "revision")]
    public async Task CreateAsync_ReturnsConflict_WhenLevelOrCodeExists(int level, string code)
    {
        var result = await Service().CreateAsync(Request(level, code));

        result.Outcome.ShouldBe(SaveRevisionLevelOutcome.Conflict);
        await _levels.DidNotReceiveWithAnyArgs().InsertAsync(default!);
    }

    [Fact]
    public async Task CreateAsync_RejectsASecondDefault()
    {
        var result = await Service().CreateAsync(Request(isDefault: true));

        result.Outcome.ShouldBe(SaveRevisionLevelOutcome.Invalid);
        await _levels.DidNotReceiveWithAnyArgs().InsertAsync(default!);
    }

    [Fact]
    public async Task UpdateAsync_RenamesAUsedPreset_WhenTheLevelValueStays()
    {
        var result = await Service().UpdateAsync(Request(200, "revision", isDefault: true, current: 200));

        result.Outcome.ShouldBe(SaveRevisionLevelOutcome.Saved);
        result.Level!.VariantCount.ShouldBe(3);
        await _levels.Received(1).UpdateAsync(200, Arg.Is<RevisionLevelDbo>(l => l.NameDe == "Einfach" && l.IsDefault));
    }

    [Fact]
    public async Task UpdateAsync_AllowsChangingTheLevelValue_EvenWhenVariantsUseIt()
    {
        var result = await Service().UpdateAsync(Request(250, "revision", isDefault: true, current: 200));

        result.Outcome.ShouldBe(SaveRevisionLevelOutcome.Saved);
        await _levels.Received(1).UpdateAsync(200, Arg.Is<RevisionLevelDbo>(l => l.Level == 250));
    }

    [Fact]
    public async Task UpdateAsync_MovesAnUnusedPreset_ToANewLevelValue()
    {
        var result = await Service().UpdateAsync(Request(500, "modern", current: 400));

        result.Outcome.ShouldBe(SaveRevisionLevelOutcome.Saved);
        await _levels.Received(1).UpdateAsync(400, Arg.Is<RevisionLevelDbo>(l => l.Level == 500));
    }

    [Fact]
    public async Task UpdateAsync_ReturnsConflict_WhenTheNewLevelOrCodeBelongsToAnotherPreset()
    {
        (await Service().UpdateAsync(Request(200, "modern", current: 400))).Outcome.ShouldBe(SaveRevisionLevelOutcome.Conflict);
        (await Service().UpdateAsync(Request(400, "revision", current: 400))).Outcome.ShouldBe(SaveRevisionLevelOutcome.Conflict);
    }

    [Fact]
    public async Task UpdateAsync_NeverTargetsTheBase_ItsCodeStaysTaken()
    {
        // Level 200 is shared: "current: 200" is the revision with variants, never the Elb1871 base.
        (await Service().UpdateAsync(Request(200, "elb1871", isDefault: true, current: 200))).Outcome.ShouldBe(SaveRevisionLevelOutcome.Conflict);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNotFound_WhenThePresetDoesNotExist()
    {
        (await Service().UpdateAsync(Request(current: 999))).Outcome.ShouldBe(SaveRevisionLevelOutcome.NotFound);
    }

    [Fact]
    public async Task UpdateAsync_RejectsUnsettingTheOnlyDefault()
    {
        var result = await Service().UpdateAsync(Request(200, "revision", isDefault: false, current: 200));

        result.Outcome.ShouldBe(SaveRevisionLevelOutcome.Invalid);
    }

    [Fact]
    public async Task UpdateAsync_MovesTheDefaultToAnotherPreset()
    {
        var result = await Service().UpdateAsync(Request(400, "modern", isDefault: true, current: 400));

        result.Outcome.ShouldBe(SaveRevisionLevelOutcome.Saved);
        await _levels.Received(1).UpdateAsync(400, Arg.Is<RevisionLevelDbo>(l => l.IsDefault));
    }

    [Theory]
    [InlineData(100, "ok", true)]
    [InlineData(0, "ok", false)]
    [InlineData(1001, "ok", false)]
    [InlineData(600, "Not Ok", false)]
    [InlineData(600, "", false)]
    [InlineData(600, "ok-1", true)]
    public void Validator_ChecksLevelRangeAndCodeShape(int level, string code, bool valid)
    {
        var result = new Validator().Validate(Request(level, code));

        result.IsValid.ShouldBe(valid);
    }
}
