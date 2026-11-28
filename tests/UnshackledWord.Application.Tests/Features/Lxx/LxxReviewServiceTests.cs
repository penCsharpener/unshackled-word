using System.Text.Json;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.AgentMapping.Lxx;
using UnshackledWord.Application.Features.Lxx.Review;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Application.Tests.Features.Lxx;

/// <summary>The pure plans of the LXX review page (roadmap 0028.7f): source rule, audit action, proposal status, validation, discard and "sichere" confirm.</summary>
public class LxxReviewServiceTests
{
    private const int UnitId = 100;
    private const long ProposalId = 77;

    private static readonly LxxLexicon Lexicon = LxxLexicon.Build(
    [
        new LxxLexiconEntry("G2532", "καί", "and"),
        new LxxLexiconEntry("G1096", "γίνομαι", "to become"),
        new LxxLexiconEntry("G1722", "ἐν", "in"),
        new LxxLexiconEntry("G3056", "λόγος", "word"),
        new LxxLexiconEntry("G1487G", "εἰ", "if"),
        new LxxLexiconEntry("G1487M", "εἰ", "whether"),
    ]);

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    /// <summary>One unit with 4 words (1 καί, 2 ἐγένετο, 3 ἐν, 4 untagged), three Hebrew words 501..503.</summary>
    private static LxxAgentWordRow Word(int position, string greek, string? lemma = null, string? strongs = null, string? morph = null, string? status = null, string? source = null) =>
        new() { Id = UnitId * 10 + position, LxxVerseId = UnitId, Position = position, Greek = greek, TagLemma = lemma, TagStrongs = strongs, TagMorphology = morph, TagStatus = status, TagSource = source };

    private static LxxWordProposal Ai(int position, string lemma, string strongs, string morph, string confidence = "high") =>
        new() { LxxWordId = UnitId * 10 + position, Position = position, Lemma = lemma, Strongs = strongs, Morph = morph, Confidence = confidence };

    private static LxxReviewProposalRow ProposalRow(IEnumerable<LxxWordProposal> words, IEnumerable<(int Position, int Hebrew)>? links = null, long id = ProposalId) => new()
    {
        Id = id,
        RunId = 5,
        LxxVerseId = UnitId,
        LemmasJson = JsonSerializer.Serialize(words, Web),
        LinksJson = JsonSerializer.Serialize((links ?? []).Select(l => new LxxLinkProposal { LxxWordId = UnitId * 10 + l.Position, Position = l.Position, StepHebrewWordId = l.Hebrew, HebNumber = l.Hebrew - 500 }), Web),
        FlagsJson = JsonSerializer.Serialize(new LxxProposalFlags { LxxVerseId = UnitId, Flags = [new LxxWordFlag("no-strongs", "no TBESG entry", 4)] }, Web),
        Note = "note",
        CreatedAt = new DateTime(2026, 11, 26, 12, 0, 0, DateTimeKind.Utc),
    };

    private static LxxReviewState State(IEnumerable<LxxAgentWordRow> words, LxxReviewProposalRow? proposal = null, IEnumerable<LxxReviewLinkRow>? links = null) => new()
    {
        Units = [new LxxAgentUnitRow { Id = UnitId, BibleBookId = 8, Chapter = 1, Verse = 1, MapKind = "one", SortOrder = 1, HebRefId = 8_001_001 }],
        Words = [.. words],
        HebrewWords =
        [
            new AgentSourceWordRow { Id = 501, RefId = 8_001_001, Position = 1, Text = "וַיְהִי", Strongs = "H1961" },
            new AgentSourceWordRow { Id = 502, RefId = 8_001_001, Position = 2, Text = "בִּימֵי", Strongs = "H3117" },
            new AgentSourceWordRow { Id = 503, RefId = 8_001_001, Position = 3, Text = "שְׁפֹט", Strongs = "H8199" },
        ],
        Links = [.. links ?? []],
        Proposals = proposal is null ? [] : [proposal],
    };

