using System.Security.Claims;
using System.Text.Json;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.AgentMapping.Lxx;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.Lxx.Review;
using UnshackledWord.Domain.WebApi.Maintainer;
using BooksEndpoint = UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.LxxReview.Books.Endpoint;
using ChapterEndpoint = UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.LxxReview.Chapter.Endpoint;
using ConfirmEndpoint = UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.LxxReview.Confirm.Endpoint;
using ConfirmSafeEndpoint = UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.LxxReview.ConfirmSafe.Endpoint;
using DiscardEndpoint = UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.LxxReview.Discard.Endpoint;
using LexiconEndpoint = UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.LxxReview.Lexicon.Endpoint;
using MorphologyEndpoint = UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.LxxReview.Morphology.Endpoint;
using ConfirmValidator = UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.LxxReview.Confirm.Validator;
using ChapterValidator = UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.LxxReview.Chapter.Validator;
using SafeValidator = UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.LxxReview.ConfirmSafe.Validator;

namespace UnshackledWord.Tooling.WebApi.Tests;

/// <summary>Roadmap 0028.7f: the six review endpoints plus lexicon and morphology: Admin/Maintainer only, status codes and one happy path each.</summary>
public class LxxReviewEndpointTests
{
    private const int UnitId = 100;
    private const long ProposalId = 7;

    private static readonly LxxLexicon Lexicon = LxxLexicon.Build([new LxxLexiconEntry("G2532", "καί", "and"), new LxxLexiconEntry("G1096", "γίνομαι", "to become")]);

    private sealed class FakeLexicon : ILxxLexiconSource
    {
        public Task<LxxLexicon> GetAsync(CancellationToken token = default) => Task.FromResult(Lexicon);
    }

    /// <summary>Runs the service's plan against a fixed state; remembers what would be written.</summary>
    private sealed class FakeRepository : ILxxReviewRepository
    {
        public LxxReviewState State { get; set; } = new();
        public List<LxxReviewChapterCountRow> Counts { get; } = [];
        public LxxReviewWrite? Written { get; private set; }
        public int? WrittenBy { get; private set; }

        public Task<IReadOnlyList<LxxReviewChapterCountRow>> GetChapterCountsAsync(CancellationToken token = default) => Task.FromResult<IReadOnlyList<LxxReviewChapterCountRow>>(Counts);

        public Task<LxxReviewState> GetChapterAsync(int bibleBookId, int chapter, CancellationToken token = default) => Task.FromResult(State);

        public Task<LxxReviewApplied<T>> ApplyAsync<T>(IReadOnlyCollection<int> lxxVerseIds, Func<LxxReviewState, LxxReviewPlan<T>> plan, int userId, CancellationToken token = default)
        {
            var decided = plan(State);
            Written = decided.Write;
            WrittenBy = userId;
            return Task.FromResult(new LxxReviewApplied<T>(decided.Result, false));
        }
    }

    private readonly FakeRepository _repository = new();

    public LxxReviewEndpointTests()
    {
        _repository.State = new LxxReviewState
        {
            Units = [new LxxAgentUnitRow { Id = UnitId, BibleBookId = 8, Chapter = 1, Verse = 1, MapKind = "one", SortOrder = 1, HebRefId = 8_001_001 }],
            Words = [new LxxAgentWordRow { Id = 1001, LxxVerseId = UnitId, Position = 1, Greek = "ΚΑΙ" }],
            HebrewWords = [new AgentSourceWordRow { Id = 501, RefId = 8_001_001, Position = 1, Text = "וַיְהִי", Strongs = "H1961" }],
            Proposals =
            [
                new LxxReviewProposalRow
                {
                    Id = ProposalId, RunId = 1, LxxVerseId = UnitId, CreatedAt = DateTime.UtcNow,
                    LemmasJson = """[{"lxxWordId":1001,"position":1,"lemma":"καί","strongs":"G2532","morph":"CONJ","confidence":"high","prefilled":false}]""",
                    LinksJson = """[{"lxxWordId":1001,"position":1,"stepHebrewWordId":501,"hebNumber":1}]""",
                    FlagsJson = """{"lxxVerseId":100,"flags":[]}""",
                },
            ],
        };
    }

