using System.Text.Json;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.AgentMapping.Scoring;
using UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping;
using static UnshackledWord.Application.Tests.Features.AgentMapping.Scoring.ScoringTestData;

namespace UnshackledWord.Application.Tests.Features.AgentMapping.Scoring;

/// <summary>How a stored proposal (a diff against the stored state) becomes the state the scorer compares.</summary>
public class AgentProposalStateTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    private static AgentVerseSnapshot Baseline() => AgentVerseSnapshots.Build(Book, 1, 17, Range(lemma2: "Jesu"));

    private static ScoredProposal Proposal(IEnumerable<VerseMappingLink>? links = null, IEnumerable<AgentLemmaChange>? lemmas = null, IEnumerable<AgentRoleChange>? roles = null, IEnumerable<AgentFlag>? flags = null) => new()
    {
        Id = 1,
        BibleBookId = Book,
        Chapter = 1,
        Verse = 17,
        LinksJson = JsonSerializer.Serialize(links ?? [], Web),
        LemmasJson = lemmas is null ? null : JsonSerializer.Serialize(lemmas, Web),
        RolesJson = roles is null ? null : JsonSerializer.Serialize(roles, Web),
        FlagsJson = flags is null ? null : JsonSerializer.Serialize(flags, Web),
    };

    private static IEnumerable<VerseMappingLink> SameLinks() =>
    [
        new() { ElbWordId = 101, StepWordId = 201 },
        new() { ElbWordId = 102, StepWordId = 202 },
        new() { ElbWordId = 103, StepWordId = 203 },
        new() { ElbWordId = 104, IsAddedWord = true, ParentElbWordId = 103 },
    ];

    [Fact]
    public void LemmaChange_OverridesTheStoredLemma_OtherWordsKeepTheirs()
    {
        var state = AgentProposalState.Apply(Baseline(), Proposal(SameLinks(), [new AgentLemmaChange { ElbWordId = 102, Lemma = "Jesus", Stts = "NE" }]));

        state.Words.Single(w => w.Pos == 2).Lemma.ShouldBe("Jesus");
        state.Words.Single(w => w.Pos == 3).Lemma.ShouldBe("sprechen");
    }

    [Fact]
    public void SilenceKeepsTheStoredState()
    {
        var baseline = Baseline();

        var state = AgentProposalState.Apply(baseline, Proposal());

        state.Words.Single(w => w.Pos == 2).Lemma.ShouldBe("Jesu");
        state.Links.Count.ShouldBe(3);
        state.Added.Count.ShouldBe(1);
    }

    [Fact]
    public void LinksWithoutRoleKeepTheStoredRole_OfTheSamePair()
    {
        var baseline = AgentVerseSnapshots.Build(Book, 1, 17, Range(role2: "unit"));

        var state = AgentProposalState.Apply(baseline, Proposal(SameLinks()));

        state.Links.Single(l => l.G == 2).Role.ShouldBe("unit");
    }

    [Fact]
    public void RoleChange_SetsRole_AndMultiHeadIsAHead()
    {
        var change = new AgentRoleChange { ElbWordId = 101, StepWordId = 201, Role = "unit" };
        var multi = new AgentRoleChange { ElbWordId = 103, StepWordId = 203, Role = "multi-head", MultiHead = true };

        var state = AgentProposalState.Apply(Baseline(), Proposal(SameLinks(), roles: [change, multi]));

        state.Links.Single(l => l.G == 1).Role.ShouldBe("unit");
        state.Links.Single(l => l.G == 3).Role.ShouldBe("head");
    }

    [Fact]
    public void PronounBecomingAnAddedWord_ReplacesItsLink()
    {
        var baseline = AgentVerseSnapshots.Build(Book, 1, 17, Range());
        var change = new AgentRoleChange { ElbWordId = 102, StepWordId = 202, AddedParentElbWordId = 103 };

        var state = AgentProposalState.Apply(baseline, Proposal(SameLinks(), roles: [change]));

        state.Links.Any(l => l.G == 2).ShouldBeFalse();
        state.Added.ShouldContain(a => a.G == 2 && a.Parent == 3);
    }

    [Fact]
    public void ProposedLinkSet_ReplacesTheStoredOne_AndIdsOutsideTheVerseAreIgnored()
    {
        var links = new List<VerseMappingLink>
        {
            new() { ElbWordId = 101, StepWordId = 202 },
            new() { ElbWordId = 999, StepWordId = 203 },
            new() { ElbWordId = 103, StepWordId = 888 },
        };

        var state = AgentProposalState.Apply(Baseline(), Proposal(links));

        state.Links.Select(l => (l.G, l.S)).ShouldBe([(1, 2)]);
        state.Added.ShouldBeEmpty();
    }

    [Fact]
    public void FlagCodes_AreReadFromTheStoredFlags()
    {
        AgentProposalState.FlagCodes(Proposal(flags: [new AgentFlag("homograph", "x"), new AgentFlag("multi-head", "y")]).FlagsJson).ShouldBe(["homograph", "multi-head"]);
        AgentProposalState.FlagCodes(null).ShouldBeEmpty();
    }

    [Fact]
    public void PackageShares_SplitTokensAndCostEvenlyOverThePackageVerses()
    {
        var map = new AgentPackageMap
        {
            BookId = Book,
            Verses = new Dictionary<string, AgentVerseMap>
            {
                ["v1"] = new() { Chapter = 1, Verse = 17 },
                ["v2"] = new() { Chapter = 1, Verse = 18 },
            },
        };
        var row = new ScorePackageRow { Id = Guid.NewGuid(), LocalIdMap = map.ToJson(), InputTokens = 1001, OutputTokens = 200, CostUsd = 0.1m };

        var shares = AgentScoreService.PackageShares([row]);

        shares.Keys.ShouldBe([(Book, 1, 17), (Book, 1, 18)], ignoreOrder: true);
        shares[(Book, 1, 17)].CostUsd.ShouldBe(0.05m);
        shares[(Book, 1, 18)].OutputTokens.ShouldBe(100);
    }
}