    private static List<LxxAgentWordRow> PlainWords() =>
    [
        Word(1, "ΚΑΙ"),
        Word(2, "ἐγένετο", "ἔπω, ἐρῶ, εἶπον", "G1096", "V-2ADI-3S", "proposed", "nt-form"),
        Word(3, "ἐν", "ἐν", "G1722", "PREP", "proposed", "nt-form"),
        Word(4, "Μωάβ"),
    ];

    private static LxxReviewWordInput In(int position, string? lemma, string? strongs, string? morph, params int[] hebrew) =>
        new() { LxxWordId = UnitId * 10 + position, Lemma = lemma, Strongs = strongs, Morphology = morph, HebrewWordIds = [.. hebrew] };

    private static ConfirmLxxUnitRequest Confirm(long? proposalId, params LxxReviewWordInput[] words) => new() { LxxVerseId = UnitId, ProposalId = proposalId, Words = [.. words] };

    private static List<string> Errors(LxxReviewPlan<AgentReviewResult<ConfirmLxxUnitResponse>> plan, AgentReviewOutcome outcome)
    {
        plan.Result.Outcome.ShouldBe(outcome);
        plan.Write.ShouldBeNull("a failed confirm writes nothing");
        return [.. plan.Result.Errors];
    }

    // ---- PlanConfirm: source rule, status, audit -------------------------------------------------------------------------------------

    [Fact]
    public void Confirm_TheAiAnswer_IsSourceAi_Merged_WithLinks_AndOneConfirmAudit()
    {
        var ai = new[] { Ai(1, "καί", "G2532", "CONJ"), Ai(2, "γίνομαι", "G1096", "V-2ADI-3S") };
        var state = State(PlainWords(), ProposalRow(ai, [(1, 501), (2, 502)]));

        var plan = LxxReviewService.PlanConfirm(Confirm(ProposalId,
            In(1, "καί", "G2532", "CONJ", 501), In(2, "γίνομαι", "G1096", "V-2ADI-3S", 502)), state, Lexicon);

        plan.Result.Outcome.ShouldBe(AgentReviewOutcome.Ok);
        plan.Result.Value!.Action.ShouldBe("confirm");
        plan.Result.Value.ProposalStatus.ShouldBe("merged");
        plan.Result.Value.ConfirmedWords.ShouldBe(2);
        var write = plan.Write.ShouldNotBeNull();
        write.Tags.Select(t => (t.LxxWordId, t.Strongs, t.Source)).ShouldBe([(1001, "G2532", "ai"), (1002, "G1096", "ai")]);
        write.LinkWordIds.ShouldBe([1001, 1002]);
        write.Links.ShouldBe([(1001, 501), (1002, 502)]);
        write.ProposalCloses.ShouldBe([new LxxProposalClose(ProposalId, "merged")]);
        var audit = write.Audits.Single();
        audit.Action.ShouldBe("confirm");
        audit.LxxVerseId.ShouldBe(UnitId);
        var before = JsonDocument.Parse(audit.BeforeJson!).RootElement;
        var after = JsonDocument.Parse(audit.AfterJson).RootElement;
        before.GetProperty("words").GetArrayLength().ShouldBe(2, "only the two nt-form proposals existed before");
        before.GetProperty("words")[0].GetProperty("status").GetString().ShouldBe("proposed");
        after.GetProperty("proposalId").GetInt64().ShouldBe(ProposalId);
        after.GetProperty("proposalStatus").GetString().ShouldBe("merged");
        after.GetProperty("words")[0].GetProperty("status").GetString().ShouldBe("confirmed");
        after.GetProperty("words")[0].GetProperty("hebrewWordIds")[0].GetInt32().ShouldBe(501);
    }

