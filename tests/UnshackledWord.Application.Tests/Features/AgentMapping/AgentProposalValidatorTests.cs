using UnshackledWord.Application.Features.AgentMapping;
using static UnshackledWord.Application.Tests.Features.AgentMapping.AgentTestData;

namespace UnshackledWord.Application.Tests.Features.AgentMapping;

public class AgentProposalValidatorTests
{
    private const string CleanV1 = """{"v":"v1","links":[["g1","s1"],["g3","s3"],["g5","s5"],["g6","s5"]]}""";

    private static string V1(string extra, string links = """[["g1","s1"],["g3","s3"],["g5","s5"],["g6","s5"]]""") => $$"""{"v":"v1","links":{{links}},{{extra}}}""";

    private static AgentVerseValidation ValidateV1(string verse, AgentValidationContext? context = null) =>
        Verse(Validate(Answer(verse, GoodV2), context), "v1");

    // ---- binding and mapping, hard rules ---------------------------------------------------------------------------------------------

    [Fact]
    public void ACorrectAnswer_IsAccepted_AndTheProposalCarriesFixedHintsAndModelLinksInRealIds()
    {
        var results = Validate(Answer(CleanV1, GoodV2));

        var v1 = Verse(results, "v1");
        v1.Messages.ShouldBeEmpty();
        v1.Status.ShouldBe("accepted");
        var ids = v1.Proposal!.Links.Select(l => (l.ElbWordId, l.StepWordId)).ToList();
        ids.ShouldBe([(104, 204), (102, 202), (101, 201), (103, 203), (105, 205), (106, 205)], ignoreOrder: true);
        Verse(results, "v2").Status.ShouldBe("accepted");
    }

    [Fact]
    public void SourceIdOfTheNextVerse_IsRejected_WithTheDocumentedMessage()
    {
        var v1 = ValidateV1("""{"v":"v1","links":[["g1","s1"],["g3","s6"],["g5","s5"],["g6","s5"]]}""");

        v1.Status.ShouldBe("rejected");
        v1.Messages.ShouldContain("v1 g3: source s6 belongs to v2");
        v1.Proposal.ShouldBeNull();
    }

    [Fact]
    public void InventedIds_AreRejected()
    {
        var v1 = ValidateV1("""{"v":"v1","links":[["g1","s99"],["g99","s1"],["g7","s1"],["g3","s3"],["g5","s5"],["g6","s5"]]}""");

        v1.Messages.ShouldContain("v1 g1: source s99 is not in this package");
        v1.Messages.ShouldContain("v1 g99: german word g99 is not in this package");
        v1.Messages.ShouldContain("v1 g7: german word g7 belongs to v2");
    }

    [Fact]
    public void ChangingAFixedLink_IsRejected_ButRepeatingItIsFine()
    {
        var changed = ValidateV1("""{"v":"v1","links":[["g4","s1"],["g1","s1"],["g3","s3"],["g5","s5"],["g6","s5"]]}""");
        changed.Messages.ShouldContain("v1 g4: this word is fixed by the algorithm (g4→s4) and must not change");

        var repeated = ValidateV1("""{"v":"v1","links":[["g4","s4"],["g1","s1"],["g3","s3"],["g5","s5"],["g6","s5"]]}""");
        repeated.Messages.ShouldBeEmpty();
        repeated.Proposal!.Links.Count(l => l.ElbWordId == 104).ShouldBe(1, "the repeated fixed link is not stored twice");
    }

    [Fact]
    public void ARemovedFixedWord_CannotBeDroppedAsUnmapped()
    {
        var v1 = ValidateV1("""{"v":"v1","links":[["g1","s1"],["g3","s3"],["g5","s5"],["g6","s5"]],"unmapped":[["g4","skip"]]}""");

        v1.Messages.ShouldContain("v1 g4: this word is fixed by the algorithm (g4→s4) and must not change");
    }

    [Fact]
    public void AnOpenWordWithoutLinkAddedOrUnmapped_IsRejected()
    {
        var v1 = ValidateV1("""{"v":"v1","links":[["g1","s1"],["g5","s5"],["g6","s5"]]}""");

        v1.Messages.ShouldBe(["v1 g3: open word 'Evangeliums' has no link, added word or unmapped reason"]);
    }

