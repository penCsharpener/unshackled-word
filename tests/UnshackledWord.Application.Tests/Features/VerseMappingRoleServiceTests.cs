using Microsoft.Extensions.Configuration;
using UnshackledWord.Application.Features.VerseMapping;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping;

namespace UnshackledWord.Application.Tests.Features;

/// <summary>Role, morpheme and part lemma on the save and read path of <see cref="VerseMappingService"/> (roadmap 0018.2), with Gen 1:3.</summary>
public class VerseMappingRoleServiceTests
{
    private const int UserId = 42;

    private readonly IVerseMappingRepository _repository = Substitute.For<IVerseMappingRepository>();

    private VerseMappingService CreateService()
    {
        var configuration = Substitute.For<IConfiguration>();
        configuration[VerseMappingService.EditingEnabledKey].Returns("true");
        _repository.GetGermanWordsAsync(1, 1, 3, Arg.Any<CancellationToken>()).Returns(
        [
            new VerseGermanWordRow { Id = 31, PositionInVerse = 1, Text = "Und", Stts = "KON" },
            new VerseGermanWordRow { Id = 32, PositionInVerse = 2, Text = "Gott", Stts = "NN" },
            new VerseGermanWordRow { Id = 33, PositionInVerse = 3, Text = "sprach:", Stts = "VVFIN" },
            new VerseGermanWordRow { Id = 34, PositionInVerse = 4, Text = "Es", Stts = "PPER" },
            new VerseGermanWordRow { Id = 35, PositionInVerse = 5, Text = "werde", Stts = "VAFIN" },
        ]);
        _repository.GetOriginalWordsAsync(true, 1, 1, 3, Arg.Any<CancellationToken>()).Returns(
        [
            new VerseOriginalWordRow { Id = 22, Position = 1, Original = "וַיֹּ֥אמֶר", Strongs = "H0559", MorphemeTags = "H9001=ו=&/{H0559=אָמַר=to say}" },
            new VerseOriginalWordRow { Id = 23, Position = 2, Original = "אֱלֹהִ֖ים", Strongs = "H0430G", MorphemeTags = "{H0430G=אֱלֹהִים=God}" },
            new VerseOriginalWordRow { Id = 24, Position = 3, Original = "יְהִ֣י", Strongs = "H1961_A", MorphemeTags = "{H1961=הָיָה=to be}" },
        ]);
        return new VerseMappingService(_repository, configuration);
    }

    private async Task<IReadOnlyList<VerseMappingRow>> SaveAsync(VerseMappingService service, params VerseMappingLink[] links)
    {
        IReadOnlyList<VerseMappingRow> written = [];
        await _repository.ReplaceMappingAsync(true, 1, 1, 3, Arg.Do<IReadOnlyList<VerseMappingRow>>(r => written = r), Arg.Any<CancellationToken>());

        var result = await service.SaveAsync(1, 1, 3, links, UserId);
        result.Errors.ShouldBeEmpty();
        result.Outcome.ShouldBe(SaveVerseMappingOutcome.Saved);
        return written;
    }

    [Fact]
    public async Task Save_WritesRoleAndResolvedMorpheme()
    {
        var service = CreateService();

        var written = await SaveAsync(
            service,
            new VerseMappingLink { ElbWordId = 31, StepWordId = 22, Role = MappingRoles.Head, StrongsNumber = "h9001" },
            new VerseMappingLink { ElbWordId = 33, StepWordId = 22, Role = MappingRoles.Head },
            new VerseMappingLink { ElbWordId = 34, IsAddedWord = true, ParentElbWordId = 35 },
            new VerseMappingLink { ElbWordId = 35, StepWordId = 24, Role = MappingRoles.Head });

        written.Select(r => (r.ElbWordId, r.Role, r.StrongsNumber, r.MultiHead)).ToArray().ShouldBe(
        [
            (31, "head", (string?)"H9001", false),
            (33, "head", (string?)null, false),
            (34, "head", (string?)null, false),
            (35, "head", (string?)null, false),
        ]);
    }

    [Fact]
    public async Task Save_RejectsTwoHeads_AndStoresMultiHeadOnEveryHeadWhenAccepted()
    {
        var service = CreateService();
        var und = new VerseMappingLink { ElbWordId = 31, StepWordId = 22, Role = MappingRoles.Head };
        var sprach = new VerseMappingLink { ElbWordId = 33, StepWordId = 22, Role = MappingRoles.Head };

        var rejected = await service.SaveAsync(1, 1, 3, [und, sprach], UserId);
        rejected.Outcome.ShouldBe(SaveVerseMappingOutcome.Invalid);
        rejected.Errors.ShouldContain(e => e.Contains("2 head links"));

        und.AllowMultiHead = true;
        var written = await SaveAsync(service, und, sprach);
        written.Select(r => r.MultiHead).ShouldAllBe(m => m);
    }

