using UnshackledWord.Application.Features.AgentMapping;
using static UnshackledWord.Application.Tests.Features.AgentMapping.AgentTestData;

namespace UnshackledWord.Application.Tests.Features.AgentMapping;

public class AgentDeterministicPassTests
{
    [Fact]
    public void PunctuationOnlyTokens_AreRemovedAndCounted()
    {
        var draft = Pass(
            [German(1, "Anfang"), German(2, ""), German(3, "—", text: "—"), German(4, "Gott")],
            [Source(201, "G1")]);

        draft.German.Select(g => g.Row.Id).ShouldBe([1, 4]);
        draft.RemovedPunctuation.ShouldBe(2);
    }

    [Fact]
    public void ExistingLinks_AreFixed_InFillOpen_AndHints_InRedo()
    {
        var german = new[] { German(1, "Anfang"), German(2, "Gott") };
        var source = new[] { Source(201, "G1"), Source(202, "G2") };
        var mappings = new[] { Mapping(1, 201, author: 7), Mapping(2, 202) };

        var fillOpen = Pass(german, source, mappings, mode: AgentMode.FillOpen);
        fillOpen.German[0].Status.ShouldBe(AgentTokenStatus.Fixed);
        fillOpen.German[1].Status.ShouldBe(AgentTokenStatus.Fixed, "in fill-open every existing link is context, confirmed or not");

        var redo = Pass(german, source, mappings, mode: AgentMode.Redo);
        redo.German[0].Status.ShouldBe(AgentTokenStatus.Hint);
        redo.Links.Single(l => l.GermanId == 1).Fixed.ShouldBeFalse();
        redo.German[1].Status.ShouldBe(AgentTokenStatus.Hint, "a seed link without author is a hint in redo, never fixed");
        redo.Links.ShouldAllBe(l => !l.Fixed);
    }

    [Fact]
    public void LinkOfAnAiUser_IsAHint_InRedo()
    {
        var draft = Pass([German(1, "Anfang")], [Source(201, "G1")], [Mapping(1, 201, author: 9, ai: true)], mode: AgentMode.Redo);

        draft.German[0].Status.ShouldBe(AgentTokenStatus.Hint);
        draft.Links.Single().Fixed.ShouldBeFalse();
    }

    [Fact]
    public void SeedAddedWord_IsAHint_InRedo_AndFixed_InFillOpen()
    {
        var german = new[] { German(1, "der"), German(2, "Gott") };
        var source = new[] { Source(201, "G1") };
        var mappings = new[] { Mapping(2, 201), Mapping(1, null, parent: 2) };

        var redo = Pass(german, source, mappings, mode: AgentMode.Redo);
        redo.German.ShouldAllBe(g => g.Status == AgentTokenStatus.Hint);
        redo.Links.ShouldAllBe(l => !l.Fixed);
        redo.Links.Single(l => l.GermanId == 1).ParentId.ShouldBe(2);

        Pass(german, source, mappings, mode: AgentMode.FillOpen).German.ShouldAllBe(g => g.Status == AgentTokenStatus.Fixed);
    }

    [Fact]
    public void LinksToWordsOutsideTheVerse_AreIgnored()
    {
        var draft = Pass([German(1, "Anfang")], [Source(201, "G1")], [Mapping(1, 999, author: 7), Mapping(1, null, author: 7, parent: 555)], mode: AgentMode.FillOpen);

        draft.German[0].Status.ShouldBe(AgentTokenStatus.Open);
        draft.Links.ShouldBeEmpty();
    }

    [Fact]
    public void ProperName_WithConfirmedPairAndOneCandidate_IsFixed()
    {
        var german = new[] { German(1, "Jesu", lemma: "Jesus", stts: "NE"), German(2, "sprach", lemma: "sprechen", stts: "VVFIN") };
        var source = new[] { Source(201, "G2424G", "N-GSM-P"), Source(202, "G3004", "V-AAI-3S") };
        var stats = new ConfirmedStats([new AgentConfirmedPairRow { Lemma = "jesus", Strongs = "G2424G", Count = 2 }]);

        var draft = Pass(german, source, stats: stats);

        draft.German[0].Status.ShouldBe(AgentTokenStatus.Fixed);
        draft.Links.Single().SourceId.ShouldBe(201);
        draft.German[1].Status.ShouldBe(AgentTokenStatus.Open);
    }

