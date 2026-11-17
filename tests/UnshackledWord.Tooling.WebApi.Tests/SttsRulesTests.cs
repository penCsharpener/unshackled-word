using UnshackledWord.Application.Features.Lemmas;
using UnshackledWord.Domain.Models.Grammar;
using UnshackledWord.Domain.WebApi.Lemmas;
using UnshackledWord.Tooling.WebApi.Endpoints.Lemmas;
using UnshackledWord.Tooling.WebApi.Endpoints.Lemmas.Stts;

namespace UnshackledWord.Tooling.WebApi.Tests;

public class SttsRulesTests
{
    private static readonly SttsCatalog Catalog = new(
    [
        new SttsTag { Tag = "NN", Upos = "NOUN", AllowedUpos = ["NOUN", "PROPN"] },
        new SttsTag { Tag = "NE", Upos = "PROPN", AllowedUpos = ["PROPN", "NOUN"] },
        new SttsTag { Tag = "VVFIN", Upos = "VERB", AllowedUpos = ["VERB", "AUX"] },
        new SttsTag { Tag = "VVINF", Upos = "VERB", AllowedUpos = ["VERB", "AUX"] },
    ]);

    private static LemmaEditFields Change(string pos, string? stts) => new() { Lemma = "x", PartOfSpeech = pos, Stts = stts };

    private static LemmaWordRow Stored(string pos, string? stts) => new() { WordId = 1, PartOfSpeech = pos, Stts = stts, HasMorphology = true };

    [Fact]
    public void Missing_stts_leaves_the_tag_untouched()
    {
        SttsRules.Check(Change("VERB", null), Stored("NOUN", "NN"), Catalog).ShouldBeNull();
    }

    [Fact]
    public void Unknown_tag_is_rejected()
    {
        SttsRules.Check(Change("NOUN", "ZZZ"), Stored("NOUN", "NN"), Catalog)!.ShouldContain("Unknown STTS tag");
    }

    [Fact]
    public void Edited_pair_that_is_not_allowed_is_rejected()
    {
        SttsRules.Check(Change("VERB", "NN"), Stored("VERB", "VVFIN"), Catalog)!.ShouldContain("does not fit");
    }

    [Fact]
    public void Row_without_morphology_counts_as_edited()
    {
        SttsRules.Check(Change("VERB", "NN"), null, Catalog).ShouldNotBeNull();
        SttsRules.Check(Change("NOUN", "NN"), null, Catalog).ShouldBeNull();
    }

    [Fact]
    public void Untouched_legacy_mismatch_is_accepted()
    {
        // stored pair VERB/NN is wrong but the request keeps both values
        SttsRules.Check(Change("VERB", "NN"), Stored("VERB", "NN"), Catalog).ShouldBeNull();
    }

    [Fact]
    public void Changing_the_part_of_speech_of_a_legacy_row_checks_the_pair_again()
    {
        SttsRules.Check(Change("ADV", "NN"), Stored("VERB", "NN"), Catalog).ShouldNotBeNull();
    }

    [Fact]
    public void Proposals_are_set_only_for_rows_whose_tag_does_not_fit()
    {
        var rows = new[]
        {
            new LemmaWordRow { WordId = 1, PartOfSpeech = "VERB", VerbForm = "Inf", Stts = "NN" },
            new LemmaWordRow { WordId = 2, PartOfSpeech = "NOUN", Stts = "NN" },
            new LemmaWordRow { WordId = 3, PartOfSpeech = "VERB", Stts = null },
        };

        SttsRules.AddProposals(rows, Catalog);

        rows[0].ProposedStts.ShouldBe("VVINF");
        rows[1].ProposedStts.ShouldBeNull();
        rows[2].ProposedStts.ShouldBeNull();
    }

    [Fact]
    public void Field_validator_rejects_a_tag_outside_the_list_and_accepts_a_known_one()
    {
        var validator = new LemmaEditFieldsValidator();

        validator.Validate(Change("NOUN", "ZZZ")).IsValid.ShouldBeFalse();
        validator.Validate(Change("NOUN", "NN")).IsValid.ShouldBeTrue();
        validator.Validate(Change("NOUN", null)).IsValid.ShouldBeTrue();
    }
}