    [Fact]
    public void Confirm_AnAcceptedNtFormPrefill_KeepsSourceNtForm_AndWithoutProposalHasNoStatus()
    {
        var state = State(PlainWords());

        var plan = LxxReviewService.PlanConfirm(Confirm(null,
            In(2, "γίνομαι", "G1096", "V-2ADI-3S"), In(3, "ἐν", "G1722", "PREP")), state, Lexicon);

        plan.Result.Value!.ProposalStatus.ShouldBeNull();
        var write = plan.Write.ShouldNotBeNull();
        write.Tags.Select(t => (t.LxxWordId, t.Source)).ShouldBe([(1002, "nt-form"), (1003, "nt-form")]);
        write.Tags[0].Lemma.ShouldBe("γίνομαι", "the TBESG lemma, not STEP's list");
        write.ProposalCloses.ShouldBeEmpty();
        write.Audits.Single().Action.ShouldBe("confirm");
    }

    [Fact]
    public void Confirm_ADifferentAnswer_IsHuman_EditedMerged_ButStillAConfirmAudit()
    {
        var ai = new[] { Ai(1, "καί", "G2532", "CONJ") };
        var state = State(PlainWords(), ProposalRow(ai));

        var plan = LxxReviewService.PlanConfirm(Confirm(ProposalId, In(1, "λόγος", "G3056", "N-NSM")), state, Lexicon);

        plan.Result.Value!.ProposalStatus.ShouldBe("edited-merged");
        plan.Result.Value.Action.ShouldBe("confirm", "nothing was confirmed before, so it is no edit");
        plan.Write!.Tags.Single().Source.ShouldBe("human");
    }

    [Fact]
    public void Confirm_ASameTagWithAnotherHebrewLink_IsEditedMerged()
    {
        var ai = new[] { Ai(1, "καί", "G2532", "CONJ") };
        var state = State(PlainWords(), ProposalRow(ai, [(1, 501)]));

        var plan = LxxReviewService.PlanConfirm(Confirm(ProposalId, In(1, "καί", "G2532", "CONJ", 502)), state, Lexicon);

        plan.Result.Value!.ProposalStatus.ShouldBe("edited-merged");
        plan.Write!.Tags.Single().Source.ShouldBe("ai");
    }

    [Fact]
    public void Confirm_ChangingAConfirmedTag_IsAnEditAudit_AndKeepsTheOtherSourceRules()
    {
        var words = PlainWords();
        words[0] = Word(1, "ΚΑΙ", "καί", "G2532", "CONJ", "confirmed", "ai");
        var links = new[] { new LxxReviewLinkRow { LxxWordId = 1001, StepHebrewWordId = 501, Status = "confirmed" } };
        var state = State(words, links: links);

        var plan = LxxReviewService.PlanConfirm(Confirm(null, In(1, "λόγος", "G3056", "N-NSM", 501)), state, Lexicon);

        plan.Result.Value!.Action.ShouldBe("edit");
        plan.Write!.Tags.Single().Source.ShouldBe("human");
        plan.Write.Audits.Single().Action.ShouldBe("edit");
        plan.Write.Audits.Single().BeforeJson!.ShouldContain("G2532");
        plan.Write.Audits.Single().AfterJson.ShouldContain("G3056");
    }

    [Fact]
    public void Confirm_ChangingAConfirmedLink_IsAnEditAudit()
    {
        var words = PlainWords();
        words[0] = Word(1, "ΚΑΙ", "καί", "G2532", "CONJ", "confirmed", "human");
        var links = new[] { new LxxReviewLinkRow { LxxWordId = 1001, StepHebrewWordId = 501, Status = "confirmed" } };

        var plan = LxxReviewService.PlanConfirm(Confirm(null, In(1, "καί", "G2532", "CONJ", 502)), State(words, links: links), Lexicon);

        plan.Result.Value!.Action.ShouldBe("edit");
        plan.Write!.Tags.ShouldBeEmpty("the tag did not change");
        plan.Write.Links.ShouldBe([(1001, 502)]);
    }

