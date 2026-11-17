using UnshackledWord.Application.Features.AgentMapping.Scoring;
using static UnshackledWord.Application.Tests.Features.AgentMapping.Scoring.ScoringTestData;

namespace UnshackledWord.Application.Tests.Features.AgentMapping.Scoring;

public class AgentScorerTests
{
    [Fact]
    public void Identical_State_ScoresFullAndMeetsTheBar()
    {
        var score = Score(Input(Reference()));

        score.Lemmas.Total.ShouldBe(4);
        score.Lemmas.Accuracy.ShouldBe(1.0);
        score.Stts.Accuracy.ShouldBe(1.0);
        score.RoleMorphemes.Accuracy.ShouldBe(1.0);
        score.Links.Precision.ShouldBe(1.0);
        score.Links.Recall.ShouldBe(1.0);
        score.AddedWords.Matched.ShouldBe(1);
        score.BulkMerge.Meets.ShouldBeTrue();
        score.PerVerse.Single().Status.ShouldBe("proposal");
        score.PerVerse.Single().Ref.ShouldBe("Mark 1:17");
    }

    [Fact]
    public void WrongLemma_IsListed_AndFailsTheBar()
    {
        var expected = Reference();
        var actual = AgentProposalState.Clone(expected);
        actual.Words.Single(w => w.Pos == 3).Lemma = "sagen";

        var score = Score(Input(expected, actual));

        score.Lemmas.Correct.ShouldBe(3);
        score.Lemmas.Accuracy.ShouldBe(0.75);
        score.PerVerse.Single().LemmaErrors.Single().ShouldSatisfyAllConditions(
            e => e.Item.ShouldBe("3"),
            e => e.Expected.ShouldBe("sprechen"),
            e => e.Actual.ShouldBe("sagen"));
        score.BulkMerge.Meets.ShouldBeFalse();
        score.BulkMerge.Checks.Single(c => c.Code == "lemmas").Ok.ShouldBeFalse();
        score.BulkMerge.Checks.Single(c => c.Code == "role-morphemes").Ok.ShouldBeTrue();
    }

    [Fact]
    public void WrongStts_CountsInSttsOnly()
    {
        var expected = Reference();
        var actual = AgentProposalState.Clone(expected);
        actual.Words.Single(w => w.Pos == 2).Stts = "NN";

        var score = Score(Input(expected, actual));

        score.Stts.Accuracy.ShouldBe(0.75);
        score.Lemmas.Accuracy.ShouldBe(1.0);
    }

    [Fact]
    public void WrongRole_LowersRoleAndRoleMorphemeAccuracy()
    {
        var expected = Reference();
        var actual = AgentProposalState.Clone(expected);
        actual.Links.Single(l => l.G == 2).Role = "unit";

        var score = Score(Input(expected, actual));

        score.Roles.Total.ShouldBe(3);
        score.Roles.Correct.ShouldBe(2);
        score.RoleMorphemes.Correct.ShouldBe(2);
        score.Morphemes.Total.ShouldBe(0);
        score.Morphemes.Accuracy.ShouldBeNull();
        score.PerVerse.Single().RoleErrors.Single().Expected.ShouldBe("head");
        score.PerVerse.Single().RoleErrors.Single().Actual.ShouldBe("unit");
        score.BulkMerge.Checks.Single(c => c.Code == "role-morphemes").Ok.ShouldBeFalse();
    }

    [Fact]
    public void WrongHebrewMorpheme_IsScoredSeparately_AndDifferenceInCaseIsIgnored()
    {
        var expected = new ReferenceVerse { Book = 8, Chapter = 2, Verse = 3, Language = "hebrew" };
        expected.Words.Add(new ReferenceWord { Pos = 1, Text = "Und", Lemma = "und" });
        expected.Words.Add(new ReferenceWord { Pos = 2, Text = "sprach", Lemma = "sprechen" });
        expected.Links.Add(new ReferenceLink { G = 1, SRef = 8002003, S = 1, Morpheme = "H9001" });
        expected.Links.Add(new ReferenceLink { G = 2, SRef = 8002003, S = 1, Morpheme = "H0559" });
        var actual = AgentProposalState.Clone(expected);
        actual.Links[0].Morpheme = "H0559";
        actual.Links[1].Morpheme = "h0559";

        var score = Score(Input(expected, actual));

        score.Morphemes.Total.ShouldBe(2);
        score.Morphemes.Correct.ShouldBe(1);
        score.Roles.Accuracy.ShouldBe(1.0);
        score.RoleMorphemes.Correct.ShouldBe(1);
    }

    [Fact]
    public void MissingLink_CountsAsWrongRole_AndLowersRecall()
    {
        var expected = Reference();
        var actual = AgentProposalState.Clone(expected);
        actual.Links.RemoveAll(l => l.G == 1);

        var score = Score(Input(expected, actual));

        score.Links.Recall.ShouldBe(Math.Round(2 / 3.0, 4));
        score.Links.Precision.ShouldBe(1.0);
        score.Roles.Correct.ShouldBe(2);
        score.PerVerse.Single().MissingLinks.Single().Item.ShouldBe("1→41001017.1");
    }

    [Fact]
    public void ExtraLinkAndExtraAddedWord_LowerPrecision()
    {
        var expected = Reference();
        var actual = AgentProposalState.Clone(expected);
        actual.Links.Add(new ReferenceLink { G = 4, SRef = 41001017, S = 3 });
        actual.Added.Add(new ReferenceAdded { G = 1, Parent = 2 });

        var score = Score(Input(expected, actual));

        score.Links.Precision.ShouldBe(0.75);
        score.Links.Recall.ShouldBe(1.0);
        score.AddedWords.Precision.ShouldBe(0.5);
        score.AddedWords.Recall.ShouldBe(1.0);
        score.PerVerse.Single().ExtraLinks.Count.ShouldBe(2);
    }