    [Fact]
    public async Task LegacySave_KeepsStoredRolesMorphemesAndFlags_AndSkipsTheOneHeadRule()
    {
        var service = CreateService();
        _repository.GetMappingRowsAsync(true, 1, 1, 3, Arg.Any<CancellationToken>()).Returns(
        [
            new VerseMappingRow { ElbWordId = 31, StepWordId = 22, Role = "head", StrongsNumber = "H9001" },
            new VerseMappingRow { ElbWordId = 33, StepWordId = 22, Role = "head" },
            new VerseMappingRow { ElbWordId = 32, StepWordId = 23, Role = "head", MultiHead = true },
            new VerseMappingRow { ElbWordId = 35, StepWordId = 23, Role = "head", MultiHead = true },
        ]);

        // The client does not know roles: no role field anywhere. "Gott" and "werde" stay a multi-head pair; a new link to יְהִי defaults to head.
        var written = await SaveAsync(
            service,
            new VerseMappingLink { ElbWordId = 31, StepWordId = 22 },
            new VerseMappingLink { ElbWordId = 33, StepWordId = 22 },
            new VerseMappingLink { ElbWordId = 32, StepWordId = 23 },
            new VerseMappingLink { ElbWordId = 35, StepWordId = 23 },
            new VerseMappingLink { ElbWordId = 35, StepWordId = 24 });

        written.Select(r => (r.ElbWordId, r.StepWordId, r.Role, r.StrongsNumber, r.MultiHead)).ToArray().ShouldBe(
        [
            (31, (int?)22, "head", (string?)"H9001", false),
            (33, (int?)22, "head", (string?)null, false),
            (32, (int?)23, "head", (string?)null, true),
            (35, (int?)23, "head", (string?)null, true),
            (35, (int?)24, "head", (string?)null, false),
        ]);
    }

    [Fact]
    public async Task LegacySave_KeptUnitWhoseHeadWasRemoved_BecomesHead()
    {
        var service = CreateService();
        _repository.GetMappingRowsAsync(true, 1, 1, 3, Arg.Any<CancellationToken>()).Returns(
        [
            new VerseMappingRow { ElbWordId = 34, StepWordId = 24, Role = "unit" },
            new VerseMappingRow { ElbWordId = 35, StepWordId = 24, Role = "head" },
        ]);

        var keptUnit = await SaveAsync(service, new VerseMappingLink { ElbWordId = 34, StepWordId = 24 }, new VerseMappingLink { ElbWordId = 35, StepWordId = 24 });
        keptUnit.Select(r => r.Role).ShouldBe(["unit", "head"]);

        var headRemoved = await SaveAsync(service, new VerseMappingLink { ElbWordId = 34, StepWordId = 24 });
        headRemoved.Single().Role.ShouldBe("head");
    }

    [Fact]
    public async Task Get_ReturnsRolesMorphemesAndMultiHead()
    {
        var service = CreateService();
        _repository.GetMappingRowsAsync(true, 1, 1, 3, Arg.Any<CancellationToken>()).Returns(
        [
            new VerseMappingRow { ElbWordId = 31, StepWordId = 22, Role = "head", StrongsNumber = "H9001" },
            new VerseMappingRow { ElbWordId = 33, StepWordId = 22, Role = "head" },
            new VerseMappingRow { ElbWordId = 34, StepWordId = 24, Role = "unit" },
            new VerseMappingRow { ElbWordId = 35, StepWordId = 24, Role = "head", MultiHead = true },
        ]);

        var response = (await service.GetAsync(1, 1, 3, callerCanWrite: true))!;

        response.Links.Select(l => (l.ElbWordId, l.Role, l.StrongsNumber, l.AllowMultiHead)).ToArray().ShouldBe(
        [
            (31, "head", (string?)"H9001", (bool?)null),
            (33, "head", (string?)null, (bool?)null),
            (34, "unit", (string?)null, (bool?)null),
            (35, "head", (string?)null, (bool?)true),
        ]);
        response.OriginalWords[0].Morphemes!.Select(m => (m.Strongs, m.IsMain)).ToArray().ShouldBe([("H9001", false), ("H0559", true)]);
    }
}