    [Fact]
    public void AHintWordLeftOut_KeepsTheHint_AndAHintWordGivenAnotherLink_ReplacesIt()
    {
        var kept = ValidateV1(CleanV1);
        kept.Proposal!.Links.ShouldContain(l => l.ElbWordId == 102 && l.StepWordId == 202);

        var replaced = ValidateV1("""{"v":"v1","links":[["g1","s1"],["g2","s3"],["g3","s3"],["g5","s5"],["g6","s5"]]}""");
        replaced.Proposal!.Links.Where(l => l.ElbWordId == 102).Select(l => l.StepWordId).ShouldBe([203]);
    }

    [Fact]
    public void AVerseMissingFromTheAnswer_IsNotJudged_AnUnknownVerseIsRejected()
    {
        var results = Validate(Answer(CleanV1, """{"v":"v9","links":[]}"""));

        results.Select(r => r.V).ShouldBe(["v1", "v9"]);
        Verse(results, "v9").Messages.ShouldBe(["v9: not a verse of this package."]);
    }

    [Fact]
    public void AVerseAnsweredTwice_IsRejected()
    {
        var results = Validate(Answer(CleanV1, CleanV1, GoodV2));

        Verse(results, "v1").Messages.ShouldHaveSingleItem().ShouldContain("answered 2 times");
    }

    [Fact]
    public void ABadlyShapedEntry_IsRejectedWithItsOwnMessage()
    {
        var v1 = ValidateV1("""{"v":"v1","links":[["g1"],"g3"]}""");

        v1.Messages.ShouldContain(m => m.StartsWith("v1: links entry [\"g1\"] must be [german, source]"));
        v1.Messages.ShouldContain(m => m.StartsWith("v1: links entry \"g3\" must be an array"));
    }

    [Fact]
    public void ALinkedAndAddedWord_IsRejected()
    {
        var v1 = ValidateV1("""{"v":"v1","links":[["g1","s1"],["g3","s3"],["g5","s5"],["g6","s5"]],"added":[["g1","g3"]]}""");

        v1.Messages.ShouldContain("v1 g1: a word cannot be both linked and added");
    }

    [Fact]
    public void AddedWord_NeedsAParentOfTheSameVerse_NotItself_NotAnAddedWord()
    {
        var otherVerse = ValidateV1("""{"v":"v1","links":[["g3","s3"],["g5","s5"],["g6","s5"]],"added":[["g1","g7"]]}""");
        otherVerse.Messages.ShouldContain("v1 g1: parent g7 belongs to v2");

        var self = ValidateV1("""{"v":"v1","links":[["g3","s3"],["g5","s5"],["g6","s5"]],"added":[["g1","g1"]]}""");
        self.Messages.ShouldContain(m => m.StartsWith("v1 g1:") && m.Contains("own parent"));

        var chain = ValidateV1("""{"v":"v1","links":[["g5","s5"],["g6","s5"]],"added":[["g1","g3"],["g3","g5"]]}""");
        chain.Messages.ShouldContain("v1 g1: parent g3 is itself an added word; chains are not allowed");
    }

    [Fact]
    public void ADuplicateLink_IsRejected_ByTheSharedSaveRules_InLocalIds()
    {
        var v1 = ValidateV1("""{"v":"v1","links":[["g1","s1"],["g1","s1"],["g3","s3"],["g5","s5"],["g6","s5"]]}""");

        v1.Messages.ShouldContain(m => m.StartsWith("v1 g1:") && m.Contains("duplicate (german, source) pair"));
        v1.Messages.ShouldNotContain(m => m.Contains("101") || m.Contains("201"), "no database ids in a message");
    }

