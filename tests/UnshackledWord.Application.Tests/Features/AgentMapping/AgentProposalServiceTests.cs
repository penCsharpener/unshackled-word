using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.WebApi.Maintainer;
using static UnshackledWord.Application.Tests.Features.AgentMapping.AgentTestData;

namespace UnshackledWord.Application.Tests.Features.AgentMapping;

public class AgentProposalServiceTests
{
    private static readonly Guid PackageId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private readonly IAgentStoreRepository _store = Substitute.For<IAgentStoreRepository>();
    private readonly IAgentDataRepository _data = Substitute.For<IAgentDataRepository>();
    private readonly ISttsTagRepository _stts = Substitute.For<ISttsTagRepository>();
    private readonly AgentPackageMap _map = Map();
    private AgentRangeData _live = new();
    private IReadOnlyList<AgentProposalInsert> _stored = [];
    private int _proposedVerses;

    public AgentProposalServiceTests()
    {
        var german = new[]
        {
            German(101, "Anfang", position: 1), German(102, "des", position: 2), German(103, "Evangeliums", position: 3),
            German(104, "Jesu", position: 4), German(105, "ging", position: 5), German(106, "aus", position: 6), German(107, "Wort", position: 1, verse: 2),
        };
        var source = new[]
        {
            Source(201, "G0746", position: 1), Source(202, "G3588", position: 2), Source(203, "G2098", position: 3), Source(204, "G2424G", position: 4), Source(205, "G1607", position: 5),
            Source(206, "G3056", position: 1, verse: 2),
        };
        _live = new AgentRangeData { GermanWords = german, SourceWords = source };
        _map.Verses["v1"].Hash = AgentPackageBuilder.VerseHash(german.Where(w => w.Verse == 1), source.Where(s => s.RefId == 41001001));
        _map.Verses["v2"].Hash = AgentPackageBuilder.VerseHash(german.Where(w => w.Verse == 2), source.Where(s => s.RefId == 41001002));

        _data.GetRangeAsync(41, 1, 1, 1, 2, Arg.Any<CancellationToken>()).Returns(_ => _live);
        _data.GetKnownLemmasAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns(new HashSet<string>());
        _data.GetFormLemmasAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns([]);
        _data.GetConfirmedPairsAsync(Arg.Any<bool>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns([]);
        _stts.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);
    }

    private AgentProposalService Create(DateTime? expiresAt = null, string status = AgentPackageStatuses.Open, int superseded = 0)
    {
        _store.GetPackageAsync(PackageId, Arg.Any<CancellationToken>()).Returns(new AgentPackageRecord
        {
            Id = PackageId,
            RunId = 5,
            WordIdsHash = AgentPackageBuilder.PackageHash(_map),
            LocalIdMap = _map.ToJson(),
            Status = status,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddHours(1),
        });
        _store.CountProposedVersesAsync(PackageId, Arg.Any<CancellationToken>()).Returns(_ => _proposedVerses);
        _store.StoreProposalsAsync(Arg.Any<int>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyList<AgentProposalInsert>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            _stored = call.ArgAt<IReadOnlyList<AgentProposalInsert>>(2);
            _proposedVerses += _stored.Count;
            return new AgentProposalStoreResult { ProposalIds = _stored.Select((_, i) => 900L + i).ToList(), Superseded = superseded };
        });