    [Fact]
    public void ProperName_WithoutConfirmedPair_StaysOpen()
    {
        var draft = Pass([German(1, "Jesu", lemma: "Jesus", stts: "NE")], [Source(201, "G2424G", "N-GSM-P")]);

        draft.German[0].Status.ShouldBe(AgentTokenStatus.Open);
    }

    [Fact]
    public void ProperName_WithTwoCandidates_StaysOpen()
    {
        var stats = new ConfirmedStats(
        [
            new AgentConfirmedPairRow { Lemma = "jesus", Strongs = "G2424G", Count = 2 },
            new AgentConfirmedPairRow { Lemma = "jesus", Strongs = "G2424H", Count = 1 },
        ]);

        var draft = Pass([German(1, "Jesu", lemma: "Jesus", stts: "NE")], [Source(201, "G2424G", "N-GSM-P"), Source(202, "G2424H", "N-GSM-P")], stats: stats);

        draft.German[0].Status.ShouldBe(AgentTokenStatus.Open);
    }

    [Fact]
    public void Statistics_SingleCandidateWithKConfirmations_IsAHint()
    {
        var german = new[] { German(1, "Haus", lemma: "Haus", stts: "NN") };
        var source = new[] { Source(201, "G3624"), Source(202, "G1") };
        var stats = new ConfirmedStats([new AgentConfirmedPairRow { Lemma = "haus", Strongs = "G3624", Count = 3 }]);

        var draft = Pass(german, source, stats: stats);

        draft.German[0].Status.ShouldBe(AgentTokenStatus.Hint);
        draft.Links.Single().SourceId.ShouldBe(201);
        draft.Links.Single().Fixed.ShouldBeFalse();
    }

    [Theory]
    [InlineData(2, "G1")] // below K
    [InlineData(5, "G9")] // a competitor with confirmations exists in the verse
    public void Statistics_BelowKOrWithCompetitor_StaysOpen(int count, string competitor)
    {
        var rows = new List<AgentConfirmedPairRow> { new() { Lemma = "haus", Strongs = "G3624", Count = count } };
        if (competitor == "G9")
        {
            rows.Add(new AgentConfirmedPairRow { Lemma = "haus", Strongs = "G9", Count = 1 });
        }

        var draft = Pass([German(1, "Haus", lemma: "Haus", stts: "NN")], [Source(201, "G3624"), Source(202, "G9")], stats: new ConfirmedStats(rows));

        draft.German[0].Status.ShouldBe(AgentTokenStatus.Open);
    }

    [Fact]
    public void Article_BeforeNoun_WithoutSourceArticle_IsAnAddedWordHint()
    {
        var german = new[] { German(1, "die", stts: "ART"), German(2, "Liebe", lemma: "Liebe", stts: "NN") };

        var draft = Pass(german, [Source(201, "G26", "N-NSF")]);

        draft.German[0].Status.ShouldBe(AgentTokenStatus.Hint);
        var link = draft.Links.Single();
        (link.GermanId, link.SourceId, link.ParentId, link.Fixed).ShouldBe((1, null, 2, false));
    }

    [Fact]
    public void Article_WithSourceArticleInTheVerse_OrNotBeforeNoun_StaysOpen()
    {
        var withSourceArticle = Pass([German(1, "die", stts: "ART"), German(2, "Liebe", stts: "NN")], [Source(201, "G3588", "T-NSF"), Source(202, "G26", "N-NSF")]);
        withSourceArticle.German[0].Status.ShouldBe(AgentTokenStatus.Open);

        var beforeVerb = Pass([German(1, "die", stts: "ART"), German(2, "kamen", stts: "VVFIN")], [Source(201, "G26", "N-NSF")]);
        beforeVerb.German[0].Status.ShouldBe(AgentTokenStatus.Open);
    }