    [Fact]
    public void Parts_AreAcceptedWhenTheySpellTheWord_AndRejectedWhenTheyDoNot()
    {
        var good = ValidateV1("""{"v":"v1","links":[["g1","s1"],["g5","s5"],["g6","s5"]],"parts":[["g3","s3",1,"Evangelium"],["g3","s1",2,"s"]]}""");
        good.Messages.ShouldBeEmpty();
        good.Proposal!.Links.Where(l => l.ElbWordId == 103).Select(l => (l.PartOrder, l.GermanWordPart)).ShouldBe([(1, "Evangelium"), (2, "s")], ignoreOrder: true);
        good.Proposal.Parts.Count.ShouldBe(2);

        var wrong = ValidateV1("""{"v":"v1","links":[["g1","s1"],["g5","s5"],["g6","s5"]],"parts":[["g3","s3",1,"Evangel"],["g3","s1",2,"ium"]]}""");
        wrong.Messages.ShouldContain(m => m.StartsWith("v1 g3:") && m.Contains("do not spell the word"));

        var withPlainLink = ValidateV1("""{"v":"v1","links":[["g1","s1"],["g3","s3"],["g5","s5"],["g6","s5"]],"parts":[["g3","s3",1,"Evangelium"],["g3","s1",2,"s"]]}""");
        withPlainLink.Messages.ShouldContain(m => m.StartsWith("v1 g3:") && m.Contains("no whole-word link"));
    }

    // ---- lemmas, hard rules -------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("""["g1",""]""", "lemma is empty")]
    [InlineData("""["g1","zwei Wörter"]""", "contains whitespace")]
    [InlineData("""["g1","Anfang!"]""", "contains punctuation")]
    [InlineData("""["g1","Anfangsanfangsanfangsanfangsanfangsanfangsanfang"]""", "longer than 40")]
    public void LemmaShape_IsChecked(string lemma, string expected)
    {
        var v1 = ValidateV1(V1($"\"lemmas\":[{lemma}]"));

        v1.Messages.ShouldHaveSingleItem().ShouldBe($"v1 g1: {v1.Messages[0].Split(": ", 2)[1]}");
        v1.Messages[0].ShouldContain(expected);
    }

    [Fact]
    public void Stts_MustExist_AndFitThePartOfSpeech()
    {
        ValidateV1(V1("""
            "lemmas":[["g1","Anfang","XX"]]
            """)).Messages.ShouldContain("v1 g1: Unknown STTS tag 'XX'");

        ValidateV1(V1("""
            "lemmas":[["g5","gehen","NN"]]
            """)).Messages.ShouldContain("v1 g5: STTS tag 'NN' does not fit the part of speech 'VERB'");

        var withPos = ValidateV1(V1("""
            "lemmas":[["g5","Gehen","NN","NOUN"]]
            """));
        withPos.Messages.ShouldBeEmpty("a given part of speech makes the tag fit");
        withPos.Proposal!.Lemmas.Single().ShouldSatisfyAllConditions(l => l.PartOfSpeech.ShouldBe("NOUN"), l => l.Stts.ShouldBe("NN"));

        ValidateV1(V1("""
            "lemmas":[["g5","gehen","VVFIN","BANANA"]]
            """)).Messages.ShouldContain("v1 g5: unknown part of speech 'BANANA'");
    }

    [Fact]
    public void Nouns_NeedACapital_OtherWordsDoNotGetOne()
    {
        ValidateV1(V1("""
            "lemmas":[["g1","anfang"]]
            """)).Messages.ShouldContain("v1 g1: lemma 'anfang' of a noun (NN) must start with a capital letter");

        ValidateV1(V1("""
            "lemmas":[["g5","Gehen"]]
            """)).Messages.ShouldContain("v1 g5: lemma 'Gehen' is capitalised but the word is not a noun or name (VVFIN)");
    }

    [Fact]
    public void AFixedLemma_CannotChange_ButCanBeRepeated()
    {
        ValidateV1(V1("""
            "lemmas":[["g4","Jesu"]]
            """)).Messages.ShouldContain("v1 g4: the lemma of this word was set by a person ('Jesus') and must not change");

        var repeated = ValidateV1(V1("""
            "lemmas":[["g4","Jesus"]]
            """));
        repeated.Messages.ShouldBeEmpty();
        repeated.Proposal!.Lemmas.ShouldBeEmpty();
    }

