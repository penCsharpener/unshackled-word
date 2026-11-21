using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Domain.Models.Vocab;

namespace UnshackledWord.Domain.Tests.Model.Vocab;

public class VocabMeaningSelectorTests
{
    private static readonly VocabOptions Options = new();

    private static VocabRenderingCandidate C(string key, int links, bool function = false, string? pos = null) =>
        new(key, char.ToUpperInvariant(key[0]) + key[1..], pos ?? (function ? "VAFIN" : null), links);

    private static VocabMeaningRow Row(int id, string german, int rank, string origin = "derived", bool pinned = false, bool accepted = true) =>
        new(id, german, german.ToLowerInvariant(), rank, 10, 50m, accepted, origin, pinned);

    [Fact]
    public void Derive_OrdersByCount_AndComputesShareOfTheTotal()
    {
        var result = VocabMeaningSelector.Derive([C("rede", 22), C("wort", 278)], Options, []);

        result.Select(r => r.German).ShouldBe(["Wort", "Rede"]);
        result.Select(r => r.Rank).ShouldBe([1, 2]);
        result[0].Share.ShouldBe(92.67m);
        result[0].Count.ShouldBe(278);
    }

    [Fact]
    public void Derive_DropsKeysBelowShareOrCount()
    {
        // total 100: "a" 94 %, "b" 4 links = 4 % (below 5 %), "c" 1 link = 1 %, "d" 1
        var result = VocabMeaningSelector.Derive([C("a", 94), C("b", 4), C("c", 1), C("d", 1)], Options, []);

        result.Select(r => r.GermanKey).ShouldBe(["a"]);
    }

    [Fact]
    public void Derive_NeedsAtLeastTwoLinks_UnlessItIsTheOnlyWayToGetAMeaning()
    {
        // a second key with 2 links passes, so the single-link key does not
        VocabMeaningSelector.Derive([C("einzig", 1), C("zwei", 2)], Options, []).Select(r => r.GermanKey).ShouldBe(["zwei"]);
    }

    [Fact]
    public void Derive_KeepsAtMostSixMeanings()
    {
        var keys = Enumerable.Range(0, 10).Select(i => C($"k{i}", 10)).ToList();

        var result = VocabMeaningSelector.Derive(keys, Options, []);

        result.Count.ShouldBe(6);
        result.Select(r => r.Rank).ShouldBe([1, 2, 3, 4, 5, 6]);
    }

    [Fact]
    public void Derive_DropsFunctionWords_WhenSomethingElseRemains()
    {
        var result = VocabMeaningSelector.Derive([C("haben", 100, true), C("und", 80, true), C("erschaffen", 30), C("schaffen", 10)], Options, []);

        result.Select(r => r.GermanKey).ShouldBe(["erschaffen", "schaffen"]);
        result[0].Share.ShouldBe(75m);
    }

    [Fact]
    public void Derive_FallsBackToFunctionWords_WhenTheyAreAllThereIs()
    {
        var result = VocabMeaningSelector.Derive([C("der", 500, true), C("die", 300, true)], Options, []);

        result.Select(r => r.GermanKey).ShouldBe(["der", "die"]);
    }

    [Fact]
    public void Derive_SkipsAGermanThatAManualOrPinnedRowAlreadyHas_AndLeavesTheirRankFree()
    {
        var kept = new[] { Row(1, "wort", 1, "manual"), Row(2, "Rede", 2, "derived", pinned: true) };

        var result = VocabMeaningSelector.Derive([C("wort", 50), C("sprache", 30), C("rede", 20)], Options, kept);

        result.Select(r => r.German).ShouldBe(["Sprache"]);
        result[0].Rank.ShouldBe(3);
    }

    [Fact]
    public void Plan_ManualAndPinnedRowsSurvive_AndOnlyUnpinnedDerivedRowsAreDeleted()
    {
        var existing = new[] { Row(1, "Manuell", 1, "manual"), Row(2, "Fest", 2, "derived", pinned: true), Row(3, "Alt", 3), Row(4, "Wort", 4) };

        var plan = VocabMeaningSelector.Plan(existing, [C("wort", 90), C("neu", 10)], Options);

        plan.DeleteIds.ShouldBe([3]);
        plan.Insert.Select(i => i.German).ShouldBe(["Neu"]);
        plan.Update.Single().Id.ShouldBe(4);
        plan.Update.Single().Meaning.Rank.ShouldBe(3);
        plan.AfterAccepted.ShouldBe(["Manuell", "Fest", "Wort", "Neu"]);
    }

    [Fact]
    public void Plan_WithNothingNew_HasNoWritesAndNoVersionBump()
    {
        var first = VocabMeaningSelector.Plan([], [C("wort", 278), C("rede", 22)], Options);
        var stored = first.Insert.Select((m, i) => new VocabMeaningRow(i + 1, m.German, m.GermanKey, m.Rank, m.Count, m.Share, true, "derived", false)).ToList();

        var second = VocabMeaningSelector.Plan(stored, [C("wort", 278), C("rede", 22)], Options);

        second.HasWrites.ShouldBeFalse();
        second.AcceptedSetChanged.ShouldBeFalse();
    }

    [Fact]
    public void Plan_ReorderingAlone_ChangesNoAcceptedSet_ButChangesTheTop()
    {
        var stored = new[] { Row(1, "Rede", 1), Row(2, "Wort", 2) };

        var plan = VocabMeaningSelector.Plan(stored, [C("wort", 278), C("rede", 22)], Options);

        plan.Update.Count.ShouldBe(2);
        plan.AcceptedSetChanged.ShouldBeFalse();
        plan.TopChanged.ShouldBeTrue();
        plan.OldTop.ShouldBe("Rede");
        plan.NewTop.ShouldBe("Wort");
    }