    [Fact]
    public void Confirm_WhenNothingChanges_WritesNoAuditRow()
    {
        var words = PlainWords();
        words[0] = Word(1, "ΚΑΙ", "καί", "G2532", "CONJ", "confirmed", "human");
        var links = new[] { new LxxReviewLinkRow { LxxWordId = 1001, StepHebrewWordId = 501, Status = "confirmed" } };

        var plan = LxxReviewService.PlanConfirm(Confirm(null, In(1, "καί", "G2532", "CONJ", 501)), State(words, links: links), Lexicon);

        plan.Result.Outcome.ShouldBe(AgentReviewOutcome.Ok);
        plan.Result.Value!.Action.ShouldBeEmpty();
        plan.Write.ShouldBeNull();
    }

    [Fact]
    public void Confirm_ALinksOnlyWord_KeepsTheTagAsItIs()
    {
        var plan = LxxReviewService.PlanConfirm(Confirm(null, In(4, null, null, null, 503)), State(PlainWords()), Lexicon);

        plan.Result.Value!.ConfirmedWords.ShouldBe(0);
        var write = plan.Write.ShouldNotBeNull();
        write.Tags.ShouldBeEmpty();
        write.Links.ShouldBe([(1004, 503)]);
    }

    [Fact]
    public void Confirm_NormalizesStrongsAndCode_AndTakesTheTbesgLemma()
    {
        var plan = LxxReviewService.PlanConfirm(Confirm(null, In(1, "ΚΑΊ", "g2532", "conj")), State(PlainWords()), Lexicon);

        var tag = plan.Write!.Tags.Single();
        (tag.Lemma, tag.Strongs, tag.Morphology, tag.Source).ShouldBe(("καί", "G2532", "CONJ", "human"));
    }

    // ---- PlanConfirm: refusals --------------------------------------------------------------------------------------------------------

    [Fact]
    public void Confirm_UnknownStrongs_IsRefused()
    {
        var errors = Errors(LxxReviewService.PlanConfirm(Confirm(null, In(1, "καί", "G99999", "CONJ")), State(PlainWords()), Lexicon), AgentReviewOutcome.Invalid);

        errors.Single().ShouldContain("G99999");
        errors[0].ShouldContain("not in TBESG");
    }

    [Fact]
    public void Confirm_ALemmaOfAnotherEntry_IsRefused()
    {
        var errors = Errors(LxxReviewService.PlanConfirm(Confirm(null, In(1, "λόγος", "G2532", "CONJ")), State(PlainWords()), Lexicon), AgentReviewOutcome.Invalid);

        errors.Single().ShouldContain("not the TBESG lemma of G2532");
    }

    [Fact]
    public void Confirm_ABadParse_IsRefused()
    {
        var errors = Errors(LxxReviewService.PlanConfirm(Confirm(null, In(1, "καί", "G2532", "V-XYZ")), State(PlainWords()), Lexicon), AgentReviewOutcome.Invalid);

        errors.Single().ShouldContain("not a Robinson code");
    }

    [Fact]
    public void Confirm_AHebrewWordOutsideTheVerse_IsRefused()
    {
        var errors = Errors(LxxReviewService.PlanConfirm(Confirm(null, In(1, "καί", "G2532", "CONJ", 999)), State(PlainWords()), Lexicon), AgentReviewOutcome.Invalid);

        errors.Single().ShouldContain("Hebrew word 999 is not in the verse's Hebrew text");
    }

    [Fact]
    public void Confirm_AHebrewLinkInAnAddition_IsRefused()
    {
        var state = State(PlainWords());
        var addition = new LxxReviewState
        {
            Units = [new LxxAgentUnitRow { Id = UnitId, BibleBookId = 18, Chapter = 36, Verse = 28, Subverse = 1, MapKind = "addition", SortOrder = 1 }],
            Words = state.Words,
        };

        var errors = Errors(LxxReviewService.PlanConfirm(Confirm(null, In(1, "καί", "G2532", "CONJ", 501)), addition, Lexicon), AgentReviewOutcome.Invalid);

        errors.Single().ShouldContain("no Hebrew words");
    }

