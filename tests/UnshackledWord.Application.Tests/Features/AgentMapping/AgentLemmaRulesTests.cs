using UnshackledWord.Application.Features.AgentMapping;

namespace UnshackledWord.Application.Tests.Features.AgentMapping;

/// <summary>Checkable rules of docs/roadmaps/0018-german-lemmas/03-lemma-rules.md; every example row of the table.</summary>
public class AgentLemmaRulesTests
{
    [Theory]
    [InlineData("ward", "werden")]
    [InlineData("sahe", "sehen")]
    [InlineData("thut", "tun")]
    [InlineData("gieng", "gehen")]
    [InlineData("thun", "tun")]
    [InlineData("Thür", "Tür")]
    [InlineData("seyn", "sein")]
    public void LegacySpelling_IsRejected_WithTheModernHeadword(string lemma, string modern)
    {
        AgentLemmaRules.CheckLegacySpelling(lemma).ShouldNotBeNull().ShouldContain($"'{modern}'");
    }

    [Theory]
    [InlineData("werden")]
    [InlineData("sehen")]
    [InlineData("tun")]
    [InlineData("gehen")]
    [InlineData("Tür")]
    [InlineData("Leute")]
    [InlineData("Sohn")]
    public void ModernLemma_IsNoLegacySpelling(string lemma) => AgentLemmaRules.CheckLegacySpelling(lemma).ShouldBeNull();

    [Theory]
    [InlineData("zum", "zu")]
    [InlineData("im", "in")]
    [InlineData("vom", "von")]
    [InlineData("am", "an")]
    [InlineData("zur", "zu")]
    [InlineData("beim", "bei")]
    [InlineData("ins", "in")]
    public void ContractionHeadword_IsThePreposition(string form, string headword)
    {
        AgentLemmaRules.ContractionHeadword(form).ShouldBe(headword);
        AgentLemmaRules.ContractionHeadword(char.ToUpperInvariant(form[0]) + form[1..]).ShouldBe(headword);
        AgentLemmaRules.CheckContraction(form, headword, "APPRART").ShouldBeNull();
        AgentLemmaRules.CheckContraction(form, "der", "APPRART").ShouldNotBeNull();
        AgentLemmaRules.CheckContraction(form, "der", "NN").ShouldBeNull("only APPRART is checked");
    }

    [Theory]
    [InlineData("ihm", "er")]
    [InlineData("uns", "wir")]
    [InlineData("mir", "ich")]
    [InlineData("euch", "ihr")]
    public void PersonalPronounHeadword_IsTheNominative(string form, string headword) =>
        AgentLemmaRules.PersonalPronounHeadword(form).ShouldBe(headword);

    [Fact]
    public void NoHeadword_ForOtherForms()
    {
        AgentLemmaRules.ContractionHeadword("Haus").ShouldBeNull();
        AgentLemmaRules.PersonalPronounHeadword("Haus").ShouldBeNull();
    }

    [Theory]
    [InlineData("auf", "auf", "stand", "aufstehen", "VVFIN")]
    [InlineData("zurück", "zurück", "kehrte", "zurückkehren", "VVFIN")]
    [InlineData("Auf", "auf", "stand", "aufstehen", "VVFIN")]
    public void Particle_KeepingItself_WithAFiniteVerbPartner_IsAccepted(string form, string lemma, string verbForm, string verbLemma, string verbStts)
    {
        verbForm.ShouldNotBeEmpty();
        AgentLemmaRules.CheckParticle(form, lemma, [(verbLemma, verbStts)]).ShouldBeNull();
    }

    [Fact]
    public void Particle_WithTheFullVerbLemma_IsRejected() =>
        AgentLemmaRules.CheckParticle("auf", "aufstehen", [("aufstehen", "VVFIN")]).ShouldNotBeNull().ShouldContain("keeps itself");

    [Theory]
    [InlineData("stehen", "VVFIN")]
    [InlineData("aufstehen", "VVINF")]
    [InlineData("aufstehen", "VVPP")]
    [InlineData("auf", "VVFIN")]
    public void Particle_WithoutAFiniteVerbStartingWithIt_IsRejected(string verbLemma, string verbStts) =>
        AgentLemmaRules.CheckParticle("auf", "auf", [(verbLemma, verbStts)]).ShouldNotBeNull().ShouldContain("needs a finite verb");

    [Theory]
    [InlineData("Himmelauf", "NN")]
    [InlineData("Weg", "NN")]
    public void NonVerbLemma_EndingWithAParticle_IsChecked(string lemma, string stts)
    {
        var message = AgentLemmaRules.CheckAbsorbedParticle(lemma, stts, null, ["auf"]);
        (message is null).ShouldBe(!lemma.EndsWith("auf"));
    }

    [Fact]
    public void AbsorbedParticle_NotCheckedForVerbsOrTheParticleItself()
    {
        AgentLemmaRules.CheckAbsorbedParticle("aufstehen", "VVFIN", "VERB", ["stehen"]).ShouldBeNull();
        AgentLemmaRules.CheckAbsorbedParticle("zurückkehren", "VVFIN", null, ["zurück"]).ShouldBeNull();
        AgentLemmaRules.CheckAbsorbedParticle("auf", "PTKVZ", "ADP", ["auf"]).ShouldBeNull();
        AgentLemmaRules.CheckAbsorbedParticle("auf", "ADV", null, ["auf"]).ShouldBeNull("equal to the particle is no absorption");
    }

    [Fact]
    public void ContiguousSeparableVerb_HinausgegangenIsALemmaOfItsOwn() =>
        AgentLemmaRules.CheckWellFormed("hinausgehen").ShouldBeNull();

    [Fact]
    public void PeriphrasticTense_AndModal_EachWordItsOwnLemma()
    {
        // none of these may be flagged by the new rules
        foreach (var lemma in new[] { "haben", "taufen", "werden", "töten", "lassen", "wir", "gehen", "fürchten", "sich", "ich", "zu", "essen" })
        {
            AgentLemmaRules.CheckLegacySpelling(lemma).ShouldBeNull();
        }
    }
}
