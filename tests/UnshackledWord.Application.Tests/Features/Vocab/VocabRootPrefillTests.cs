using UnshackledWord.Application.Features.Vocab;
using UnshackledWord.Domain.Models.Vocab;

namespace UnshackledWord.Application.Tests.Features.Vocab;

public class VocabRootPrefillTests
{
    private static VocabRootLexiconLemma Lemma(string key, string display, bool inVocab = true) =>
        new(key, VocabText.Normalize(display), display, inVocab);

    private static readonly VocabRootLexiconLemma[] Lexicon =
    [
        Lemma("G4198", "πορεύομαι"),
        new("G4198", "πορευω", "πορεύομαι", true), // the lexicon lemma of the same key, display is the vocab lemma
        Lemma("G1161", "δέ"),
        Lemma("G3140", "μαρτυρέω"),
        Lemma("G0630", "ἀπολύω"),
        Lemma("G3004", "λέγω"),
        Lemma("G2564", "καλέω"),
        Lemma("G5000", "ἄγω"),
        Lemma("G5001", "ἄγω", inVocab: false),
        Lemma("G5002", "ἀγάπη"),
        Lemma("G5003", "βάλλω"),
        Lemma("G5004", "δίδωμι"),
        Lemma("G5005", "ἁλίσγω"),
        Lemma("G5006", "ἐσθίω"),
    ];

    private static VocabRootGreekResult Resolve(string mainKey, string headword) =>
        VocabRootPrefill.ResolveGreek([new VocabRootGreekEntry(mainKey, $"<b>{headword} </b>, -ῶ <BR /> <b>to x</b>")], Lexicon);

    private static VocabRootGreekSplit Single(string headword)
    {
        var result = Resolve("G9999", headword);
        result.Unresolved.ShouldBeEmpty();
        result.Splits.Count.ShouldBe(1);

        return result.Splits[0];
    }

    [Fact]
    public void Headword_is_cut_at_the_first_comma_or_tag()
    {
        VocabRootPrefill.Headword("<b>ἐκ-πορεύω </b><BR /> [in LXX]").ShouldBe("ἐκ-πορεύω");
        VocabRootPrefill.Headword("<b>συν-επι-μαρτυρέω</b>, -ῶ").ShouldBe("συν-επι-μαρτυρέω");
        VocabRootPrefill.Headword("no tag").ShouldBeNull();
    }

    [Fact]
    public void Ekporeuomai_resolves_to_ek_plus_poreuomai_with_the_vocab_lemma()
    {
        var split = Single("ἐκ-πορεύω");

        split.RootKey.ShouldBe("G4198");
        split.RootLemma.ShouldBe("πορεύομαι");
        split.Prefixes.ShouldBe("ἐκ");
        split.PrefixKeys.ShouldBe(["G1537"]);
    }

    [Theory]
    [InlineData("ἐξ-άγω", "ἐκ", "G1537")]
    [InlineData("συμ-βάλλω", "σύν", "G4862")]
    [InlineData("καθ-άλλω", "κατά", "G2596")]
    [InlineData("ἀφ-ίημι", "ἀπό", "G0575")]
    [InlineData("ἀνθ-ίστημι", "ἀντί", "G0473")]
    [InlineData("ἐμ-βάλλω", "ἐν", "G1722")]
    public void Elided_and_assimilated_prefixes_map_to_the_preposition(string headword, string dictionary, string key)
    {
        var lexicon = new[] { Lemma("G7000", headword[(headword.IndexOf('-') + 1)..]) };
        var result = VocabRootPrefill.ResolveGreek([new VocabRootGreekEntry("G9999", $"<b>{headword}</b>")], lexicon);

        result.Splits.Count.ShouldBe(1);
        result.Splits[0].Prefixes.ShouldBe(dictionary);
        result.Splits[0].PrefixKeys.ShouldBe([key]);
        result.Splits[0].RootKey.ShouldBe("G7000");
    }

