using UnshackledWord.Application.Features.Revision;
using UnshackledWord.Domain.WebApi.Revision;

namespace UnshackledWord.Application.Tests.Features.Revision;

public class RevisionServiceTests
{
    private const int JohHebRefId = 43003016;
    private readonly IRevisionRepository _repository = Substitute.For<IRevisionRepository>();

    private readonly IRevisionCarryService _carry = Substitute.For<IRevisionCarryService>();

    private RevisionService Service() => new(_repository, _carry);

    // Stands in for the repository's version rule: live version + 1 (expected version), or 1 for a verse without a live row.
    private static SavedRevisionVersion Saved(NewRevisionVersion v) => new(77, (v.ExpectedVersion ?? 0) + 1);

    private IRevisionRepository ExistingVerse(int level = 200, RevisionActiveRow? active = null)
    {
        _repository.GetBaseVerseTextAsync(JohHebRefId, Arg.Any<CancellationToken>()).Returns("Denn also hat Gott die Welt geliebt");
        _repository.GetBaseWordsAsync(JohHebRefId, Arg.Any<CancellationToken>())
            .Returns([new RevisionBaseWordRow { PositionInVerse = 1, PlainWord = "Denn" }, new RevisionBaseWordRow { PositionInVerse = 2, PlainWord = "also" }]);
        _repository.IsWritableLevelAsync(level, Arg.Any<CancellationToken>()).Returns(true);
        _repository.GetActiveAsync(JohHebRefId, level, Arg.Any<CancellationToken>()).Returns(active);
        _repository.SaveVersionAsync(Arg.Any<NewRevisionVersion>(), Arg.Any<CancellationToken>()).Returns(ci => Saved(ci.Arg<NewRevisionVersion>()));
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
        await _repository.Received(1).SaveVersionAsync(
            Arg.Is<NewRevisionVersion>(v => v.ExpectedVersion == null && v.HebRefId == JohHebRefId && v.CreatedByUserId == 5), Arg.Any<CancellationToken>());
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
        await _repository.Received(1).SaveVersionAsync(
            Arg.Is<NewRevisionVersion>(v => v.Lang == "G" && v.Words.Count == 3 && v.CarriedRows.Count == 1 && v.CarriedRows[0].StepWordId == 501), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveTextAsync_UsesHebrewLang_ForOldTestamentBooks()
    {
        _repository.GetBaseVerseTextAsync(1001001, Arg.Any<CancellationToken>()).Returns("Im Anfang");
        _repository.GetBaseWordsAsync(1001001, Arg.Any<CancellationToken>()).Returns([new RevisionBaseWordRow { PositionInVerse = 1, PlainWord = "Im" }]);
        _repository.IsWritableLevelAsync(200, Arg.Any<CancellationToken>()).Returns(true);
        _repository.SaveVersionAsync(Arg.Any<NewRevisionVersion>(), Arg.Any<CancellationToken>()).Returns(new SavedRevisionVersion(9, 1));
        _carry.CarryFromBaseAsync(1, 1, 1, Arg.Any<IReadOnlyList<RevisionBaseWordRow>>(), Arg.Any<IReadOnlyList<RevisionToken>>(), Arg.Any<CancellationToken>())
            .Returns(new RevisionCarryResult([], [], new RevisionCarryReport(0, 0, 0, [])));

        await Service().SaveTextAsync(1, 1, 1, Request(), userId: 5);

        await _repository.Received(1).SaveVersionAsync(Arg.Is<NewRevisionVersion>(v => v.Lang == "H"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveTextAsync_PassesTheExpectedVersionToTheRepository_WhichUpdatesTheLiveRowInPlace()
    {
        ExistingVerse(active: new RevisionActiveRow { Id = 40, Version = 3, RevisionId = 2 });

        var result = await Service().SaveTextAsync(43, 3, 16, Request(expected: 3), userId: 5);

        result.Saved!.Version.ShouldBe(4);
        await _repository.Received(1).SaveVersionAsync(Arg.Is<NewRevisionVersion>(v => v.ExpectedVersion == 3), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveTextAsync_ReturnsTheVersionTheRepositoryAssigned_AfterAWithdrawal()
    {
        ExistingVerse(active: null);
        _repository.SaveVersionAsync(Arg.Any<NewRevisionVersion>(), Arg.Any<CancellationToken>()).Returns(new SavedRevisionVersion(78, 3));

        var result = await Service().SaveTextAsync(43, 3, 16, Request(), userId: 5);

        result.Saved!.RevisionVerseId.ShouldBe(78);
        result.Saved.Version.ShouldBe(3);
    }

    [Theory]
    [InlineData(2, 3)]
    [InlineData(null, 3)]
    public async Task SaveTextAsync_ReturnsConflict_WhenExpectedVersionIsNotTheActiveVersion(int? expected, int active)
    {
        ExistingVerse(active: new RevisionActiveRow { Id = 40, Version = active });

        var result = await Service().SaveTextAsync(43, 3, 16, Request(expected: expected), userId: 5);

        result.Outcome.ShouldBe(SaveRevisionOutcome.Conflict);
        await _repository.DidNotReceive().SaveVersionAsync(Arg.Any<NewRevisionVersion>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveTextAsync_ReturnsConflict_WhenExpectedVersionGivenButLevelHasNoVariant()
    {
        ExistingVerse(active: null);

        var result = await Service().SaveTextAsync(43, 3, 16, Request(expected: 1), userId: 5);

        result.Outcome.ShouldBe(SaveRevisionOutcome.Conflict);
    }

    [Fact]
    public async Task SaveTextAsync_ReturnsConflict_WhenTheRepositoryRejectsTheSave()
    {
        // The repository re-checks the expected version under the row lock (stale after the pre-check) and on a concurrent first insert.
        ExistingVerse();
        _repository.SaveVersionAsync(Arg.Any<NewRevisionVersion>(), Arg.Any<CancellationToken>()).Returns((SavedRevisionVersion?)null);

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
        _repository.SaveVersionAsync(Arg.Do<NewRevisionVersion>(v => stored = v), Arg.Any<CancellationToken>()).Returns(new SavedRevisionVersion(77, 1));

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
        _repository.GetActiveAsync(JohHebRefId, 200, Arg.Any<CancellationToken>()).Returns(new RevisionActiveRow { Id = 40, Version = 2, RevisionId = 7 });
        _repository.IsWritableLevelAsync(600, Arg.Any<CancellationToken>()).Returns(true);
        _repository.GetActiveAsync(JohHebRefId, 600, Arg.Any<CancellationToken>()).Returns((RevisionActiveRow?)null);
        _carry.CarryFromVariantAsync(40, Arg.Any<IReadOnlyList<RevisionToken>>(), Arg.Any<CancellationToken>())
            .Returns(new RevisionCarryResult([new RevisionCarryWord(1, 1, RevisionCarry.Same)], [new RevisionCarryRow(1, 777, false, null, null)], new RevisionCarryReport(1, 0, 0, [])));

        var result = await Service().SaveTextAsync(43, 3, 16, new SaveRevisionVerseRequest { Level = 600, Text = "Denn", BasedOnLevel = 200 }, userId: 5);

        result.Outcome.ShouldBe(SaveRevisionOutcome.Saved);
        await _repository.Received(1).SaveVersionAsync(
            Arg.Is<NewRevisionVersion>(v => v.BasedOnRevisionId == 7 && v.BasedOnVersion == 2 && v.Level == 600 && v.CarriedRows[0].StepWordId == 777), Arg.Any<CancellationToken>());
        await _carry.DidNotReceive().CarryFromBaseAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<IReadOnlyList<RevisionBaseWordRow>>(), Arg.Any<IReadOnlyList<RevisionToken>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveTextAsync_StoresNoParent_WhenBasedOnLevelIsTheBase()
    {
        ExistingVerse();

        await Service().SaveTextAsync(43, 3, 16, Request(), userId: 5);

        await _repository.Received(1).SaveVersionAsync(Arg.Is<NewRevisionVersion>(v => v.BasedOnRevisionId == null && v.BasedOnVersion == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveTextAsync_ReturnsInvalid_WhenTheBasedOnLevelHasNoVariant()
    {
        ExistingVerse();
        _repository.GetActiveAsync(JohHebRefId, 400, Arg.Any<CancellationToken>()).Returns((RevisionActiveRow?)null);

        var result = await Service().SaveTextAsync(43, 3, 16, new SaveRevisionVerseRequest { Level = 200, Text = "Denn", BasedOnLevel = 400 }, userId: 5);

        result.Outcome.ShouldBe(SaveRevisionOutcome.Invalid);
        await _repository.DidNotReceive().SaveVersionAsync(Arg.Any<NewRevisionVersion>(), Arg.Any<CancellationToken>());
    }

    private static RevisionHistoryRow Audit(long id, int version, string action, string text = "x", string? by = null, int day = 1, string? note = null) =>
        new() { AuditId = id, Version = version, Action = action, VerseText = text, By = by, Note = note, At = new DateTime(2026, 1, day, 0, 0, 0, DateTimeKind.Utc) };

    [Fact]
    public async Task GetHistoryAsync_ReturnsAuditEntriesNewestFirst_WithActiveAndEndedAt()
    {
        ExistingVerse();
        _repository.GetHistoryAsync(JohHebRefId, 200, Arg.Any<CancellationToken>()).Returns(
        [
            Audit(4, 3, "text", "c", day: 4),
            Audit(3, 2, "mapping", "b", "A", day: 3),
            Audit(2, 2, "text", "b", note: "n", day: 2),
            Audit(1, 1, "text", "a", "M", day: 1),
        ]);

        var history = await Service().GetHistoryAsync(43, 3, 16, 200);

        history!.Versions.Select(v => (v.AuditId, v.Action, v.Version, v.IsActive)).ShouldBe(
            [(4L, "text", 3, true), (3L, "mapping", 2, false), (2L, "text", 2, false), (1L, "text", 1, false)]);
        history.Versions[0].EndedAt.ShouldBeNull();
        history.Versions[1].EndedAt.ShouldBeNull();
        history.Versions[2].EndedAt.ShouldBe(new DateTime(2026, 1, 4, 0, 0, 0, DateTimeKind.Utc));
        history.Versions[3].EndedAt.ShouldBe(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));
        history.Versions[3].By.ShouldBe("M");
        history.Versions[2].Note.ShouldBe("n");
    }

    [Fact]
    public async Task GetHistoryAsync_WithdrawEntry_EndsTheVerse_AndNothingIsActive()
    {
        ExistingVerse();
        _repository.GetHistoryAsync(JohHebRefId, 200, Arg.Any<CancellationToken>()).Returns(
        [
            Audit(3, 2, "withdraw", "b", "A", day: 3),
            Audit(2, 2, "text", "b", day: 2),
            Audit(1, 1, "text", "a", day: 1),
        ]);

        var history = await Service().GetHistoryAsync(43, 3, 16, 200);

        history!.Versions.Select(v => (v.Action, v.IsActive)).ShouldBe([("withdraw", false), ("text", false), ("text", false)]);
        history.Versions[1].EndedAt.ShouldBe(new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task GetHistoryAsync_ARecreatedVerseAfterAWithdraw_IsActiveAgain()
    {
        ExistingVerse();
        _repository.GetHistoryAsync(JohHebRefId, 200, Arg.Any<CancellationToken>()).Returns(
        [
            Audit(3, 3, "text", "c", day: 3),
            Audit(2, 2, "withdraw", "b", day: 2),
            Audit(1, 2, "text", "b", day: 1),
        ]);

        var history = await Service().GetHistoryAsync(43, 3, 16, 200);

        history!.Versions.Select(v => (v.Action, v.IsActive)).ShouldBe([("text", true), ("withdraw", false), ("text", false)]);
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsNull_WhenTheVerseDoesNotExist()
    {
        _repository.GetBaseVerseTextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns((string?)null);

        (await Service().GetHistoryAsync(43, 3, 99, 200)).ShouldBeNull();
    }

    [Fact]
    public async Task WithdrawAsync_WithdrawsTheVariant()
    {
        _repository.GetHeaderAsync(40, Arg.Any<CancellationToken>()).Returns(new RevisionVerseHeaderRow { Id = 40 });
        _repository.EndVariantAsync(40, 5, Arg.Any<CancellationToken>()).Returns(true);

        (await Service().WithdrawAsync(40, 5)).ShouldBe(WithdrawRevisionOutcome.Withdrawn);
    }

    [Fact]
    public async Task WithdrawAsync_ReturnsConflict_WhenAConcurrentWithdrawDeletedTheRowFirst()
    {
        _repository.GetHeaderAsync(40, Arg.Any<CancellationToken>()).Returns(new RevisionVerseHeaderRow { Id = 40 });
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