    [Fact]
    public void Confirm_PartialTagFields_AWordOfAnotherUnit_AndAllErrorsTogether()
    {
        var errors = Errors(LxxReviewService.PlanConfirm(Confirm(null,
            In(1, "καί", null, "CONJ"),
            new LxxReviewWordInput { LxxWordId = 4242, Lemma = "καί", Strongs = "G2532", Morphology = "CONJ" },
            In(3, "ἐν", "G1722", "PREP")), State(PlainWords()), Lexicon), AgentReviewOutcome.Invalid);

        errors.Count.ShouldBe(2, "the valid third word adds no error");
        errors[0].ShouldContain("needed together");
        errors[1].ShouldContain("4242");
    }

    [Fact]
    public void Confirm_UnknownUnit_Is404_AndStaleProposalIs409()
    {
        var state = State(PlainWords(), ProposalRow([Ai(1, "καί", "G2532", "CONJ")]));

        var unknown = LxxReviewService.PlanConfirm(new ConfirmLxxUnitRequest { LxxVerseId = 5 }, state, Lexicon);
        var otherId = LxxReviewService.PlanConfirm(Confirm(ProposalId + 1), state, Lexicon);
        var gone = LxxReviewService.PlanConfirm(Confirm(ProposalId), State(PlainWords()), Lexicon);

        unknown.Result.Outcome.ShouldBe(AgentReviewOutcome.NotFound);
        otherId.Result.Outcome.ShouldBe(AgentReviewOutcome.Stale);
        gone.Result.Outcome.ShouldBe(AgentReviewOutcome.Stale);
        new[] { unknown, otherId, gone }.ShouldAllBe(p => p.Write == null);
    }

    // ---- PlanDiscard ------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Discard_ClosesOnlyTheAiProposal_KeepsNtFormTags_AndAuditsReject()
    {
        var state = State(PlainWords(), ProposalRow([Ai(1, "καί", "G2532", "CONJ")], [(1, 501)]));

        var plan = LxxReviewService.PlanDiscard(new DiscardLxxUnitRequest { LxxVerseId = UnitId, ProposalId = ProposalId }, state);

        plan.Result.Outcome.ShouldBe(AgentReviewOutcome.Ok);
        plan.Result.Value!.ProposalStatus.ShouldBe("discarded");
        var write = plan.Write.ShouldNotBeNull();
        write.Tags.ShouldBeEmpty();
        write.Links.ShouldBeEmpty();
        write.ProposalCloses.ShouldBe([new LxxProposalClose(ProposalId, "discarded")]);
        var audit = write.Audits.Single();
        audit.Action.ShouldBe("reject");
        var after = JsonDocument.Parse(audit.AfterJson).RootElement;
        after.GetProperty("proposalStatus").GetString().ShouldBe("discarded");
        after.GetProperty("words").GetArrayLength().ShouldBe(2, "the nt-form proposals are still in the unit");
        after.GetProperty("discarded")[0].GetProperty("strongs").GetString().ShouldBe("G2532");
        audit.BeforeJson.ShouldNotBeNull();
    }

    [Fact]
    public void Discard_WithoutTheShownProposal_IsNotOpen_AndAnUnknownUnitIsNotFound()
    {
        var state = State(PlainWords(), ProposalRow([Ai(1, "καί", "G2532", "CONJ")]));

        var wrong = LxxReviewService.PlanDiscard(new DiscardLxxUnitRequest { LxxVerseId = UnitId, ProposalId = 1 }, state);
        var none = LxxReviewService.PlanDiscard(new DiscardLxxUnitRequest { LxxVerseId = UnitId, ProposalId = ProposalId }, State(PlainWords()));
        var unknown = LxxReviewService.PlanDiscard(new DiscardLxxUnitRequest { LxxVerseId = 9, ProposalId = ProposalId }, state);

        wrong.Result.Outcome.ShouldBe(AgentReviewOutcome.NotOpen);
        none.Result.Outcome.ShouldBe(AgentReviewOutcome.NotOpen);
        unknown.Result.Outcome.ShouldBe(AgentReviewOutcome.NotFound);
        new[] { wrong.Write, none.Write, unknown.Write }.ShouldAllBe(w => w == null);
    }