    [Fact]
    public void ConfirmingTheStoredLemma_IsNotAChange()
    {
        var v1 = ValidateV1(V1("""
            "lemmas":[["g1","Anfang"],["g5","gehen","VVFIN"]]
            """));

        v1.Messages.ShouldBeEmpty();
        v1.Proposal!.Lemmas.ShouldBeEmpty();
    }

    [Fact]
    public void SeparableVerb_FiniteVerbCarriesParticleAndVerb_ParticleKeepsItself()
    {
        var v1 = ValidateV1(V1("""
            "lemmas":[["g5","ausgehen"],["g6","aus"]]
            """));

        v1.Messages.ShouldBeEmpty();
        v1.Proposal!.Lemmas.Select(l => (l.ElbWordId, l.Lemma)).ShouldBe([(105, "ausgehen")]);
        v1.Flags.ShouldNotContain(f => f.Code == "lemma-form-mismatch", "ging/ausgehen is checked without the particle");
    }

    [Fact]
    public void SeparableParticle_WithTheFullVerbLemma_IsRejected_SinceTheParticleKeepsItself()
    {
        var v1 = ValidateV1(V1("""
            "lemmas":[["g5","ausgehen"],["g6","ausgehen"]]
            """));

        v1.Messages.ShouldContain(m => m.StartsWith("v1 g6: separable particle 'aus' keeps itself as lemma ('aus'), got 'ausgehen'"));
    }

    [Fact]
    public void SeparableParticle_AloneWithTheVerbLemma_IsRejected()
    {
        var v1 = ValidateV1(V1("""
            "lemmas":[["g6","ausgehen"]]
            """));

        v1.Messages.ShouldContain(m => m.StartsWith("v1 g6: separable particle 'aus' keeps itself as lemma"));
    }

    [Fact]
    public void LegacySpelling_AsLemma_IsRejected_WithTheModernHeadword()
    {
        var v1 = ValidateV1(V1("""
            "lemmas":[["g5","gieng"]]
            """));

        v1.Messages.ShouldContain(m => m.StartsWith("v1 g5: lemma 'gieng' is an 1871 spelling, use the modern headword 'gehen'"));
    }

    [Fact]
    public void NonVerbLemma_EndingWithTheNeighbouringParticle_IsRejected()
    {
        var v1 = ValidateV1(V1("""
            "lemmas":[["g5","Gangaus","NN","NOUN"]]
            """));

        v1.Messages.ShouldContain(m => m.StartsWith("v1 g5: lemma 'Gangaus' ends with the neighbouring particle 'aus'"));
    }

    [Fact]
    public void ARetaggedParticle_IsNotCheckedAsAParticle()
    {
        // 'aus' as a preposition (not PTKVZ): normal lemma rules, no verb needed
        var v1 = ValidateV1(V1("""
            "lemmas":[["g6","aus","APPR"]]
            """));

        v1.Messages.ShouldBeEmpty();
    }

    [Fact]
    public void ALemmaOfAnotherVerse_IsRejected()
    {
        ValidateV1(V1("""
            "lemmas":[["g7","Wort"]]
            """)).Messages.ShouldContain(m => m.StartsWith("v1 g7:"));
    }

    // ---- soft rules ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void SourceWordWithoutGermanLink_IsFlagged_ArticlesAreNot()
    {
        var v1 = ValidateV1("""{"v":"v1","links":[["g1","s1"],["g3","s3"]],"unmapped":[["g5","no counterpart"],["g6","particle"]]}""");

        v1.Status.ShouldBe("flagged");
        v1.Messages.ShouldBeEmpty();
        v1.Flags.Select(f => f.Code).ShouldContain("source-unlinked");
        v1.Flags.Single(f => f.Code == "source-unlinked").Message.ShouldBe("v1: no German word links to s5");
        v1.Flags.Count(f => f.Code == "unmapped").ShouldBe(2);
        v1.Proposal.ShouldNotBeNull("flagged verses are stored");
    }

