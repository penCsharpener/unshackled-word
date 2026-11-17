using System.Text.Json;
using Microsoft.Extensions.Configuration;
using UnshackledWord.Application.Features.AgentMapping;
using static UnshackledWord.Application.Tests.Features.AgentMapping.AgentTestData;

namespace UnshackledWord.Application.Tests.Features.AgentMapping;

public class AgentPackageBuilderTests
{
    private readonly IAgentDataRepository _data = Substitute.For<IAgentDataRepository>();
    private readonly IAgentStoreRepository _store = Substitute.For<IAgentStoreRepository>();
    private readonly List<AgentPackageInsert> _inserted = [];

    private AgentPackageBuilder Create(int? budget = null)
    {
        var configuration = Substitute.For<IConfiguration>();
        if (budget is not null)
        {
            configuration["AgentMapping:TokenBudget"].Returns(budget.ToString());
        }

        _store.CreateRunAsync(Arg.Any<AgentRunInsert>(), Arg.Any<CancellationToken>()).Returns(77);
        _store.InsertPackageAsync(Arg.Do<AgentPackageInsert>(_inserted.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _data.GetFormLemmasAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns([]);
        _data.GetConfirmedPairsAsync(Arg.Any<bool>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns([]);

        return new AgentPackageBuilder(_data, _store, configuration, TimeProvider.System);
    }

    /// <summary>Verses 1..n, each with five words (ids 574000 + verse * 10 + i) and three original words (ids 880000 + verse * 10 + i).</summary>
    private void GivenVerses(int count)
    {
        var german = new List<AgentGermanWordRow>();
        var source = new List<AgentSourceWordRow>();
        for (var verse = 1; verse <= count; verse++)
        {
            for (var i = 1; i <= 5; i++)
            {
                german.Add(German(574000 + verse * 10 + i, "Wort" + i, lemma: "Wort", stts: "NN", pos: "NOUN", position: i, verse: verse));
            }

            german.Add(German(574000 + verse * 10 + 9, ",", position: 6, verse: verse, text: ","));
            for (var i = 1; i <= 3; i++)
            {
                source.Add(Source(880000 + verse * 10 + i, "G" + i, position: i, verse: verse));
            }
        }

        _data.GetRangeAsync(41, 1, 1, 1, count, Arg.Any<CancellationToken>()).Returns(new AgentRangeData { GermanWords = german, SourceWords = source, Mappings = [] });
    }

    private static AgentRange Range(int to) => new() { BibleBookId = 41, FromChapter = 1, FromVerse = 1, ToChapter = 1, ToVerse = to };

    [Fact]
    public async Task Build_UsesLocalIdsOnly_AndRemovesPunctuation()
    {
        GivenVerses(2);

        var response = await Create().BuildAsync(Range(2), AgentMode.Redo, null, userId: 3);

        response.ShouldNotBeNull();
        var json = JsonSerializer.Serialize(response.Packages[0].Package, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        json.ShouldNotContain("574011");
        json.ShouldNotContain("880011");
        var verse = response.Packages[0].Package.Verses[0];
        verse.V.ShouldBe("v1");
        verse.Ref.ShouldBe("Mk 1:1");
        verse.De.Count.ShouldBe(5, "the comma is removed");
        verse.De[0][0].ShouldBe("g1");
        verse.Src[0][0].ShouldBe("s1");
        response.Packages[0].Package.Verses[1].De[0][0].ShouldBe("g6", "ids run on across the verses of a package");
        response.Packages[0].Package.Verses[1].Src[0][0].ShouldBe("s4");
        response.Stats.GermanTokens.ShouldBe(10);
        response.Stats.RemovedPunctuation.ShouldBe(2);
        response.Stats.Open.ShouldBe(10);
    }

    [Fact]
    public async Task Build_CutsByTokenBudget_WithoutSplittingAVerse_InOrder()
    {
        GivenVerses(6);
        var builder = Create(budget: 300);

        var response = await builder.BuildAsync(Range(6), AgentMode.Redo, null, userId: 3);

        response!.Packages.Count.ShouldBeGreaterThan(1);
        var refs = response.Packages.SelectMany(p => p.Package.Verses.Select(v => v.Ref)).ToList();
        refs.ShouldBe(["Mk 1:1", "Mk 1:2", "Mk 1:3", "Mk 1:4", "Mk 1:5", "Mk 1:6"], "every verse once, in order");
        response.Packages.Select(p => p.Package.Verses.Count).ShouldAllBe(n => n >= 1);
        response.Packages.Skip(1).Select(p => p.Package.Verses[0].V).ShouldAllBe(v => v == "v1", "verse ids restart per package");
        foreach (var package in response.Packages)
        {
            package.EstimatedTokens.ShouldBeLessThanOrEqualTo(300);
        }
    }

    [Fact]
    public async Task Build_PutsAVerseAboveTheBudgetInAPackageOfItsOwn()
    {
        GivenVerses(2);

        var response = await Create(budget: 20).BuildAsync(Range(2), AgentMode.Redo, null, userId: 3);

        response!.Packages.Select(p => p.Package.Verses.Count).ShouldBe([1, 1]);
    }

    [Fact]
    public async Task Build_TokenBudgetParameter_OverridesTheConfiguredBudget()
    {
        GivenVerses(6);

        var response = await Create(budget: 100_000).BuildAsync(Range(6), AgentMode.Redo, tokenBudget: 300, userId: 3);

        response!.Packages.Count.ShouldBeGreaterThan(1);
        response.TokenBudget.ShouldBe(300);
    }

    [Fact]
    public async Task Build_SavesRunAndPackages_WithMapHashAndExpiry()
    {
        GivenVerses(2);

        var response = await Create().BuildAsync(Range(2), AgentMode.FillOpen, null, userId: 3);

        await _store.Received(1).CreateRunAsync(Arg.Is<AgentRunInsert>(r => r.CreatedByUserId == 3 && r.AiUserId == 3 && r.Mode == "fill-open" && r.Profile == "manual" && r.BibleBookId == 41 && r.FromRefId == 41001001 && r.ToRefId == 41001002), Arg.Any<CancellationToken>());
        response!.RunId.ShouldBe(77);
        var saved = _inserted.ShouldHaveSingleItem();
        saved.Id.ToString().ShouldBe(response.Packages[0].Package.PackageId);
        saved.RunId.ShouldBe(77);
        saved.ExpiresAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddHours(23));

        var map = AgentPackageMap.FromJson(saved.LocalIdMapJson);
        map.Language.ShouldBe("greek");
        map.Verses["v1"].German["g1"].Id.ShouldBe(574011);
        map.Verses["v2"].Source["s4"].Id.ShouldBe(880021);
        saved.WordIdsHash.ShouldBe(AgentPackageBuilder.PackageHash(map));
        map.Verses["v1"].Hash.ShouldBe(AgentPackageBuilder.VerseHash(
            Enumerable.Range(1, 6).Select(i => German(i == 6 ? 574019 : 574010 + i, "x", position: i)),
            Enumerable.Range(1, 3).Select(i => Source(880010 + i, "G" + i, position: i))));
    }

    [Fact]
    public async Task Build_SkipsVersesThatAreFullyFixed_ButCountsThem()
    {
        var german = new[] { German(1, "Jesu", lemma: "Jesus", humanEdited: true, position: 1, verse: 1), German(2, "Wort", position: 1, verse: 2) };
        var source = new[] { Source(201, "G1", verse: 1), Source(202, "G2", verse: 2) };
        _data.GetRangeAsync(41, 1, 1, 1, 2, Arg.Any<CancellationToken>()).Returns(new AgentRangeData { GermanWords = german, SourceWords = source, Mappings = [Mapping(1, 201, author: 7)] });

        var response = await Create().BuildAsync(Range(2), AgentMode.FillOpen, null, userId: 3);

        response!.Packages.ShouldHaveSingleItem().Package.Verses.Select(v => v.Ref).ShouldBe(["Mk 1:2"]);
        response.Stats.SkippedVerses.ShouldBe(1);
        response.Stats.Fixed.ShouldBe(1);
    }

    [Fact]
    public async Task Build_ReturnsNull_ForARangeWithoutWords()
    {
        _data.GetRangeAsync(41, 1, 1, 1, 1, Arg.Any<CancellationToken>()).Returns(new AgentRangeData());

        (await Create().BuildAsync(Range(1), AgentMode.Redo, null, userId: 3)).ShouldBeNull();
    }

    [Fact]
    public async Task Build_ShowsLemmaStateAndCandidatesInTheGermanRow()
    {
        var german = new[] { German(1, "Jesu", lemma: "Jesus", stts: "NE", humanEdited: true, position: 1), German(2, "Weg", lemma: "Weg", stts: "NN", position: 2) };
        _data.GetRangeAsync(41, 1, 1, 1, 1, Arg.Any<CancellationToken>()).Returns(new AgentRangeData { GermanWords = german, SourceWords = [Source(201, "G1")], Mappings = [] });
        var builder = Create();
        _data.GetFormLemmasAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns([FormLemma("weg", "Weg", 5), FormLemma("weg", "weg", 2)]);

        var verse = (await builder.BuildAsync(Range(1), AgentMode.Redo, null, userId: 3))!.Packages[0].Package.Verses[0];

        verse.De[0].ShouldBe(["g1", "Jesu", "Jesus", "NE", "fixed-lemma"]);
        verse.De[1].ShouldBe(["g2", "Weg", "Weg", "NN", null, new[] { "weg" }]);
    }
}