    // ---- PlanSafe ---------------------------------------------------------------------------------------------------------------------

    private static ConfirmLxxSafeRequest Safe(long proposalId, params int[] positions) => new()
    {
        BibleBookId = 8,
        Chapter = 1,
        Units = [new LxxSafeUnitInput { LxxVerseId = UnitId, ProposalId = proposalId, LxxWordIds = positions.Select(p => UnitId * 10 + p).ToList() }],
    };

    [Fact]
    public void Safe_ConfirmsOnlyListedHighAiWords_NeverLow_NeverNtFormAlone_AndSkipsConfirmed()
    {
        var words = PlainWords();
        words.Add(Word(5, "λόγος", "λόγος", "G3056", "N-NSM", "confirmed", "human"));
        var ai = new[]
        {
            Ai(1, "καί", "G2532", "CONJ"),
            Ai(2, "γίνομαι", "G1096", "V-2ADI-3S", "low"),
            Ai(5, "λόγος", "G3056", "N-NSM"),
            Ai(4, "καί", "G2532", "CONJ"),
        };
        var state = State(words, ProposalRow(ai, [(1, 501)]));

        // Listed: 1 (high), 2 (low), 3 (only an nt-form prefill, no AI answer), 5 (high but already confirmed). 4 is high but not listed.
        var plan = LxxReviewService.PlanSafe(Safe(ProposalId, 1, 2, 3, 5), state, Lexicon);

        var response = plan.Result.Value!;
        (response.Units, response.ConfirmedWords, response.SkippedLow, response.SkippedOther, response.MergedProposals).ShouldBe((1, 1, 1, 2, 0));
        response.StaleUnits.ShouldBeEmpty();
        var write = plan.Write.ShouldNotBeNull();
        write.Tags.Select(t => (t.LxxWordId, t.Source)).ShouldBe([(1001, "ai")]);
        write.Links.ShouldBe([(1001, 501)]);
        write.ProposalCloses.ShouldBeEmpty("words 2 and 4 are still open");
        write.Audits.Single().Action.ShouldBe("confirm");
    }

    [Fact]
    public void Safe_ClosesTheProposalAsMerged_WhenEveryWordOfItIsConfirmed()
    {
        var ai = new[] { Ai(1, "καί", "G2532", "CONJ"), Ai(2, "γίνομαι", "G1096", "V-2ADI-3S") };

        var plan = LxxReviewService.PlanSafe(Safe(ProposalId, 1, 2), State(PlainWords(), ProposalRow(ai)), Lexicon);

        plan.Result.Value!.MergedProposals.ShouldBe(1);
        plan.Write!.ProposalCloses.ShouldBe([new LxxProposalClose(ProposalId, "merged")]);
    }

    [Fact]
    public void Safe_ALowWordOnly_WritesNothing()
    {
        var plan = LxxReviewService.PlanSafe(Safe(ProposalId, 1), State(PlainWords(), ProposalRow([Ai(1, "καί", "G2532", "CONJ", "low")])), Lexicon);

        plan.Write.ShouldBeNull();
        plan.Result.Value!.SkippedLow.ShouldBe(1);
        plan.Result.Value.Units.ShouldBe(0);
    }

    [Fact]
    public void Safe_AStaleProposalOrAnotherChapter_IsReportedAndSkipped()
    {
        var state = State(PlainWords(), ProposalRow([Ai(1, "καί", "G2532", "CONJ")]));

        var stale = LxxReviewService.PlanSafe(Safe(ProposalId + 1, 1), state, Lexicon);
        var chapter = LxxReviewService.PlanSafe(new ConfirmLxxSafeRequest { BibleBookId = 8, Chapter = 2, Units = Safe(ProposalId, 1).Units }, state, Lexicon);
        var noProposal = LxxReviewService.PlanSafe(Safe(ProposalId, 1), State(PlainWords()), Lexicon);

        foreach (var plan in new[] { stale, chapter, noProposal })
        {
            plan.Result.Value!.StaleUnits.ShouldBe([UnitId]);
            plan.Write.ShouldBeNull();
        }
    }