    [Fact]
    public void PartLemma_IsScoredOnItsOwn()
    {
        var expected = new ReferenceVerse { Book = 8, Chapter = 1, Verse = 2, Language = "hebrew" };
        expected.Words.Add(new ReferenceWord { Pos = 1, Text = "Gerstenernte", Lemma = "Gerstenernte" });
        expected.Links.Add(new ReferenceLink { G = 1, SRef = 8001002, S = 1, Part = 1, PartText = "Gersten", PartLemma = "Gerste" });
        expected.Links.Add(new ReferenceLink { G = 1, SRef = 8001002, S = 2, Part = 2, PartText = "ernte", PartLemma = "Ernte" });
        var actual = AgentProposalState.Clone(expected);
        actual.Links[0].PartLemma = "Gersten";

        var score = Score(Input(expected, actual));

        score.PartLemmas.Total.ShouldBe(2);
        score.PartLemmas.Correct.ShouldBe(1);
        score.Lemmas.Accuracy.ShouldBe(1.0);
    }

    [Theory]
    [InlineData(50, 1, true)]   // 98 % exactly
    [InlineData(50, 2, false)]  // 96 %
    [InlineData(200, 4, true)]
    [InlineData(200, 5, false)] // 97.5 %
    public void LemmaBar_UsesTheExactFraction(int words, int wrong, bool meets)
    {
        var score = Score(Words(words, wrong));

        score.BulkMerge.Checks.Single(c => c.Code == "lemmas").Ok.ShouldBe(meets);
        score.BulkMerge.Meets.ShouldBe(meets);
    }

    [Fact]
    public void RoleBar_97Percent_IsInclusive()
    {
        var verse = Words(100, 0);
        for (var i = 0; i < 3; i++)
        {
            verse.Actual.Links[i].Role = "unit";
        }

        Score(verse).BulkMerge.Meets.ShouldBeTrue();

        verse.Actual.Links[3].Role = "unit";
        var score = Score(verse);
        score.BulkMerge.Meets.ShouldBeFalse();
        score.BulkMerge.Reasons.ShouldContain(r => r.Contains("Role/morpheme"));
    }

    [Fact]
    public void VerseInPackageWithoutProposal_IsAHardRejection_ButASilentVerseIsNot()
    {
        var silent = Score(Input(Reference(), proposal: false, inPackage: false));
        silent.Verses.Silent.ShouldBe(1);
        silent.Verses.HardRejections.ShouldBe(0);
        silent.PerVerse.Single().Status.ShouldBe("silent");
        silent.BulkMerge.Meets.ShouldBeTrue();

        var rejected = Score(Input(Reference(), proposal: false, inPackage: true));
        rejected.Verses.HardRejections.ShouldBe(1);
        rejected.PerVerse.Single().Status.ShouldBe("no-proposal");
        rejected.BulkMerge.Meets.ShouldBeFalse();
        rejected.BulkMerge.Checks.Single(c => c.Code == "hard-rejections").Ok.ShouldBeFalse();
    }

    [Fact]
    public void NothingScored_NeverMeetsTheBar()
    {
        var score = Score();

        score.Lemmas.Accuracy.ShouldBeNull();
        score.BulkMerge.Meets.ShouldBeFalse();
        score.BulkMerge.Checks.Single(c => c.Code == "scored-verses").Ok.ShouldBeFalse();
    }

    [Fact]
    public void Flags_AreCountedPerCode_AndPerVerseWithProposal()
    {
        var score = Score(
            Input(Reference(), flags: ["homograph", "pronoun-added"]),
            Input(Reference(), flags: ["homograph"]),
            Input(Reference()),
            Input(Reference(), proposal: false, inPackage: false));

        score.Flags.Total.ShouldBe(3);
        score.Flags.VersesFlagged.ShouldBe(2);
        score.Flags.PerVerse.ShouldBe(1.0);
        score.Flags.ByCode["homograph"].ShouldBe(2);
        score.Flags.ByCode["pronoun-added"].ShouldBe(1);
    }

    [Fact]
    public void Cost_IsSummedOverVersesInPackages_AndAveragedPerVerse()
    {
        var a = new ScoredVerseInput { Expected = Reference(), Actual = Reference(), HasProposal = true, InPackage = true, InputTokens = 1000, CachedInputTokens = 400, OutputTokens = 100, CostUsd = 0.02m };
        var b = new ScoredVerseInput { Expected = Reference(), Actual = Reference(), HasProposal = true, InPackage = true, InputTokens = 3000, CachedInputTokens = 0, OutputTokens = 300, CostUsd = 0.06m };
        var silent = new ScoredVerseInput { Expected = Reference(), Actual = Reference(), HasProposal = false, InPackage = false };

        var score = Score(a, b, silent);

        score.Cost.InputTokens.ShouldBe(4000);
        score.Cost.CachedInputTokens.ShouldBe(400);
        score.Cost.OutputTokens.ShouldBe(400);
        score.Cost.CostUsd.ShouldBe(0.08m);
        score.Cost.Verses.ShouldBe(2);
        score.Cost.InputTokensPerVerse.ShouldBe(2000);
        score.Cost.CostUsdPerVerse.ShouldBe(0.04m);
    }
}