        return new AgentProposalService(_store, _data, _stts, new AgentProposalStore(_store), Substitute.For<IConfiguration>(), TimeProvider.System, NullLogger<AgentProposalService>.Instance);
    }

    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();

    private static JsonElement AnswerOf(params string[] verses) => Json($$"""{"verses":[{{string.Join(",", verses)}}]}""");

    private const string CleanV1 = """{"v":"v1","links":[["g1","s1"],["g3","s3"],["g5","s5"],["g6","s5"]]}""";

    [Fact]
    public async Task UnknownPackage_IsNotFound()
    {
        var service = Create();

        (await service.SubmitAsync("not-a-guid", AnswerOf())).Outcome.ShouldBe(AgentSubmitOutcome.NotFound);
        (await service.SubmitAsync(Guid.NewGuid().ToString(), AnswerOf())).Outcome.ShouldBe(AgentSubmitOutcome.NotFound);
    }

    [Fact]
    public async Task ExpiredPackage_IsRefused_AndMarkedExpired()
    {
        var service = Create(expiresAt: DateTime.UtcNow.AddMinutes(-1));

        var result = await service.SubmitAsync(PackageId.ToString(), AnswerOf(CleanV1));

        result.Outcome.ShouldBe(AgentSubmitOutcome.Expired);
        await _store.Received(1).SetPackageStatusAsync(PackageId, AgentPackageStatuses.Expired, false, Arg.Any<CancellationToken>());
        await _store.DidNotReceiveWithAnyArgs().StoreProposalsAsync(default, default, default!, default);
    }

    [Fact]
    public async Task StalePackage_WithChangedWordIds_IsRefused_AndMarkedStale()
    {
        var service = Create();
        _live = new AgentRangeData
        {
            GermanWords = [.. _live.GermanWords.Where(w => w.Id != 103), German(999, "Evangeliums", position: 3)],
            SourceWords = _live.SourceWords,
        };

        var result = await service.SubmitAsync(PackageId.ToString(), AnswerOf(CleanV1));

        result.Outcome.ShouldBe(AgentSubmitOutcome.Stale);
        result.Errors.ShouldHaveSingleItem().ShouldContain("v1 (Mk 1:1) has other words than in the package");
        await _store.Received(1).SetPackageStatusAsync(PackageId, AgentPackageStatuses.Stale, false, Arg.Any<CancellationToken>());
        await _store.DidNotReceiveWithAnyArgs().StoreProposalsAsync(default, default, default!, default);
    }

    [Fact]
    public async Task StalePackage_WhenTheStoredHashDiffers_IsRefused()
    {
        var service = Create();
        _store.GetPackageAsync(PackageId, Arg.Any<CancellationToken>()).Returns(new AgentPackageRecord
        {
            Id = PackageId, RunId = 5, WordIdsHash = new string('0', 64), LocalIdMap = _map.ToJson(), Status = AgentPackageStatuses.Open, ExpiresAt = DateTime.UtcNow.AddHours(1),
        });

        var result = await service.SubmitAsync(PackageId.ToString(), AnswerOf(CleanV1));

        result.Outcome.ShouldBe(AgentSubmitOutcome.Stale);
        result.Errors.ShouldHaveSingleItem().ShouldContain("stored package hash");
    }

    [Fact]
    public async Task StalePackage_WithAVanishedVerse_IsRefused()
    {
        var service = Create();
        _live = new AgentRangeData { GermanWords = _live.GermanWords.Where(w => w.Verse == 1).ToList(), SourceWords = _live.SourceWords };

        (await service.SubmitAsync(PackageId.ToString(), AnswerOf(CleanV1))).Outcome.ShouldBe(AgentSubmitOutcome.Stale);
    }

    [Fact]
    public async Task AnAnswerForAnotherPackage_OrNotAnObject_IsInvalid()
    {
        var service = Create();

        var other = await service.SubmitAsync(PackageId.ToString(), Json($$"""{"packageId":"{{Guid.NewGuid()}}","verses":[]}"""));
        other.Outcome.ShouldBe(AgentSubmitOutcome.Invalid);
        other.Errors.ShouldHaveSingleItem().ShouldContain("names package");

        (await service.SubmitAsync(PackageId.ToString(), Json("[1,2]"))).Outcome.ShouldBe(AgentSubmitOutcome.Invalid);
        (await service.SubmitAsync(PackageId.ToString(), Json("\"{ not json\""))).Outcome.ShouldBe(AgentSubmitOutcome.Invalid);
    }

    [Fact]
    public async Task RejectedVerses_AreNotStored_AcceptedAndFlaggedAre()
    {
        var service = Create();
        const string rejectedV2 = """{"v":"v2","links":[["g7","s1"]]}""";

        var result = await service.SubmitAsync(PackageId.ToString(), AnswerOf(CleanV1, rejectedV2));

        result.Outcome.ShouldBe(AgentSubmitOutcome.Ok);
        var response = result.Response!;
        (response.Accepted, response.Flagged, response.Rejected).ShouldBe((1, 0, 1));
        response.Verses.Single(v => v.V == "v1").ProposalId.ShouldBe(900);
        var v2 = response.Verses.Single(v => v.V == "v2");
        v2.Status.ShouldBe("rejected");
        v2.Messages.ShouldBe(["v2 g7: source s1 belongs to v1"]);
        v2.ProposalId.ShouldBeNull();

        var insert = _stored.ShouldHaveSingleItem();
        (insert.BibleBookId, insert.Chapter, insert.Verse, insert.Language).ShouldBe((41, 1, 1, "greek"));
        insert.LinksJson.ShouldContain("\"elbWordId\":101");
        insert.LinksJson.ShouldContain("\"stepWordId\":201");
        insert.FlagsJson.ShouldBeNull();
        await _store.Received(1).SetPackageStatusAsync(PackageId, AgentPackageStatuses.Submitted, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AFlaggedVerse_IsStoredWithItsFlags()
    {
        var service = Create();
        const string flaggedV1 = """{"v":"v1","links":[["g1","s1"],["g3","s3"]],"unmapped":[["g5","x"],["g6","y"]]}""";

        var result = await service.SubmitAsync(PackageId.ToString(), AnswerOf(flaggedV1, """{"v":"v2","links":[["g7","s6"]]}"""));
        result.Response!.NotAnswered.ShouldBeEmpty();

        result.Response!.Flagged.ShouldBe(1);
        result.Response.Verses[0].Flags.ShouldContain(f => f.Code == "source-unlinked");
        _stored.Count.ShouldBe(2);
        _stored[0].FlagsJson.ShouldContain("source-unlinked");
        await _store.Received(1).SetPackageStatusAsync(PackageId, AgentPackageStatuses.Completed, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ASupersededOlderProposal_IsReported()
    {
        var service = Create(superseded: 2);

        var result = await service.SubmitAsync(PackageId.ToString(), AnswerOf(CleanV1, """{"v":"v2","links":[["g7","s6"]]}"""));

        result.Response!.Superseded.ShouldBe(2);
    }

    [Fact]
    public async Task ARejectedVerse_CanBeResubmittedWithTheSamePackage()
    {
        var service = Create();

        var first = await service.SubmitAsync(PackageId.ToString(), AnswerOf("""{"v":"v1","links":[["g1","s6"]]}"""));
        var second = await service.SubmitAsync(PackageId.ToString(), AnswerOf(CleanV1));

        first.Response!.Verses.ShouldHaveSingleItem().Status.ShouldBe("rejected");
        first.Response.NotAnswered.ShouldBe(["v2"]);
        second.Response!.Verses.ShouldHaveSingleItem().Status.ShouldBe("accepted");
    }
}