    [Fact]
    public void Plan_ANewOrRemovedMeaning_ChangesTheAcceptedSet()
    {
        var plan = VocabMeaningSelector.Plan([Row(1, "Wort", 1)], [C("wort", 90), C("rede", 10)], Options);

        plan.AcceptedSetChanged.ShouldBeTrue();
        plan.BeforeAccepted.ShouldBe(["Wort"]);
        plan.AfterAccepted.ShouldBe(["Wort", "Rede"]);
    }

    [Fact]
    public void Plan_TheFirstMeaningsOfAnEntry_BumpNoVersion()
    {
        var plan = VocabMeaningSelector.Plan([], [C("wort", 90)], Options);

        plan.AcceptedSetChanged.ShouldBeTrue();
        plan.BumpsVersion.ShouldBeFalse();
    }

    [Fact]
    public void Plan_AnEntryWithoutRenderings_ClearsItsDerivedRows()
    {
        var plan = VocabMeaningSelector.Plan([Row(1, "Alt", 1), Row(2, "Fest", 2, "manual")], [], Options);

        plan.DeleteIds.ShouldBe([1]);
        plan.AfterAccepted.ShouldBe(["Fest"]);
    }

    [Theory]
    [InlineData("G:N-M", VocabEntryClass.Content)]
    [InlineData("G:V", VocabEntryClass.Content)]
    [InlineData("N:N-M-P", VocabEntryClass.Content)]
    [InlineData("H:Adv", VocabEntryClass.Content)]
    [InlineData("G:ADV-T", VocabEntryClass.Content)]
    [InlineData("A / G:ADV", VocabEntryClass.Content)]
    [InlineData("", VocabEntryClass.Content)]
    [InlineData(null, VocabEntryClass.Content)]
    [InlineData("G:T", VocabEntryClass.Article)]
    [InlineData("G:CONJ", VocabEntryClass.Conjunction)]
    [InlineData("H:Conj", VocabEntryClass.Conjunction)]
    [InlineData("G:COND", VocabEntryClass.Conjunction)]
    [InlineData("G:PREP", VocabEntryClass.Preposition)]
    [InlineData("H:Prep+H:RelP", VocabEntryClass.Preposition)]
    [InlineData("G:PRT-N+G:CONJ", VocabEntryClass.Particle)]
    [InlineData("H:Neg", VocabEntryClass.Particle)]
    [InlineData("G:INJ", VocabEntryClass.Interjection)]
    [InlineData("H:Intj", VocabEntryClass.Interjection)]
    [InlineData("G:D", VocabEntryClass.Pronoun)]
    [InlineData("G:P-1", VocabEntryClass.Pronoun)]
    [InlineData("H:PerP-CS", VocabEntryClass.Pronoun)]
    [InlineData("H:DemP", VocabEntryClass.Pronoun)]
    [InlineData("Sp2m", VocabEntryClass.Pronoun)]
    [InlineData("Os3f", VocabEntryClass.Pronoun)]
    public void ClassifyEntry_MapsStepMorphologyToAWordClass(string? pos, VocabEntryClass expected)
    {
        VocabMeaningSelector.ClassifyEntry(pos).ShouldBe(expected);
    }

    [Fact]
    public void Derive_ForAConjunctionEntry_KeepsOnlyConjunctionRows()
    {
        var result = VocabMeaningSelector.Derive([C("und", 900, pos: "KON"), C("auch", 800, pos: "ADV"), C("aber", 50, pos: "KON")], Options, [], VocabEntryClass.Conjunction);

        result.Select(r => r.GermanKey).ShouldBe(["und", "aber"]);
    }

    [Fact]
    public void Derive_ForAnArticleEntry_UsesArtRows_AndFallsBackWhenThereAreNone()
    {
        VocabMeaningSelector.Derive([C("der", 500, pos: "ART"), C("wer", 100, pos: "PRELS")], Options, [], VocabEntryClass.Article)
            .Select(r => r.GermanKey).ShouldBe(["der"]);
        VocabMeaningSelector.Derive([C("wer", 100, pos: "PRELS")], Options, [], VocabEntryClass.Article)
            .Select(r => r.GermanKey).ShouldBe(["wer"]);
    }

    [Fact]
    public void Derive_ForAParticleEntry_TakesParticlesAndPrepositions_NotVerbPrefixes()
    {
        var result = VocabMeaningSelector.Derive([C("an", 100, pos: "PTKVZ"), C("mit", 80, pos: "APPR"), C("nicht", 50, pos: "PTKNEG"), C("sie", 300, pos: "PPER")], Options, [], VocabEntryClass.Particle);

        result.Select(r => r.GermanKey).ShouldBe(["mit", "nicht"]);
    }

    [Fact]
    public void Derive_LowerCasesNonNouns_AndKeepsNounCapitals_AndSumsTheKey()
    {
        var result = VocabMeaningSelector.Derive([new("lieben", "Lieben", "VVINF", 100), new("lieben", "Liebet", "VVIMP", 10), new("liebe", "Liebe", "NN", 20)], Options, []);

        result.Select(r => r.German).ShouldBe(["lieben", "Liebe"]);
        result[0].Count.ShouldBe(110);
    }

    [Fact]
    public void Derive_WhenNothingReachesTheThresholds_TakesTheTopKeyAsTheOneMeaning()
    {
        var result = VocabMeaningSelector.Derive([C("a", 1), C("b", 1)], Options, []);

        result.Select(r => r.GermanKey).ShouldBe(["a"]);
        result[0].Rank.ShouldBe(1);
    }
}
