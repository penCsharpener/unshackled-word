using Microsoft.Extensions.Configuration;
using UnshackledWord.Application.Features.VerseMapping;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping;

namespace UnshackledWord.Application.Tests.Features;

public class VerseMappingServiceTests
{
    private readonly IVerseMappingRepository _repository = Substitute.For<IVerseMappingRepository>();

    private VerseMappingService CreateService(bool? enabled)
    {
        var configuration = Substitute.For<IConfiguration>();
        configuration[VerseMappingService.EditingEnabledKey].Returns(enabled?.ToString());
        _repository.GetGermanWordsAsync(1, 1, 1, Arg.Any<CancellationToken>()).Returns(
        [
            new VerseGermanWordRow { Id = 1, PositionInVerse = 1, Text = "Im" },
            new VerseGermanWordRow { Id = 2, PositionInVerse = 2, Text = "Anfang" },
            new VerseGermanWordRow { Id = 3, PositionInVerse = 3, Text = "die" },
        ]);
        _repository.GetOriginalWordsAsync(true, 1, 1, 1, Arg.Any<CancellationToken>()).Returns(
        [
            new VerseOriginalWordRow { Id = 10, Position = 1, Original = "בְּ", Strongs = "H9003" },
            new VerseOriginalWordRow { Id = 11, Position = 2, Original = "רֵאשִׁית", Gloss = "beginning", Transliteration = "" },
        ]);
        return new VerseMappingService(_repository, configuration);
    }

    [Fact]
    public async Task Get_ReturnsNull_ForUnknownVerse()
    {
        var service = CreateService(false);
        _repository.GetGermanWordsAsync(1, 1, 1, Arg.Any<CancellationToken>()).Returns([]);

        (await service.GetAsync(1, 1, 1)).ShouldBeNull();
    }

    [Fact]
    public async Task Get_FiltersOverAssignedRows_AndReportsEditingFlag()
    {
        var service = CreateService(null);
        _repository.GetMappingRowsAsync(true, 1, 1, 1, Arg.Any<CancellationToken>()).Returns(
        [
            new VerseMappingRow { ElbWordId = 2, StepWordId = 11 },
            new VerseMappingRow { ElbWordId = 1, StepWordId = 10 },
            new VerseMappingRow { ElbWordId = 1, StepWordId = 10 },                       // duplicate
            new VerseMappingRow { ElbWordId = 1, StepWordId = 999 },                      // original of another verse
            new VerseMappingRow { ElbWordId = 99, StepWordId = 10 },                      // word of another verse
            new VerseMappingRow { ElbWordId = 3, StepWordId = 11, IsAddedWord = true, ParentElbWordId = 2 },   // added, still points at an original
            new VerseMappingRow { ElbWordId = 3, IsAddedWord = true, ParentElbWordId = 500 }, // parent from another verse
        ]);

        var response = (await service.GetAsync(1, 1, 1))!;

        response.Language.ShouldBe("hebrew");
        response.EditingEnabled.ShouldBeFalse();
        response.OriginalWords[1].Transliteration.ShouldBeNull();
        response.Links.Select(l => (l.ElbWordId, l.StepWordId, l.IsAddedWord, l.ParentElbWordId)).ToArray().ShouldBe(
        [
            (1, (int?)10, false, (int?)null),
            (2, (int?)11, false, (int?)null),
            (3, (int?)null, true, (int?)2),
        ]);
    }

    [Fact]
    public async Task Save_WhenDisabled_IsForbidden_AndTouchesNothing()
    {
        var service = CreateService(false);

        var result = await service.SaveAsync(1, 1, 1, [new VerseMappingLink { ElbWordId = 1, StepWordId = 10 }]);

        result.Outcome.ShouldBe(SaveVerseMappingOutcome.Forbidden);
        result.Errors.ShouldNotBeEmpty();
        await _repository.DidNotReceiveWithAnyArgs().ReplaceMappingAsync(default, default, default, default, default!, default);
    }

    [Fact]
    public async Task Save_WhenNotConfigured_IsForbidden()
    {
        var service = CreateService(null);

        (await service.SaveAsync(1, 1, 1, [])).Outcome.ShouldBe(SaveVerseMappingOutcome.Forbidden);
    }

    [Fact]
    public async Task Save_InvalidLinks_AreRejected_WithoutWriting()
    {
        var service = CreateService(true);

        var result = await service.SaveAsync(1, 1, 1, [new VerseMappingLink { ElbWordId = 1, StepWordId = 999 }]);

        result.Outcome.ShouldBe(SaveVerseMappingOutcome.Invalid);
        result.Errors.ShouldContain(e => e.Contains("stepWordId"));
        await _repository.DidNotReceiveWithAnyArgs().ReplaceMappingAsync(default, default, default, default, default!, default);
    }

    [Fact]
    public async Task Save_UnknownVerse_IsNotFound()
    {
        var service = CreateService(true);
        _repository.GetGermanWordsAsync(1, 1, 1, Arg.Any<CancellationToken>()).Returns([]);

        (await service.SaveAsync(1, 1, 1, [])).Outcome.ShouldBe(SaveVerseMappingOutcome.NotFound);
    }

    [Fact]
    public async Task Save_ReplacesVerseRows_AndReturnsReloadedMapping()
    {
        var service = CreateService(true);
        IReadOnlyList<VerseMappingRow>? written = null;
        await _repository.ReplaceMappingAsync(true, 1, 1, 1, Arg.Do<IReadOnlyList<VerseMappingRow>>(r => written = r), Arg.Any<CancellationToken>());

        var result = await service.SaveAsync(1, 1, 1,
        [
            new VerseMappingLink { ElbWordId = 1, StepWordId = 10 },
            new VerseMappingLink { ElbWordId = 3, IsAddedWord = true, ParentElbWordId = 2 },
        ]);

        result.Outcome.ShouldBe(SaveVerseMappingOutcome.Saved);
        result.Mapping!.EditingEnabled.ShouldBeTrue();
        written!.Count.ShouldBe(2);
        written[1].IsAddedWord.ShouldBeTrue();
        written[1].StepWordId.ShouldBeNull();
        written[1].ParentElbWordId.ShouldBe(2);
    }
}