    [Fact]
    public void Two_prefix_compound_keeps_the_prefix_order()
    {
        var split = Single("συν-επι-μαρτυρέω");

        split.Prefixes.ShouldBe("σύν + ἐπί");
        split.PrefixKeys.ShouldBe(["G4862", "G1909"]);
        split.RootKey.ShouldBe("G3140");
        split.RootLemma.ShouldBe("μαρτυρέω");
    }

    [Fact]
    public void Part_that_is_no_preposition_leaves_the_entry_unresolved()
    {
        var result = Resolve("G9999", "ἀ-γάπη");

        result.Splits.ShouldBeEmpty();
        result.Unresolved.Count.ShouldBe(1);
        result.Unresolved[0].Reason.ShouldContain("no preposition");
    }

    [Fact]
    public void Root_that_is_not_in_the_lexicon_leaves_the_entry_unresolved()
    {
        var result = Resolve("G9999", "ἐκ-ξενόω");

        result.Splits.ShouldBeEmpty();
        result.Unresolved[0].Reason.ShouldContain("not in lexicon");
    }

    [Fact]
    public void Root_with_two_vocab_keys_is_ambiguous_but_one_vocab_key_wins_over_a_non_vocab_one()
    {
        // G5000 is in the vocab, G5001 is not: the vocab key wins
        Single("ἀπο-άγω").RootKey.ShouldBe("G5000");

        // two vocab keys with the same lemma: ambiguous
        var lexicon = new[] { Lemma("G6000", "φέρω"), Lemma("G6001", "φέρω") };
        var result = VocabRootPrefill.ResolveGreek([new VocabRootGreekEntry("G9999", "<b>ἐκ-φέρω</b>")], lexicon);
        result.Splits.ShouldBeEmpty();
        result.Unresolved[0].Reason.ShouldContain("ambiguous");
    }

    [Fact]
    public void Entry_without_a_hyphen_is_not_touched()
    {
        var result = VocabVocabless("<b>πορεύομαι</b>, -ομαι");

        result.Splits.ShouldBeEmpty();
        result.Unresolved.ShouldBeEmpty();
    }

    private static VocabRootGreekResult VocabVocabless(string lexicon) =>
        VocabRootPrefill.ResolveGreek([new VocabRootGreekEntry("G9999", lexicon)], Lexicon);

    [Fact]
    public void Root_equal_to_the_word_itself_is_unresolved()
    {
        var result = VocabRootPrefill.ResolveGreek([new VocabRootGreekEntry("G3140", "<b>ἐκ-μαρτυρέω</b>")], Lexicon);

        result.Splits.ShouldBeEmpty();
        result.Unresolved[0].Reason.ShouldContain("itself");
    }

    [Fact]
    public void Hebrew_consonants_drop_niqqud_and_cantillation_and_keep_final_forms()
    {
        VocabRootPrefill.Consonants("כָּתַב").ShouldBe("כתב");
        VocabRootPrefill.Consonants("דָּבָ֑ר").ShouldBe("דבר");
        VocabRootPrefill.Consonants("שָׁלוֹם").ShouldBe("שלום");
        VocabRootPrefill.Consonants("מֶ֫לֶךְ").ShouldBe("מלך");
    }

    [Fact]
    public void Hebrew_homograph_verbs_are_numbered_in_key_order()
    {
        var roots = VocabRootPrefill.ResolveHebrew(
        [
            new VocabRootHebrewVerb("H6030B", "עָנָה"),
            new VocabRootHebrewVerb("H3789", "כָּתַב"),
            new VocabRootHebrewVerb("H6030A", "עָנָה"),
        ]).ToDictionary(r => r.MainKey);

        roots["H3789"].Root.ShouldBe("כתב");
        roots["H3789"].Homograph.ShouldBe((short)1);
        roots["H6030A"].Homograph.ShouldBe((short)1);
        roots["H6030B"].Homograph.ShouldBe((short)2);
    }
}