    private LxxReviewService Service() => new(_repository, new FakeLexicon());

    private T Create<T>(int? userId = null) where T : class, IEndpoint =>
        Factory.Create<T>(ctx =>
        {
            if (userId is { } id)
            {
                ctx.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], "test"));
            }
        }, typeof(T) == typeof(MorphologyEndpoint) ? [] : [Service()]);

    // ---- auth -------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AllEndpoints_RequireLogin_AndOnlyAdminAndMaintainerPass()
    {
        foreach (var type in new[] { typeof(BooksEndpoint), typeof(ChapterEndpoint), typeof(ConfirmEndpoint), typeof(DiscardEndpoint), typeof(ConfirmSafeEndpoint), typeof(LexiconEndpoint), typeof(MorphologyEndpoint) })
        {
            // RequiresLogin is the 401 for anonymous callers (the route group's policy); Roles() is the 403 for any other role.
            type.GetCustomAttributes(inherit: true).Select(a => a.GetType().Name).ShouldContain("RequiresLoginAttribute", type.FullName);

            var endpoint = (BaseEndpoint)typeof(LxxReviewEndpointTests).GetMethod(nameof(Create), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .MakeGenericMethod(type).Invoke(this, [null])!;
            // Factory.Create already ran Configure().
            endpoint.Definition.AllowedRoles.ShouldNotBeNull(type.FullName);
            endpoint.Definition.AllowedRoles!.OrderBy(r => r).ShouldBe(new[] { AppRoles.Admin, AppRoles.Maintainer }.OrderBy(r => r), type.FullName);
        }
    }

    // ---- reads ------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Books_ListsBooksWithOpenWork()
    {
        _repository.Counts.Add(new LxxReviewChapterCountRow { BibleBookId = 8, Chapter = 1, OpenUnits = 2, AiUnits = 1, LowWords = 3 });
        var endpoint = Create<BooksEndpoint>();

        await endpoint.HandleAsync(CancellationToken.None);

        var book = endpoint.Response.Books.Single();
        (book.BibleBookId, book.OpenUnits, book.LowWords, book.Chapters.Single().Chapter).ShouldBe((8, 2, 3, 1));
        endpoint.HttpContext.Response.Headers.CacheControl.ToString().ShouldBe("no-store");
    }

    [Fact]
    public async Task Chapter_ReturnsTheUnits_And404WhenTheChapterHasNone()
    {
        var endpoint = Create<ChapterEndpoint>();
        await endpoint.HandleAsync(new GetLxxReviewChapterRequest { BibleBookId = 8, Chapter = 1 }, CancellationToken.None);

        var unit = endpoint.Response.Units.Single();
        (unit.LxxVerseId, unit.Ref, unit.Open, unit.Proposal!.Id).ShouldBe((UnitId, "Rut.1:1", true, ProposalId));
        unit.Words.Single().Ai!.Strongs.ShouldBe("G2532");

        _repository.State = new LxxReviewState();
        var empty = Create<ChapterEndpoint>();
        await empty.HandleAsync(new GetLxxReviewChapterRequest { BibleBookId = 8, Chapter = 99 }, CancellationToken.None);
        empty.HttpContext.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Theory]
    [InlineData(0, 1, false)]
    [InlineData(40, 1, false)]
    [InlineData(8, 0, false)]
    [InlineData(8, 4, true)]
    public void ChapterValidator_ChecksBookAndChapter(int book, int chapter, bool valid) =>
        new ChapterValidator().Validate(new GetLxxReviewChapterRequest { BibleBookId = book, Chapter = chapter }).IsValid.ShouldBe(valid);

    [Fact]
    public async Task Lexicon_And_Morphology_Answer()
    {
        var lexicon = Create<LexiconEndpoint>();
        await lexicon.HandleAsync(new SearchLxxLexiconRequest { Q = "G2532" }, CancellationToken.None);
        lexicon.Response.Entries.Single().Lemma.ShouldBe("καί");

        var http = Create<MorphologyEndpoint>();
        await http.HandleAsync(new CheckLxxMorphologyRequest { Code = "v-2adi-3s" }, CancellationToken.None);
        http.Response.Valid.ShouldBeTrue();
        http.Response.Code.ShouldBe("V-2ADI-3S");
    }

    // ---- writes -----------------------------------------------------------------------------------------------------------------------

    private static ConfirmLxxUnitRequest ConfirmRequest(string strongs = "G2532", long? proposalId = ProposalId) => new()
    {
        LxxVerseId = UnitId,
        ProposalId = proposalId,
        Words = [new LxxReviewWordInput { LxxWordId = 1001, Lemma = "καί", Strongs = strongs, Morphology = "CONJ", HebrewWordIds = [501] }],
    };

    [Fact]
    public async Task Confirm_Answers200WithTheAuditActionAndWritesAsTheCaller()
    {
        var endpoint = Create<ConfirmEndpoint>(userId: 42);

        await endpoint.HandleAsync(ConfirmRequest(), CancellationToken.None);

        endpoint.HttpContext.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        (endpoint.Response.Action, endpoint.Response.ProposalStatus, endpoint.Response.ConfirmedWords).ShouldBe(("confirm", "merged", 1));
        _repository.WrittenBy.ShouldBe(42);
        _repository.Written!.Tags.Single().Source.ShouldBe("ai");
    }

    [Fact]
    public async Task Confirm_Answers400ForARuleFailure_And409ForAStaleProposal()
    {
        var invalid = Create<ConfirmEndpoint>(userId: 42);
        await invalid.HandleAsync(ConfirmRequest("G99999"), CancellationToken.None);
        invalid.HttpContext.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        invalid.ValidationFailures.Single().ErrorMessage.ShouldContain("G99999");

        var stale = Create<ConfirmEndpoint>(userId: 42);
        await stale.HandleAsync(ConfirmRequest(proposalId: 999), CancellationToken.None);
        stale.HttpContext.Response.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        _repository.Written.ShouldBeNull();
    }

    [Fact]
    public void ConfirmValidator_RefusesBadIds() =>
        new ConfirmValidator().Validate(new ConfirmLxxUnitRequest { LxxVerseId = 0, Words = [new LxxReviewWordInput { LxxWordId = 0 }] }).Errors.Count.ShouldBe(2);

    [Fact]
    public async Task Discard_Answers200_AndWritesTheRejectAudit()
    {
        var endpoint = Create<DiscardEndpoint>(userId: 42);

        await endpoint.HandleAsync(new DiscardLxxUnitRequest { LxxVerseId = UnitId, ProposalId = ProposalId }, CancellationToken.None);

        endpoint.Response.ProposalStatus.ShouldBe("discarded");
        _repository.Written!.Audits.Single().Action.ShouldBe("reject");

        var again = Create<DiscardEndpoint>(userId: 42);
        await again.HandleAsync(new DiscardLxxUnitRequest { LxxVerseId = UnitId, ProposalId = 1 }, CancellationToken.None);
        again.HttpContext.Response.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task ConfirmSafe_Answers200WithCounts()
    {
        var endpoint = Create<ConfirmSafeEndpoint>(userId: 42);

        await endpoint.HandleAsync(new ConfirmLxxSafeRequest
        {
            BibleBookId = 8, Chapter = 1,
            Units = [new LxxSafeUnitInput { LxxVerseId = UnitId, ProposalId = ProposalId, LxxWordIds = [1001] }],
        }, CancellationToken.None);

        (endpoint.Response.Units, endpoint.Response.ConfirmedWords, endpoint.Response.MergedProposals).ShouldBe((1, 1, 1));
        _repository.WrittenBy.ShouldBe(42);
    }

    [Fact]
    public void ConfirmSafeValidator_RefusesBadIds() =>
        new SafeValidator().Validate(new ConfirmLxxSafeRequest { BibleBookId = 0, Chapter = 1, Units = [new LxxSafeUnitInput { LxxVerseId = 1, ProposalId = 0 }] }).Errors.Count.ShouldBe(2);
}
