using UnshackledWord.Application.Features.CrossReferences;
using UnshackledWord.Application.Features.Notes;
using UnshackledWord.Application.Features.TskCuration;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Tsk;

namespace UnshackledWord.Application.Tests.Features.TskCuration;

/// <summary>Roadmap 0024.5 without a database: request validators, drift rule, variant mapping and the service's refusals.</summary>
public class TskCurationWordLinkTests
{
    private const int Gen11 = 1_001_001;

    private static TskLinkTargetKey Key(int start, int? end = null) => new() { TargetStartHebRefId = start, TargetEndHebRefId = end };

    private static SaveTskWordLinkRequest Save(int start, int end, params TskLinkTargetKey[] keys) =>
        new() { SourceHebRefId = Gen11, Keys = [.. keys], StartPosition = start, EndPosition = end };

    [Fact]
    public void SaveValidator_AcceptsAWellFormedRequest()
    {
        new SaveTskWordLinkRequestValidator().Validate(Save(2, 2, Key(20_008_022, 20_008_024), Key(58_001_010))).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 2)]
    [InlineData(3, 2)]
    public void SaveValidator_RefusesPositionsOutOfRange(int start, int end)
    {
        new SaveTskWordLinkRequestValidator().Validate(Save(start, end, Key(58_001_010))).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validators_RefuseEmptyTooManyDuplicateOrMalformedKeys()
    {
        var save = new SaveTskWordLinkRequestValidator();
        save.Validate(Save(2, 2)).IsValid.ShouldBeFalse();
        save.Validate(Save(2, 2, Enumerable.Range(1, 101).Select(v => Key(43_001_000 + v)).ToArray())).IsValid.ShouldBeFalse();
        save.Validate(Save(2, 2, Enumerable.Range(1, 100).Select(v => Key(19_119_000 + v)).ToArray())).IsValid.ShouldBeTrue();
        save.Validate(Save(2, 2, Key(58_001_010), Key(58_001_010))).IsValid.ShouldBeFalse();
        save.Validate(Save(2, 2, Key(43_001_003, 43_001_001))).IsValid.ShouldBeFalse();
        save.Validate(Save(2, 2, Key(43_001_001, 43_002_001))).IsValid.ShouldBeFalse();
        save.Validate(Save(2, 2, Key(0))).IsValid.ShouldBeFalse();
        save.Validate(new SaveTskWordLinkRequest { SourceHebRefId = 0, Keys = [Key(58_001_010)], StartPosition = 1, EndPosition = 1 }).IsValid.ShouldBeFalse();

        var delete = new DeleteTskWordLinkRequestValidator();
        delete.Validate(new DeleteTskWordLinkRequest { SourceHebRefId = Gen11, Keys = [Key(58_001_010)] }).IsValid.ShouldBeTrue();
        delete.Validate(new DeleteTskWordLinkRequest { SourceHebRefId = Gen11, Keys = [] }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void WordLink_IsOk_WhenOnlyPunctuationOrSpacingDiffers_AndDrifted_WhenTheWordsChangeOrArePartlyGone()
    {
        TskCrossReference Row(string? current, int count) => new()
        {
            SourceHebRefId = Gen11, Scope = "", TargetStartHebRefId = 58_001_010, StartPosition = 8, EndPosition = 9, QuotedText = "die Erde.",
            CurrentQuote = current, CurrentWordCount = count,
        };

        var ok = CrossReferenceWordLink.Build(Row("die  Erde", 2)).ShouldNotBeNull();
        (ok.State, ok.StartPosition, ok.EndPosition, ok.QuotedText).ShouldBe((TskWordLinkStates.Ok, (int?)8, (int?)9, "die Erde."));

        var changed = CrossReferenceWordLink.Build(Row("die Welt.", 2)).ShouldNotBeNull();
        (changed.State, changed.StartPosition, changed.EndPosition, changed.QuotedText).ShouldBe((TskWordLinkStates.Drifted, (int?)null, (int?)null, "die Erde."));

        CrossReferenceWordLink.Build(Row("die", 1))!.State.ShouldBe(TskWordLinkStates.Drifted);
        CrossReferenceWordLink.Build(Row(null, 0))!.State.ShouldBe(TskWordLinkStates.Drifted);
        CrossReferenceWordLink.Build(new TskCrossReference { Scope = "" }).ShouldBeNull();

        CrossReferenceWordLink.IsOnWord(ok, 8).ShouldBeTrue();
        CrossReferenceWordLink.IsOnWord(ok, 10).ShouldBeFalse();
        CrossReferenceWordLink.IsOnWord(changed, 8).ShouldBeFalse();
    }

    [Fact]
    public void MapToVariant_KeepsVariantWordsWhoseBaseIsInTheRange()
    {
        var word = new TskWordResponse { StartPosition = 2, EndPosition = 3, QuotedText = "a b", State = TskWordLinkStates.Ok };
        NoteVariantWordRow W(int position, int? basePosition) => new() { HebRefId = Gen11, RevisionVerseId = 7, RevisionWordId = position, PositionInVerse = position, BasePositionInVerse = basePosition };

        CrossReferenceWordLink.MapToVariant(word, [W(1, null), W(2, 3), W(3, 2), W(4, 4)])!.Select(w => (w.PositionInVerse, w.BasePositionInVerse)).ShouldBe([(2, 3), (3, 2)]);
        CrossReferenceWordLink.MapToVariant(word, [W(1, 1)]).ShouldNotBeNull().ShouldBeEmpty();
        CrossReferenceWordLink.MapToVariant(word, null).ShouldBeNull();
        CrossReferenceWordLink.MapToVariant(new TskWordResponse { QuotedText = "a b", State = TskWordLinkStates.Drifted }, [W(2, 2)]).ShouldBeNull();
    }

    [Fact]
    public async Task Service_RefusesNonMaintainers_ForeignKeys_AndMissingPositions_WithoutWriting()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = Substitute.For<ITskCurationRepository>();
        repository.GetLinksOfSourceAsync(Gen11, Arg.Any<CancellationToken>()).Returns([new TskSourceLink(new TskLinkKey(Gen11, 58_001_010, null), true)]);
        repository.GetElbWordsAsync(Gen11, Arg.Any<CancellationToken>()).Returns(
            [new NoteWordRow { HebRefId = Gen11, PositionInVerse = 1, WordInContext = "Im" }, new NoteWordRow { HebRefId = Gen11, PositionInVerse = 2, WordInContext = "Anfang" }]);
        var service = new TskCurationService(repository);

        (await service.SaveWordLinkAsync(1, false, Save(2, 2, Key(58_001_010)), ct)).Status.ShouldBe(TskCurationStatus.Forbidden);
        (await service.SaveWordLinkAsync(1, true, Save(2, 2, Key(58_001_010), Key(45_008_028)), ct)).Status.ShouldBe(TskCurationStatus.NotFound);
        (await service.SaveWordLinkAsync(1, true, Save(2, 3, Key(58_001_010)), ct)).Status.ShouldBe(TskCurationStatus.BadRequest);
        (await service.DeleteWordLinkAsync(1, true, new DeleteTskWordLinkRequest { SourceHebRefId = Gen11, Keys = [Key(45_008_028)] }, ct))
            .Status.ShouldBe(TskCurationStatus.NotFound);

        await repository.DidNotReceiveWithAnyArgs().ApplyManyAsync(default!, default!, default, default!, default);
    }
}
