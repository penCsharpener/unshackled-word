using UnshackledWord.Application.Features.Revision;
using UnshackledWord.Domain.WebApi.Revision;

namespace UnshackledWord.Application.Tests.Features.Revision;

public class RevisionServiceTests
{
    private const int JohHebRefId = 43003016;
    private readonly IRevisionRepository _repository = Substitute.For<IRevisionRepository>();

    private readonly IRevisionCarryService _carry = Substitute.For<IRevisionCarryService>();

    private RevisionService Service() => new(_repository, _carry);

    private IRevisionRepository ExistingVerse(int level = 200, RevisionActiveRow? active = null, int lastVersion = 0)
    {
        _repository.GetBaseVerseTextAsync(JohHebRefId, Arg.Any<CancellationToken>()).Returns("Denn also hat Gott die Welt geliebt");
        _repository.GetBaseWordsAsync(JohHebRefId, Arg.Any<CancellationToken>())
            .Returns([new RevisionBaseWordRow { PositionInVerse = 1, PlainWord = "Denn" }, new RevisionBaseWordRow { PositionInVerse = 2, PlainWord = "also" }]);
        _repository.IsWritableLevelAsync(level, Arg.Any<CancellationToken>()).Returns(true);
        _repository.GetActiveAsync(JohHebRefId, level, Arg.Any<CancellationToken>()).Returns(active);
        _repository.GetLastVersionAsync(JohHebRefId, level, Arg.Any<CancellationToken>()).Returns(lastVersion);
        _repository.InsertVersionAsync(Arg.Any<NewRevisionVersion>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(77);
        _carry.CarryFromBaseAsync(43, 3, 16, Arg.Any<IReadOnlyList<RevisionBaseWordRow>>(), Arg.Any<IReadOnlyList<RevisionToken>>(), Arg.Any<CancellationToken>())
            .Returns(new RevisionCarryResult(
                [new RevisionCarryWord(1, 1, RevisionCarry.Same), new RevisionCarryWord(2, null, RevisionCarry.New), new RevisionCarryWord(3, null, RevisionCarry.New)],
                [new RevisionCarryRow(1, 501, false, null, null)],
                new RevisionCarryReport(1, 0, 2, [502])));
        return _repository;
    }

    private static SaveRevisionVerseRequest Request(string text = "Denn so hat Gott", int level = 200, int? expected = null) =>
        new() { Level = level, Text = text, ExpectedVersion = expected };

    [Fact]
    public async Task SaveTextAsync_StartsAtVersionOne_WhenLevelHasNoVariant()
    {
        ExistingVerse();

        var result = await Service().SaveTextAsync(43, 3, 16, Request(), userId: 5);

        result.Outcome.ShouldBe(SaveRevisionOutcome.Saved);
        result.Saved!.RevisionVerseId.ShouldBe(77);
        result.Saved.Level.ShouldBe(200);
        result.Saved.Version.ShouldBe(1);
        await _repository.Received(1).InsertVersionAsync(
            Arg.Is<NewRevisionVersion>(v => v.Version == 1 && v.HebRefId == JohHebRefId && v.EndActiveRevisionVerseId == null && v.CreatedByUserId == 5), 5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveTextAsync_StoresCarriedWordsAndRowsAndReportsTheCarry()
    {
        ExistingVerse();

        var result = await Service().SaveTextAsync(43, 3, 16, Request(), userId: 5);

        result.Saved!.Carry.Same.ShouldBe(1);
        result.Saved.Carry.Changed.ShouldBe(0);
        result.Saved.Carry.New.ShouldBe(2);
        result.Saved.Carry.LostOriginalWordIds.ShouldBe([502]);
        await _repository.Received(1).InsertVersionAsync(
            Arg.Is<NewRevisionVersion>(v => v.Lang == "G" && v.Words.Count == 3 && v.CarriedRows.Count == 1 && v.CarriedRows[0].StepWordId == 501), 5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveTextAsync_UsesHebrewLang_ForOldTestamentBooks()
    {
        _repository.GetBaseVerseTextAsync(1001001, Arg.Any<CancellationToken>()).Returns("Im Anfang");
        _repository.GetBaseWordsAsync(1001001, Arg.Any<CancellationToken>()).Returns([new RevisionBaseWordRow { PositionInVerse = 1, PlainWord = "Im" }]);
        _repository.IsWritableLevelAsync(200, Arg.Any<CancellationToken>()).Returns(true);
        _repository.InsertVersionAsync(Arg.Any<NewRevisionVersion>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(9);
        _carry.CarryFromBaseAsync(1, 1, 1, Arg.Any<IReadOnlyList<RevisionBaseWordRow>>(), Arg.Any<IReadOnlyList<RevisionToken>>(), Arg.Any<CancellationToken>())
            .Returns(new RevisionCarryResult([], [], new RevisionCarryReport(0, 0, 0, [])));

        await Service().SaveTextAsync(1, 1, 1, Request(), userId: 5);

        await _repository.Received(1).InsertVersionAsync(Arg.Is<NewRevisionVersion>(v => v.Lang == "H"), 5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveTextAsync_IncrementsPastEndedVersionsAndEndsTheActiveOne_WhenExpectedVersionMatches()
    {
        ExistingVerse(active: new RevisionActiveRow { Id = 40, Version = 3 }, lastVersion: 3);

        var result = await Service().SaveTextAsync(43, 3, 16, Request(expected: 3), userId: 5);

        result.Saved!.Version.ShouldBe(4);
        await _repository.Received(1).InsertVersionAsync(Arg.Is<NewRevisionVersion>(v => v.Version == 4 && v.EndActiveRevisionVerseId == 40), 5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveTextAsync_ContinuesAfterWithdrawal_WhenOnlyEndedVersionsExist()
    {
        ExistingVerse(active: null, lastVersion: 2);

        var result = await Service().SaveTextAsync(43, 3, 16, Request(), userId: 5);

        result.Saved!.Version.ShouldBe(3);
    }

    [Theory]
    [InlineData(2, 3)]
    [InlineData(null, 3)]
    public async Task SaveTextAsync_ReturnsConflict_WhenExpectedVersionIsNotTheActiveVersion(int? expected, int active)
    {
        ExistingVerse(active: new RevisionActiveRow { Id = 40, Version = active }, lastVersion: active);

        var result = await Service().SaveTextAsync(43, 3, 16, Request(expected: expected), userId: 5);

        result.Outcome.ShouldBe(SaveRevisionOutcome.Conflict);
        await _repository.DidNotReceive().InsertVersionAsync(Arg.Any<NewRevisionVersion>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveTextAsync_ReturnsConflict_WhenExpectedVersionGivenButLevelHasNoVariant()
    {
        ExistingVerse(active: null);

        var result = await Service().SaveTextAsync(43, 3, 16, Request(expected: 1), userId: 5);

        result.Outcome.ShouldBe(SaveRevisionOutcome.Conflict);
    }

    [Fact]
    public async Task SaveTextAsync_ReturnsConflict_WhenAConcurrentSaveWonTheInsert()
    {
        ExistingVerse();
        _repository.InsertVersionAsync(Arg.Any<NewRevisionVersion>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns((int?)null);

        var result = await Service().SaveTextAsync(43, 3, 16, Request(), userId: 5);

        result.Outcome.ShouldBe(SaveRevisionOutcome.Conflict);
    }

    [Fact]
    public async Task SaveTextAsync_ReturnsNotFound_WhenTheVerseDoesNotExist()
    {
        _repository.GetBaseVerseTextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns((string?)null);

        var result = await Service().SaveTextAsync(43, 3, 99, Request(), userId: 5);

        result.Outcome.ShouldBe(SaveRevisionOutcome.NotFound);
    }

    [Fact]
    public async Task SaveTextAsync_ReturnsInvalid_WhenTheLevelIsNotAWritablePreset()
    {
        _repository.GetBaseVerseTextAsync(JohHebRefId, Arg.Any<CancellationToken>()).Returns("text");
        _repository.IsWritableLevelAsync(150, Arg.Any<CancellationToken>()).Returns(false);

        var result = await Service().SaveTextAsync(43, 3, 16, Request(level: 150), userId: 5);

        result.Outcome.ShouldBe(SaveRevisionOutcome.Invalid);
    }

    [Fact]
    public async Task SaveTextAsync_StoresTokenisedTextAndTheBaseHash()
    {
        ExistingVerse();
        NewRevisionVersion? stored = null;
        _repository.InsertVersionAsync(Arg.Do<NewRevisionVersion>(v => stored = v), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(77);

        await Service().SaveTextAsync(43, 3, 16, new SaveRevisionVerseRequest { Level = 200, Text = "  Denn   so,\nhat ", Note = " x " }, userId: 5);

        stored!.VerseText.ShouldBe("Denn so, hat");
        stored.Tokens.Select(t => t.PlainWord).ShouldBe(["Denn", "so", "hat"]);
        stored.BaseTextHash.Length.ShouldBe(12);
        stored.Note.ShouldBe("x");
    }

    [Fact]
    public async Task GetVerseAsync_ReturnsBaseTextAndVariantsOrderedByLevel()
    {
        ExistingVerse();
        _repository.GetActiveVariantsAsync(JohHebRefId, Arg.Any<CancellationToken>()).Returns(
        [
            new RevisionVariantRow { RevisionVerseId = 2, Level = 600, Version = 1, VerseText = "b" },
            new RevisionVariantRow { RevisionVerseId = 1, Level = 200, Version = 4, VerseText = "a", CreatedBy = "Maintainer" },
        ]);

        var response = await Service().GetVerseAsync(43, 3, 16);

        response!.BaseText.ShouldBe("Denn also hat Gott die Welt geliebt");
        response.Variants.Select(v => (v.Level, v.Version, v.Text)).ShouldBe([(200, 4, "a"), (600, 1, "b")]);
        response.Variants[0].CreatedBy.ShouldBe("Maintainer");
    }

    [Fact]
    public async Task GetVerseAsync_ReturnsNull_WhenTheVerseDoesNotExist()
    {
        _repository.GetBaseVerseTextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns((string?)null);

        (await Service().GetVerseAsync(43, 3, 99)).ShouldBeNull();
    }

    [Fact]
    public async Task SaveTextAsync_CarriesFromTheBasedOnVariant_WhenBasedOnLevelIsNotTheBase()
    {
        ExistingVerse();
        _repository.GetActiveAsync(JohHebRefId, 200, Arg.Any<CancellationToken>()).Returns(new RevisionActiveRow { Id = 40, Version = 2 });
        _repository.IsWritableLevelAsync(600, Arg.Any<CancellationToken>()).Returns(true);
        _repository.GetActiveAsync(JohHebRefId, 600, Arg.Any<CancellationToken>()).Returns((RevisionActiveRow?)null);
        _repository.GetLastVersionAsync(JohHebRefId, 600, Arg.Any<CancellationToken>()).Returns(0);
        _carry.CarryFromVariantAsync(40, Arg.Any<IReadOnlyList<RevisionToken>>(), Arg.Any<CancellationToken>())
            .Returns(new RevisionCarryResult([new RevisionCarryWord(1, 1, RevisionCarry.Same)], [new RevisionCarryRow(1, 777, false, null, null)], new RevisionCarryReport(1, 0, 0, [])));

        var result = await Service().SaveTextAsync(43, 3, 16, new SaveRevisionVerseRequest { Level = 600, Text = "Denn", BasedOnLevel = 200 }, userId: 5);

        result.Outcome.ShouldBe(SaveRevisionOutcome.Saved);
        await _repository.Received(1).InsertVersionAsync(
            Arg.Is<NewRevisionVersion>(v => v.BasedOnRevisionVerseId == 40 && v.Level == 600 && v.CarriedRows[0].StepWordId == 777), 5, Arg.Any<CancellationToken>());
        await _carry.DidNotReceive().CarryFromBaseAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<IReadOnlyList<RevisionBaseWordRow>>(), Arg.Any<IReadOnlyList<RevisionToken>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveTextAsync_StoresNoParent_WhenBasedOnLevelIsTheBase()
    {
        ExistingVerse();

        await Service().SaveTextAsync(43, 3, 16, Request(), userId: 5);

        await _repository.Received(1).InsertVersionAsync(Arg.Is<NewRevisionVersion>(v => v.BasedOnRevisionVerseId == null), 5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveTextAsync_ReturnsInvalid_WhenTheBasedOnLevelHasNoVariant()
    {
        ExistingVerse();
        _repository.GetActiveAsync(JohHebRefId, 400, Arg.Any<CancellationToken>()).Returns((RevisionActiveRow?)null);

        var result = await Service().SaveTextAsync(43, 3, 16, new SaveRevisionVerseRequest { Level = 200, Text = "Denn", BasedOnLevel = 400 }, userId: 5);

        result.Outcome.ShouldBe(SaveRevisionOutcome.Invalid);
        await _repository.DidNotReceive().InsertVersionAsync(Arg.Any<NewRevisionVersion>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsAllVersionsNewestFirst()
    {
        ExistingVerse();
        _repository.GetHistoryAsync(JohHebRefId, 200, Arg.Any<CancellationToken>()).Returns(
        [
            new RevisionHistoryRow { RevisionVerseId = 1, Version = 1, VerseText = "a", EndedAt = new DateTime(2026, 1, 2), CreatedBy = "M" },
            new RevisionHistoryRow { RevisionVerseId = 3, Version = 3, VerseText = "c", IsActive = true },
            new RevisionHistoryRow { RevisionVerseId = 2, Version = 2, VerseText = "b", Note = "n", EndedAt = new DateTime(2026, 1, 3) },
        ]);

        var history = await Service().GetHistoryAsync(43, 3, 16, 200);

        history!.Versions.Select(v => (v.Version, v.Text, v.IsActive)).ShouldBe([(3, "c", true), (2, "b", false), (1, "a", false)]);
        history.Versions[2].CreatedBy.ShouldBe("M");
        history.Versions[1].Note.ShouldBe("n");
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsNull_WhenTheVerseDoesNotExist()
    {
        _repository.GetBaseVerseTextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns((string?)null);

        (await Service().GetHistoryAsync(43, 3, 99, 200)).ShouldBeNull();
    }

    [Fact]
    public async Task WithdrawAsync_EndsTheActiveVariant()
    {
        _repository.GetHeaderAsync(40, Arg.Any<CancellationToken>()).Returns(new RevisionVerseHeaderRow { Id = 40, IsActive = true });
        _repository.EndVariantAsync(40, 5, Arg.Any<CancellationToken>()).Returns(true);

        (await Service().WithdrawAsync(40, 5)).ShouldBe(WithdrawRevisionOutcome.Withdrawn);
    }

    [Fact]
    public async Task WithdrawAsync_ReturnsConflict_WhenTheVersionIsAlreadyEnded()
    {
        _repository.GetHeaderAsync(40, Arg.Any<CancellationToken>()).Returns(new RevisionVerseHeaderRow { Id = 40, IsActive = false });

        (await Service().WithdrawAsync(40, 5)).ShouldBe(WithdrawRevisionOutcome.Conflict);
        await _repository.DidNotReceive().EndVariantAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WithdrawAsync_ReturnsConflict_WhenAConcurrentSaveEndedItFirst()
    {
        _repository.GetHeaderAsync(40, Arg.Any<CancellationToken>()).Returns(new RevisionVerseHeaderRow { Id = 40, IsActive = true });
        _repository.EndVariantAsync(40, 5, Arg.Any<CancellationToken>()).Returns(false);

        (await Service().WithdrawAsync(40, 5)).ShouldBe(WithdrawRevisionOutcome.Conflict);
    }

    [Fact]
    public async Task WithdrawAsync_ReturnsNotFound_WhenTheIdDoesNotExist()
    {
        _repository.GetHeaderAsync(40, Arg.Any<CancellationToken>()).Returns((RevisionVerseHeaderRow?)null);

        (await Service().WithdrawAsync(40, 5)).ShouldBe(WithdrawRevisionOutcome.NotFound);
    }
}
