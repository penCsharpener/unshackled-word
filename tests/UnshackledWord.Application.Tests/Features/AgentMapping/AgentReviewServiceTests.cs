using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.VerseMapping;
using UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping;
using UnshackledWord.Domain.WebApi.Lemmas;
using UnshackledWord.Domain.WebApi.Maintainer;
using static UnshackledWord.Application.Tests.Features.AgentMapping.AgentTestData;

namespace UnshackledWord.Application.Tests.Features.AgentMapping;

public class AgentReviewServiceTests
{
    private const int AiUser = 77;
    private const int Reviewer = 5;
    private static readonly Guid PackageId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    private readonly IAgentReviewRepository _proposals = Substitute.For<IAgentReviewRepository>();
    private readonly IAgentStoreRepository _store = Substitute.For<IAgentStoreRepository>();
    private readonly IAgentDataRepository _data = Substitute.For<IAgentDataRepository>();
    private readonly IVerseMappingService _mapping = Substitute.For<IVerseMappingService>();
    private readonly IAgentLemmaWriter _lemmas = Substitute.For<IAgentLemmaWriter>();
    private readonly AgentPackageMap _map = Map();
    private readonly Dictionary<long, AgentProposalRecord> _records = [];
    private AgentRangeData _live = new();

    public AgentReviewServiceTests()
    {
        var german = new[] { German(101, "Anfang", position: 1), German(105, "ging", position: 2), German(107, "Wort", position: 1, verse: 2) };
        var source = new[] { Source(201, "G0746", position: 1), Source(206, "G3056", position: 1, verse: 2) };
        _live = new AgentRangeData { GermanWords = german, SourceWords = source };
        _map.Verses["v1"].Hash = AgentPackageBuilder.VerseHash(german.Where(w => w.Verse == 1), source.Where(s => s.RefId == 41001001));
        _map.Verses["v2"].Hash = AgentPackageBuilder.VerseHash(german.Where(w => w.Verse == 2), source.Where(s => s.RefId == 41001002));

        _data.GetRangeAsync(41, 1, Arg.Any<int>(), 1, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(_ => _live);
        _store.GetPackageAsync(PackageId, Arg.Any<CancellationToken>()).Returns(_ => new AgentPackageRecord { Id = PackageId, RunId = 5, LocalIdMap = _map.ToJson(), WordIdsHash = "x", Status = AgentPackageStatuses.Completed, ExpiresAt = DateTime.UtcNow.AddHours(1) });
        _proposals.GetAsync(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(c => _records.GetValueOrDefault(c.ArgAt<long>(0)));
        _proposals.TryCloseAsync(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(true);
        _mapping.SaveAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<IReadOnlyList<VerseMappingLink>>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new SaveVerseMappingResult { Outcome = SaveVerseMappingOutcome.Saved });
        _lemmas.GetVerseAsync(41, 1, 1, Arg.Any<CancellationToken>()).Returns(
        [
            new LemmaWordRow { WordId = 101, PlainWord = "Anfang", Lemma = "Anfang", PartOfSpeech = "NOUN", Stts = "NN" },
            new LemmaWordRow { WordId = 105, PlainWord = "ging", Lemma = "gehn", PartOfSpeech = "VERB", Stts = "VVFIN", Tense = "Past", Mood = "Ind" },
        ]);
        _lemmas.CheckAsync(Arg.Any<IReadOnlyList<LemmaWordChange>>(), Arg.Any<CancellationToken>()).Returns([]);
        _lemmas.ApplyAsync(Arg.Any<IReadOnlyList<LemmaWordChange>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);
        _mapping.GetAsync(41, 1, 1, false, Arg.Any<CancellationToken>()).Returns(new GetVerseMappingResponse
        {
            BibleBookId = 41, Chapter = 1, Verse = 1,
            GermanWords = [new() { Id = 101, Text = "Anfang", PositionInVerse = 1 }, new() { Id = 105, Text = "ging", PositionInVerse = 2 }],
            OriginalWords = [new() { Id = 201, Original = "Ἀρχή" }],
            Links = [new() { ElbWordId = 101, StepWordId = 201 }, new() { ElbWordId = 105, StepWordId = 205 }],
        });
    }

    private AgentReviewService Create() => new(_proposals, _store, _data, _mapping, _lemmas, NullLogger<AgentReviewService>.Instance);

    private AgentProposalRecord Add(long id, int verse = 1, string status = AgentProposalStatuses.Open, VerseMappingLink[]? links = null, AgentLemmaChange[]? lemmas = null, string? flags = null, string? parts = null)
    {
        var record = new AgentProposalRecord
        {
            Id = id, RunId = 5, PackageId = PackageId, BibleBookId = 41, Chapter = 1, Verse = verse, Language = "greek",
            LinksJson = JsonSerializer.Serialize(links ?? [new VerseMappingLink { ElbWordId = 101, StepWordId = 201 }], Web),
            LemmasJson = lemmas is null ? null : JsonSerializer.Serialize(lemmas, Web),
            FlagsJson = flags, PartsJson = parts, Status = status, CreatedAt = DateTime.UtcNow, AiUserId = AiUser, RunProfile = "ollama", RunModel = "gemma4:31b",
        };
        _records[id] = record;

        return record;
    }

    [Fact]
    public async Task Merge_WritesAsAiUser_RecordsReviewer_AndClosesAsMerged()
    {
        Add(1);

        var result = await Create().MergeAsync(1, Reviewer);

        result.Outcome.ShouldBe(AgentReviewOutcome.Ok);
        result.Value!.Status.ShouldBe("merged");
        result.Value.AuthorUserId.ShouldBe(AiUser);
        result.Value.ReviewerUserId.ShouldBe(Reviewer);
        await _mapping.Received(1).SaveAsync(41, 1, 1, Arg.Is<IReadOnlyList<VerseMappingLink>>(l => l.Count == 1 && l[0].ElbWordId == 101), AiUser, false, Arg.Any<CancellationToken>());
        await _proposals.Received(1).TryCloseAsync(1, "merged", Reviewer, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Merge_UnknownProposal_IsNotFound()
    {
        (await Create().MergeAsync(99, Reviewer)).Outcome.ShouldBe(AgentReviewOutcome.NotFound);
    }

    [Theory]
    [InlineData(AgentProposalStatuses.Merged)]
    [InlineData(AgentProposalStatuses.EditedMerged)]
    [InlineData(AgentProposalStatuses.Discarded)]
    [InlineData(AgentProposalStatuses.Superseded)]
    public async Task Merge_NotOpen_IsNotOpen_AndWritesNothing(string status)
    {
        Add(1, status: status);

        var result = await Create().MergeAsync(1, Reviewer);

        result.Outcome.ShouldBe(AgentReviewOutcome.NotOpen);
        await _mapping.DidNotReceiveWithAnyArgs().SaveAsync(default, default, default, default!, default, default, default);
    }

    [Fact]
    public async Task Merge_VerseWordsChanged_IsStale_Veraltet()
    {
        Add(1);
        _live = new AgentRangeData { GermanWords = [.. _live.GermanWords, German(108, "neu", position: 3)], SourceWords = _live.SourceWords };

        var result = await Create().MergeAsync(1, Reviewer);

        result.Outcome.ShouldBe(AgentReviewOutcome.Stale);
        result.Errors.ShouldContain(e => e.StartsWith("veraltet"));
        await _mapping.DidNotReceiveWithAnyArgs().SaveAsync(default, default, default, default!, default, default, default);
        await _proposals.DidNotReceiveWithAnyArgs().TryCloseAsync(default, default!, default, default);
    }

    [Fact]
    public async Task Merge_PackageGone_IsStale()
    {
        Add(1);
        _store.GetPackageAsync(PackageId, Arg.Any<CancellationToken>()).Returns((AgentPackageRecord?)null);

        (await Create().MergeAsync(1, Reviewer)).Outcome.ShouldBe(AgentReviewOutcome.Stale);
    }

    [Fact]
    public async Task Merge_WithLemmas_CarriesMorphologyOver_AndWritesAuthorAndReviewer()
    {
        Add(1, lemmas: [new AgentLemmaChange { ElbWordId = 105, Lemma = "gehen", Stts = "VVFIN", PartOfSpeech = "VERB", BeforeLemma = "gehn" }]);

        var result = await Create().MergeAsync(1, Reviewer);

        result.Outcome.ShouldBe(AgentReviewOutcome.Ok);
        result.Value!.LemmasWritten.ShouldBe(1);
        await _lemmas.Received(1).ApplyAsync(
            Arg.Is<IReadOnlyList<LemmaWordChange>>(c => c.Count == 1 && c[0].WordId == 105 && c[0].Lemma == "gehen" && c[0].Tense == "Past" && c[0].Mood == "Ind" && c[0].Stts == "VVFIN"),
            AiUser, Reviewer, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Merge_LemmaEditedByHumanSinceProposal_IsStale()
    {
        Add(1, lemmas: [new AgentLemmaChange { ElbWordId = 105, Lemma = "gehen", BeforeLemma = "gehn-alt" }]);

        var result = await Create().MergeAsync(1, Reviewer);

        result.Outcome.ShouldBe(AgentReviewOutcome.Stale);
        result.Errors.ShouldContain(e => e.Contains("ging") && e.StartsWith("veraltet"));
        await _mapping.DidNotReceiveWithAnyArgs().SaveAsync(default, default, default, default!, default, default, default);
    }

    [Fact]
    public async Task Merge_LemmaBreaksHardRule_IsInvalid_BeforeAnythingIsWritten()
    {
        Add(1, lemmas: [new AgentLemmaChange { ElbWordId = 101, Lemma = "anfang", Stts = "NN", PartOfSpeech = "NOUN", BeforeLemma = "Anfang" }]);

        var result = await Create().MergeAsync(1, Reviewer);

        result.Outcome.ShouldBe(AgentReviewOutcome.Invalid);
        result.Errors.ShouldContain(e => e.Contains("capital"));
        await _mapping.DidNotReceiveWithAnyArgs().SaveAsync(default, default, default, default!, default, default, default);
        await _lemmas.DidNotReceiveWithAnyArgs().ApplyAsync(default!, default, default, default);
    }

    [Fact]
    public async Task Merge_SttsGuardMessages_AreInvalid()
    {
        Add(1, lemmas: [new AgentLemmaChange { ElbWordId = 105, Lemma = "gehen", Stts = "VVINF", BeforeLemma = "gehn" }]);
        _lemmas.CheckAsync(Arg.Any<IReadOnlyList<LemmaWordChange>>(), Arg.Any<CancellationToken>()).Returns(["STTS VVINF does not fit VERB"]);

        var result = await Create().MergeAsync(1, Reviewer);

        result.Outcome.ShouldBe(AgentReviewOutcome.Invalid);
        result.Errors.ShouldContain("STTS VVINF does not fit VERB");
    }

    [Fact]
    public async Task Merge_MappingRulesFail_IsInvalid_AndProposalStaysOpen()
    {
        Add(1);
        _mapping.SaveAsync(41, 1, 1, Arg.Any<IReadOnlyList<VerseMappingLink>>(), AiUser, false, Arg.Any<CancellationToken>())
            .Returns(new SaveVerseMappingResult { Outcome = SaveVerseMappingOutcome.Invalid, Errors = ["duplicate link"] });

        var result = await Create().MergeAsync(1, Reviewer);

        result.Outcome.ShouldBe(AgentReviewOutcome.Invalid);
        result.Errors.ShouldBe(["duplicate link"]);
        await _proposals.DidNotReceiveWithAnyArgs().TryCloseAsync(default, default!, default, default);
        await _lemmas.DidNotReceiveWithAnyArgs().ApplyAsync(default!, default, default, default);
    }

    [Fact]
    public async Task Merge_EditingDisabled_IsForbidden()
    {
        Add(1);
        _mapping.SaveAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<IReadOnlyList<VerseMappingLink>>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new SaveVerseMappingResult { Outcome = SaveVerseMappingOutcome.Forbidden, Errors = ["disabled"] });

        (await Create().MergeAsync(1, Reviewer)).Outcome.ShouldBe(AgentReviewOutcome.Forbidden);
    }

    [Fact]
    public async Task Merge_ProposalWithParts_IsMerged_PartsReachTheSave()
    {
        Add(1, links: [new VerseMappingLink { ElbWordId = 101, StepWordId = 201, PartOrder = 1, GermanWordPart = "Anf" }, new VerseMappingLink { ElbWordId = 101, StepWordId = 201, PartOrder = 2, GermanWordPart = "ang" }], parts: """[{"elbWordId":101,"stepWordId":201,"order":1,"text":"Anf"}]""");

        var result = await Create().MergeAsync(1, Reviewer);

        result.Outcome.ShouldBe(AgentReviewOutcome.Ok);
        await _mapping.Received(1).SaveAsync(41, 1, 1, Arg.Is<IReadOnlyList<VerseMappingLink>>(l => l.Count == 2 && l[0].PartOrder == 1 && l[1].GermanWordPart == "ang"), AiUser, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EditMerge_WritesEditedLinks_AsReviewer_AndClosesAsEditedMerged()
    {
        Add(1, lemmas: [new AgentLemmaChange { ElbWordId = 105, Lemma = "gehen", BeforeLemma = "something-else" }]);
        var edited = new EditMergeAgentProposalRequest
        {
            Id = 1,
            Links = [new VerseMappingLink { ElbWordId = 105, StepWordId = 205 }],
            Lemmas = [new AgentLemmaEditDto { ElbWordId = 105, Lemma = "gehen", Stts = "VVFIN", PartOfSpeech = "VERB" }],
            ClearParts = true,
        };

        var result = await Create().EditMergeAsync(edited, Reviewer);

        result.Outcome.ShouldBe(AgentReviewOutcome.Ok);
        result.Value!.Status.ShouldBe("edited-merged");
        result.Value.AuthorUserId.ShouldBe(Reviewer);
        await _mapping.Received(1).SaveAsync(41, 1, 1, Arg.Is<IReadOnlyList<VerseMappingLink>>(l => l.Count == 1 && l[0].ElbWordId == 105), Reviewer, true, Arg.Any<CancellationToken>());
        await _lemmas.Received(1).ApplyAsync(Arg.Any<IReadOnlyList<LemmaWordChange>>(), Reviewer, Reviewer, Arg.Any<CancellationToken>());
        await _proposals.Received(1).TryCloseAsync(1, "edited-merged", Reviewer, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EditMerge_StillRefusesStaleVerse_AndLemmaOfOtherVerse()
    {
        Add(1);
        var other = new EditMergeAgentProposalRequest { Id = 1, Links = [], Lemmas = [new AgentLemmaEditDto { ElbWordId = 999, Lemma = "Haus" }] };

        var invalid = await Create().EditMergeAsync(other, Reviewer);
        invalid.Outcome.ShouldBe(AgentReviewOutcome.Invalid);

        _live = new AgentRangeData { GermanWords = [], SourceWords = [] };
        (await Create().EditMergeAsync(new EditMergeAgentProposalRequest { Id = 1, Links = [] }, Reviewer)).Outcome.ShouldBe(AgentReviewOutcome.Stale);
    }

    [Fact]
    public async Task EditMerge_AlreadyMerged_IsNotOpen()
    {
        Add(1, status: AgentProposalStatuses.Merged);

        (await Create().EditMergeAsync(new EditMergeAgentProposalRequest { Id = 1, Links = [] }, Reviewer)).Outcome.ShouldBe(AgentReviewOutcome.NotOpen);
    }

    [Fact]
    public async Task Discard_OpenProposal_IsDiscarded_WithoutTouchingLiveData()
    {
        Add(1);

        var result = await Create().DiscardAsync(1, Reviewer);

        result.Outcome.ShouldBe(AgentReviewOutcome.Ok);
        result.Value!.Status.ShouldBe("discarded");
        await _proposals.Received(1).TryCloseAsync(1, "discarded", Reviewer, Arg.Any<CancellationToken>());
        await _mapping.DidNotReceiveWithAnyArgs().SaveAsync(default, default, default, default!, default, default, default);
    }

    [Fact]
    public async Task Discard_NotOpenOrUnknown()
    {
        Add(1, status: AgentProposalStatuses.Superseded);

        (await Create().DiscardAsync(1, Reviewer)).Outcome.ShouldBe(AgentReviewOutcome.NotOpen);
        (await Create().DiscardAsync(2, Reviewer)).Outcome.ShouldBe(AgentReviewOutcome.NotFound);
    }

    [Fact]
    public async Task Discard_LostRace_IsNotOpen()
    {
        Add(1);
        _proposals.TryCloseAsync(1, "discarded", Reviewer, Arg.Any<CancellationToken>()).Returns(false);

        (await Create().DiscardAsync(1, Reviewer)).Outcome.ShouldBe(AgentReviewOutcome.NotOpen);
    }

    [Fact]
    public async Task BulkMerge_MergesUnflagged_SkipsFlagged_ReportsStaleAndInvalid()
    {
        var flagged = """[{"code":"source-unlinked","message":"v1: no German word links to s3"}]""";
        Add(1);
        Add(2, flags: flagged);
        Add(3, verse: 2, links: [new VerseMappingLink { ElbWordId = 107, StepWordId = 206 }]);
        Add(4, status: AgentProposalStatuses.Discarded);
        _live = new AgentRangeData { GermanWords = [.. _live.GermanWords, German(108, "neu", position: 2, verse: 2)], SourceWords = _live.SourceWords };
        _proposals.ListAsync(5, AgentProposalStatuses.Open, Arg.Any<CancellationToken>()).Returns(_records.Values.Where(r => r.Status == AgentProposalStatuses.Open).ToList());

        var result = await Create().BulkMergeUnflaggedAsync(5, Reviewer);

        result.Outcome.ShouldBe(AgentReviewOutcome.Ok);
        result.Value!.Merged.ShouldBe(1);
        result.Value.SkippedFlagged.ShouldBe(1);
        result.Value.Failed.Count.ShouldBe(1);
        result.Value.Failed[0].Id.ShouldBe(3);
        result.Value.Failed[0].Reason.ShouldBe("stale");
        await _proposals.Received(1).TryCloseAsync(1, "merged", Reviewer, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BulkMerge_EditingDisabled_StopsWithForbidden()
    {
        Add(1);
        _proposals.ListAsync(5, AgentProposalStatuses.Open, Arg.Any<CancellationToken>()).Returns(_records.Values.ToList());
        _mapping.SaveAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<IReadOnlyList<VerseMappingLink>>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new SaveVerseMappingResult { Outcome = SaveVerseMappingOutcome.Forbidden, Errors = ["disabled"] });

        (await Create().BulkMergeUnflaggedAsync(5, Reviewer)).Outcome.ShouldBe(AgentReviewOutcome.Forbidden);
    }

    [Fact]
    public async Task List_FiltersFlaggedOnly_AndCountsFlags()
    {
        Add(1);
        Add(2, flags: """[{"code":"a","message":"m"},{"code":"b","message":"n"}]""", lemmas: [new AgentLemmaChange { ElbWordId = 105, Lemma = "gehen" }]);
        _proposals.ListAsync(5, null, Arg.Any<CancellationToken>()).Returns(_records.Values.ToList());

        var all = await Create().ListAsync(5, null, false);
        var flagged = await Create().ListAsync(5, null, true);

        all.Proposals.Count.ShouldBe(2);
        flagged.Proposals.ShouldHaveSingleItem().Id.ShouldBe(2);
        flagged.Proposals[0].FlagCount.ShouldBe(2);
        flagged.Proposals[0].HasLemmaChanges.ShouldBeTrue();
    }

    [Fact]
    public async Task Diff_SplitsLinksIntoUnchangedAddedRemoved_AndShowsLemmaChange()
    {
        Add(1, links: [new VerseMappingLink { ElbWordId = 101, StepWordId = 201 }, new VerseMappingLink { ElbWordId = 105, IsAddedWord = true, ParentElbWordId = 101 }],
            lemmas: [new AgentLemmaChange { ElbWordId = 105, Lemma = "gehen", BeforeLemma = "gehn" }], flags: """[{"code":"x","message":"y"}]""");
        _records[1].Note = "check ging";

        var diff = await Create().GetDiffAsync(1);

        diff.ShouldNotBeNull();
        diff.Unchanged.ShouldHaveSingleItem().German.ShouldBe("Anfang");
        diff.Added.ShouldHaveSingleItem().IsAddedWord.ShouldBeTrue();
        diff.Added[0].ParentGerman.ShouldBe("Anfang");
        diff.Removed.ShouldHaveSingleItem().ElbWordId.ShouldBe(105);
        diff.Stale.ShouldBeFalse();
        diff.Note.ShouldBe("check ging");
        diff.Flags.ShouldHaveSingleItem().Code.ShouldBe("x");
        var lemma = diff.LemmaChanges.ShouldHaveSingleItem();
        lemma.CurrentLemma.ShouldBe("gehn");
        lemma.Lemma.ShouldBe("gehen");
        lemma.Changed.ShouldBeFalse();
        diff.AiUserId.ShouldBe(AiUser);
    }

    [Fact]
    public async Task Diff_MarksStaleProposal_AndUnknownIsNull()
    {
        Add(1);
        _live = new AgentRangeData();

        var diff = await Create().GetDiffAsync(1);

        diff!.Stale.ShouldBeTrue();
        diff.StaleReason.ShouldNotBeNull();
        (await Create().GetDiffAsync(2)).ShouldBeNull();
    }
}