    [Fact]
    public void Article_Hebrew_UsesTheDefiniteArticleOfTheGrammarCode()
    {
        var german = new[] { German(1, "die", stts: "ART"), German(2, "Gerste", stts: "NN") };

        Pass(german, [Source(201, "H1", "HTd/Ncfsa")], hebrew: true).German[0].Status.ShouldBe(AgentTokenStatus.Open);
        Pass(german, [Source(201, "H1", "HNcfsa")], hebrew: true).German[0].Status.ShouldBe(AgentTokenStatus.Hint);
    }

    [Fact]
    public void Lemma_OfAHumanEditedWord_IsFixed()
    {
        var draft = Pass([German(1, "Jesu", lemma: "Jesus", stts: "NE", humanEdited: true)], [Source(201, "G1")]);

        draft.German[0].LemmaState.ShouldBe(AgentLemmaState.Fixed);
        draft.German[0].Lemma.ShouldBe("Jesus");
    }

    [Fact]
    public void Lemma_OfAFormWithOneTrustedLemma_IsAHintWithThatLemma()
    {
        var lexicon = Lexicon(FormLemma("thun", "tun", 3, trusted: true), FormLemma("thun", "thun", 5));

        var draft = Pass([German(1, "thun", lemma: "thun")], [Source(201, "G1")], lexicon: lexicon);

        draft.German[0].LemmaState.ShouldBe(AgentLemmaState.Hint);
        draft.German[0].Lemma.ShouldBe("tun");
    }

    [Fact]
    public void Lemma_OfAnAmbiguousOrUntrustedForm_HasNoStateAndListsCandidates()
    {
        var lexicon = Lexicon(FormLemma("sein", "sein", 90), FormLemma("sein", "seine", 10), FormLemma("sein", "sein", 1, trusted: true), FormLemma("sein", "seine", 1, trusted: true));

        var ambiguous = Pass([German(1, "sein", lemma: "sein")], [Source(201, "G1")], lexicon: lexicon).German[0];
        ambiguous.LemmaState.ShouldBeNull();
        ambiguous.Candidates.ShouldContain("seine");

        var lemmaSharpOnly = Pass([German(1, "Weg", lemma: "Weg")], [Source(201, "G1")], lexicon: Lexicon(FormLemma("weg", "Weg", 5), FormLemma("weg", "weg", 2))).German[0];
        lemmaSharpOnly.LemmaState.ShouldBeNull();
        lemmaSharpOnly.Lemma.ShouldBe("Weg");
        lemmaSharpOnly.Candidates.ShouldBe(["weg"]);
    }

    [Fact]
    public void Verse_WithEverythingFixed_IsFullyFixed()
    {
        var done = Pass([German(1, "Jesu", lemma: "Jesus", humanEdited: true)], [Source(201, "G1")], [Mapping(1, 201, author: 7)], mode: AgentMode.FillOpen);
        done.FullyFixed.ShouldBeTrue();

        var lemmaStillOpen = Pass([German(1, "Jesu", lemma: "Jesus")], [Source(201, "G1")], [Mapping(1, 201, author: 7)], mode: AgentMode.FillOpen);
        lemmaStillOpen.FullyFixed.ShouldBeFalse();
    }

    [Theory]
    [InlineData("T-GSN", false, true)]
    [InlineData("CONJ", false, true)]
    [InlineData("N-GSM-P", false, false)]
    [InlineData("HTd", true, true)]
    [InlineData("HTd/Ncmsa", true, false)]
    public void ArticlesAndParticles_AreExemptFromTheUnlinkedSourceFlag(string morphology, bool hebrew, bool exempt)
    {
        AgentDeterministicPass.IsArticleOrParticle(morphology, hebrew).ShouldBe(exempt);
    }
}