    [Fact]
    public void Safe_AnAiAnswerThatFailsTheRulesToday_IsSkipped()
    {
        var ai = new[] { Ai(1, "καί", "G2532", "CONJ"), Ai(2, "γίνομαι", "G1096", "V-BAD") };

        var plan = LxxReviewService.PlanSafe(Safe(ProposalId, 1, 2), State(PlainWords(), ProposalRow(ai)), Lexicon);

        plan.Result.Value!.ConfirmedWords.ShouldBe(1);
        plan.Result.Value.SkippedOther.ShouldBe(1);
    }

    // ---- reads ------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void BuildChapter_ShowsTagsInTbesgSpelling_Hebrew_AiAnswersAndFlags()
    {
        var ai = new[] { Ai(1, "καί", "G2532", "CONJ") };
        var links = new[] { new LxxReviewLinkRow { LxxWordId = 1001, StepHebrewWordId = 501, Status = "confirmed" } };
        var state = State(PlainWords(), ProposalRow(ai, [(1, 501)]), links);

        var chapter = LxxReviewService.BuildChapter(8, 1, state, Lexicon);

        var unit = chapter.Units.Single();
        unit.Open.ShouldBeTrue();
        unit.HasConfirmed.ShouldBeFalse();
        unit.Hebrew.Select(h => h.Number).ShouldBe([1, 2, 3]);
        unit.Words[1].Tag!.Lemma.ShouldBe("γίνομαι");
        unit.Words[1].Tag!.Status.ShouldBe("proposed");
        unit.Words[0].Tag.ShouldBeNull();
        unit.Words[0].Ai!.Strongs.ShouldBe("G2532");
        unit.Words[0].Ai!.HebrewWordIds.ShouldBe([501]);
        unit.Words[0].LinksConfirmed.ShouldBeTrue();
        unit.Words[0].HebrewWordIds.ShouldBe([501]);
        unit.Proposal!.Id.ShouldBe(ProposalId);
        unit.Proposal.Flags.Single().Position.ShouldBe(4);
        unit.Proposal.CreatedAt.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void BuildChapter_AddsTheGermanVerseOfTheHebRefId_AndTheMappedWordIdsPerHebrewWord()
    {
        var state = State(PlainWords());
        state = new LxxReviewState
        {
            Units = state.Units,
            Words = state.Words,
            HebrewWords = state.HebrewWords,
            HebrewGerman = new Dictionary<int, string> { [501] = "Und es geschah" },
            HebrewGermanIds = new Dictionary<int, IReadOnlyList<int>> { [501] = [71, 72, 73], [502] = [] },
            GermanWords =
            [
                new LxxReviewGermanVerseWordRow { Id = 71, HebRefId = 8_001_001, Text = "Und" },
                new LxxReviewGermanVerseWordRow { Id = 72, HebRefId = 8_001_001, Text = "es" },
                new LxxReviewGermanVerseWordRow { Id = 73, HebRefId = 8_001_001, Text = "geschah," },
                new LxxReviewGermanVerseWordRow { Id = 99, HebRefId = 8_001_002, Text = "Fremd" },
            ],
        };

        var unit = LxxReviewService.BuildChapter(8, 1, state, Lexicon).Units.Single();

        unit.German.Select(g => g.Text).ShouldBe(["Und", "es", "geschah,"]);
        unit.Hebrew[0].German.ShouldBe("Und es geschah");
        unit.Hebrew[0].GermanWordIds.ShouldBe([71, 72, 73]);
        unit.Hebrew[1].GermanWordIds.ShouldBeNull();
        unit.Hebrew[2].GermanWordIds.ShouldBeNull();
    }

    [Fact]
    public void BuildChapter_DecodesTheHebrewMorphology_RootSegment()
    {
        var state = State(PlainWords());
        state = new LxxReviewState
        {
            Units = state.Units,
            Words = state.Words,
            HebrewWords = [new AgentSourceWordRow { Id = 501, RefId = 8_001_001, Position = 1, Text = "וַיְהִי", Strongs = "H1961", Morphology = "HC/Vqw3ms" }],
        };

        var hebrew = LxxReviewService.BuildChapter(8, 1, state, Lexicon).Units.Single().Hebrew.Single();

        hebrew.MorphologyDetail!.Code.ShouldBe("HC/Vqw3ms");
        hebrew.MorphologyDetail.PartOfSpeech.ShouldBe("verb");
        hebrew.MorphologyDetail.Person.ShouldNotBeNull();
    }

    [Fact]
    public void BuildChapter_AnAdditionWithoutHebRefId_HasNoGermanVerse()
    {
        var state = State(PlainWords());
        state = new LxxReviewState
        {
            Units = [new LxxAgentUnitRow { Id = UnitId, BibleBookId = 8, Chapter = 1, Verse = 1, MapKind = "addition", SortOrder = 1 }],
            Words = state.Words,
            GermanWords = [new LxxReviewGermanVerseWordRow { Id = 71, HebRefId = 8_001_001, Text = "Und" }],
        };

        var unit = LxxReviewService.BuildChapter(8, 1, state, Lexicon).Units.Single();

        unit.German.ShouldBeEmpty();
        unit.Hebrew.ShouldBeEmpty();
    }

    [Fact]
    public void BuildBooks_GroupsChaptersAndSumsCounts()
    {
        var books = LxxReviewService.BuildBooks(
        [
            new LxxReviewChapterCountRow { BibleBookId = 8, Chapter = 2, OpenUnits = 3, AiUnits = 1, LowWords = 2 },
            new LxxReviewChapterCountRow { BibleBookId = 8, Chapter = 1, OpenUnits = 5, AiUnits = 4, LowWords = 0 },
            new LxxReviewChapterCountRow { BibleBookId = 1, Chapter = 1, OpenUnits = 1 },
        ]);

        books.Books.Select(b => b.BibleBookId).ShouldBe([1, 8]);
        var ruth = books.Books[1];
        (ruth.OpenUnits, ruth.AiUnits, ruth.LowWords).ShouldBe((8, 5, 2));
        ruth.Chapters.Select(c => c.Chapter).ShouldBe([1, 2]);
    }

    [Fact]
    public void SearchLexicon_FindsByStrongsWithSiblings_AndByLemmaWithoutAccents()
    {
        LxxReviewService.SearchLexicon(Lexicon, "G1487", 20).Select(e => e.Strongs).ShouldBe(["G1487G", "G1487M"]);
        LxxReviewService.SearchLexicon(Lexicon, "1487m", 20).Select(e => e.Strongs).ShouldBe(["G1487M", "G1487G"]);
        LxxReviewService.SearchLexicon(Lexicon, "ΛΟΓ", 20).Select(e => e.Strongs).ShouldBe(["G3056"]);
        LxxReviewService.SearchLexicon(Lexicon, "γιν", 20).Select(e => e.Lemma).ShouldBe(["γίνομαι"]);
        LxxReviewService.SearchLexicon(Lexicon, "", 20).ShouldBeEmpty();
        LxxReviewService.SearchLexicon(Lexicon, "G1487", 1).Count.ShouldBe(1);
    }

    [Fact]
    public void CheckMorphology_NormalizesAndExplains()
    {
        var good = LxxReviewService.CheckMorphology(" v-2adi-3s ");
        var bad = LxxReviewService.CheckMorphology("V-XYZ");

        good.Valid.ShouldBeTrue();
        good.Code.ShouldBe("V-2ADI-3S");
        good.German.ShouldNotBeNullOrEmpty();
        bad.Valid.ShouldBeFalse();
        bad.Error.ShouldNotBeNullOrEmpty();
        LxxReviewService.CheckMorphology(null).Valid.ShouldBeFalse();
    }
}