    [Fact]
    public void MoreThanThreeGermanWordsOnOneSource_IsFlagged()
    {
        var v1 = ValidateV1("""{"v":"v1","links":[["g1","s1"],["g3","s1"],["g5","s1"],["g6","s1"]]}""");

        v1.Flags.ShouldContain(f => f.Code == "many-to-one" && f.Message == "v1 s1: 4 German words (g1, g3, g5, g6) share this source word");
    }

    [Fact]
    public void ALinkJumpingAcrossALongVerse_IsFlagged()
    {
        var map = new AgentPackageMap { Language = "greek", BookId = 41 };
        var verse = new AgentVerseMap { Ref = "Mk 1:3", Chapter = 1, Verse = 3 };
        for (var i = 1; i <= 15; i++)
        {
            verse.German[$"g{i}"] = new AgentGermanMap { Id = 100 + i, Text = "w" + i, Lemma = "w", Stts = "NN", Pos = "NOUN" };
            verse.Source[$"s{i}"] = new AgentSourceMap { Id = 200 + i, Strongs = "G" + i };
        }

        map.Verses["v1"] = verse;
        var links = string.Join(",", Enumerable.Range(1, 15).Select(i => $"[\"g{i}\",\"s{(i == 1 ? 14 : i == 14 ? 1 : i)}\"]"));

        var v1 = Verse(Validate(Answer($$"""{"v":"v1","links":[{{links}}]}"""), map: map), "v1");

        v1.Messages.ShouldBeEmpty();
        v1.Flags.ShouldContain(f => f.Code == "crossing-link" && f.Message.StartsWith("v1 g1: link to s14 jumps across the verse"));
    }

    [Fact]
    public void LinkContradictingConfirmedStatistics_IsFlagged()
    {
        var stats = new ConfirmedStats([new AgentConfirmedPairRow { Lemma = "anfang", Strongs = "G2098", Count = 4 }]);

        var v1 = ValidateV1(CleanV1, Context(stats: stats));

        v1.Flags.ShouldContain(f => f.Code == "strongs-statistics" && f.Message.StartsWith("v1 g1: 'Anfang' was confirmed with G2098 (s3), never with G0746 (s1)"));
    }

    [Fact]
    public void LemmaSoftRules_FlagUnknownMismatchingAndNonInfinitiveLemmas()
    {
        var unknown = ValidateV1(V1("""
            "lemmas":[["g1","Anbeginn"]]
            """));
        unknown.Status.ShouldBe("flagged");
        unknown.Flags.ShouldContain(f => f.Code == "lemma-unknown" && f.Message == "v1 g1: lemma 'Anbeginn' is not in the lexicon (new word, please check)");
        unknown.Proposal!.Lemmas.Single().Lemma.ShouldBe("Anbeginn");

        ValidateV1(V1("""
            "lemmas":[["g5","laufen"]]
            """)).Flags.ShouldContain(f => f.Code == "lemma-form-mismatch");

        ValidateV1(V1("""
            "lemmas":[["g5","gehe"]]
            """)).Flags.ShouldContain(f => f.Code == "verb-lemma-ending");
    }

    [Fact]
    public void IrregularPair_IsNotFlagged()
    {
        var v1 = ValidateV1(V1("""
            "lemmas":[["g5","gehen","VVINF"]]
            """));

        v1.Flags.ShouldNotContain(f => f.Code == "lemma-form-mismatch");
    }

    [Fact]
    public void LemmaDifferingFromAHumanChoiceForTheForm_IsFlagged()
    {
        var context = Context(known: ["Beginn", "Anfang"], human: form => form == "anfang" ? new HashSet<string> { "Anfang" } : new HashSet<string>());

        var v1 = ValidateV1(V1("""
            "lemmas":[["g1","Beginn"]]
            """), context);

        v1.Flags.ShouldContain(f => f.Code == "lemma-differs-from-human" && f.Message == "v1 g1: people chose 'Anfang' for the form 'Anfang' elsewhere");
    }

    [Fact]
    public void ALongNote_IsCut_AndFlagged()
    {
        var v1 = ValidateV1(V1($"\"note\":\"{new string('x', 250)}\""));

        v1.Proposal!.Note!.Length.ShouldBe(200);
        v1.Flags.ShouldContain(f => f.Code == "note-truncated");
    }
}
