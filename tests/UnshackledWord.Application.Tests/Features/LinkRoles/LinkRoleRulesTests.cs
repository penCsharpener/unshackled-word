using UnshackledWord.Application.Features.LinkRoles;
using static UnshackledWord.Application.Tests.Features.LinkRoles.LinkRoleFixtures;

namespace UnshackledWord.Application.Tests.Features.LinkRoles;

/// <summary>
/// Rules 1–6 of the link-role backfill on real verses. A verse name without suffix is the uw_0018_test state; "/before" turns an added
/// subject pronoun back into a link (the state rule 5 repairs); "/lemmas" applies the 0018.5 lemma fixes the rules 3 and 6 wait for.
/// </summary>
public class LinkRoleRulesTests
{
    private static LinkRoleVerse Verse(string name) => name switch
    {
        "Gen1v3" => LinkRoleVerseBuilder.Load(Gen1v3, Gen1v3Ref, SourceLanguage.Hebrew),
        "Gen1v3/before" => Verse("Gen1v3").Linked(34, 24).Linked(38, 26),
        "Mk1v8" => LinkRoleVerseBuilder.Load(Mk1v8, Mk1v8Ref, SourceLanguage.Greek),
        "Mk1v29" => LinkRoleVerseBuilder.Load(Mk1v29, Mk1v29Ref, SourceLanguage.Greek),
        "Mk1v29/before" => Verse("Mk1v29").Linked(575271, 19382, "G1831"),
        "Mk1v29/lemmas" => Verse("Mk1v29").Word(575272, w => w with { Lemma = "ausgehen" }),
        "Mk1v35" => LinkRoleVerseBuilder.Load(Mk1v35, Mk1v35Ref, SourceLanguage.Greek),
        "Mk1v35/before" => Verse("Mk1v35").Linked(575389, 19475, "G0450"),
        "Mk1v35/lemmas" => Verse("Mk1v35")
            .Word(575388, w => w with { Lemma = "aufstehen" })
            .Word(575392, w => w with { Lemma = "hinausgehen" })
            .Word(575393, w => w with { Stts = "PTKVZ", Lemma = "hinausgehen" }),
        "Rt1v5" => LinkRoleVerseBuilder.Load(Rt1v5, Rt1v5Ref, SourceLanguage.Hebrew),
        "Rt1v5/lemmas" => Verse("Rt1v5").Word(176174, w => w with { Lemma = "übrigbleiben" }),
        "Rt1v22" => LinkRoleVerseBuilder.Load(Rt1v22, Rt1v22Ref, SourceLanguage.Hebrew),
        "Rt1v22/lemmas" => Verse("Rt1v22").Word(176625, w => w with { Lemma = "zurückkehren" }),
        "Rt3v4" => LinkRoleVerseBuilder.Load(Rt3v4, Rt3v4Ref, SourceLanguage.Hebrew),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };

    public static TheoryData<string, int, string, string> Cases => new()
    {
        // Rule 1: Hebrew morpheme by tag, else the main morpheme.
        { "Gen1v3", 31, "Und", "1 head H9001" },
        { "Gen1v3", 33, "sprach", "1-main head H0559" },
        { "Gen1v3", 35, "werde", "1-main head H1961" },
        { "Gen1v3", 37, "und", "1 head H9001" },
        { "Gen1v3", 32, "Gott", "1-main head H0430G" },
        { "Gen1v3", 34, "Es (added)", "none" },
        { "Rt1v5", 176163, "Da (KOUS, no hint)", "1-main head H4191" },
        { "Rt1v5", 176169, "und", "1 head H9002" },
        { "Rt1v5", 176177, "von", "1 head H9006" },
        { "Rt1v5", 176178, "ihren", "1 head H9024" },
        { "Rt1v5", 176181, "und (of three prefixes)", "1 head H9002" },
        { "Rt1v5", 176182, "von (of three prefixes)", "1 head H9006" },
        { "Rt1v5", 176183, "ihrem", "1 head H9024" },
        { "Rt1v5", 176184, "Manne", "1-main head H0376H" },
        { "Rt1v5", 176167, "beiden (PIAT, suffix stays unlinked)", "1-main head H8147" },
        { "Rt1v5", 176176, "übrig, verb lemma bleiben", "1-main head H7604" },
        { "Rt1v22", 176630, "die", "1 head H9009" },
        { "Rt1v22", 176632, "ihre", "1 head H9024" },
        { "Rt1v22", 176634, "mit (preposition is the main morpheme)", "1-main head H5973A" },
        { "Rt1v22", 176635, "ihr (object suffix)", "1 head H9034" },
        { "Rt1v22", 176637, "aus (PTKVZ as preposition)", "1 head H9006" },
        { "Rt1v22", 176638, "den (no article morpheme)", "1-main head H7704G" },
        { "Rt1v22", 176643, "sie (independent pronoun)", "1-main head H1992" },
        { "Rt1v22", 176647, "beim", "1 head H9003" },
        { "Rt1v22", 176650, "Gerstenernte", "1-main head H8184" },
        { "Rt3v4", 177446, "er (subject suffix of the infinitive)", "1 head H9043" },
        { "Rt3v4", 177463, "seinen", "1 head H9023" },
        { "Rt3v4", 177470, "aber", "1 head H9002" },
        { "Rt3v4", 177472, "dir (suffix is the main morpheme)", "1 head H9032" },
        { "Rt3v4", 177462, "zu (PTKVZ, no infinitive follows)", "1-main head H4772" },

        // Rule 2: auxiliary unit.
        { "Mk1v8", 574860, "habe getauft", "2 unit G0907" },
        { "Mk1v8", 574864, "getauft", "none" },
        { "Mk1v8", 574867, "wird taufen", "2 unit G0907" },
        { "Mk1v8", 574858, "Ich (own source word)", "none" },
        { "Rt3v4", 177471, "wird kundtun", "2 unit H5046" },
        { "Rt3v4", 177477, "tun sollst", "2 unit H6213A" },
        { "Rt3v4", 177473, "kundtun", "1-main head H5046" },

        // Rule 3: particle unit.
        { "Mk1v35", 575390, "auf, verb lemma stehen", "none" },
        { "Mk1v35/lemmas", 575390, "auf of aufstehen", "3 unit G0450" },
        { "Mk1v35/lemmas", 575393, "hinaus of hinausgehen", "3 unit G1831" },
        { "Mk1v35/lemmas", 575397, "an (on εἰς, not the verb)", "none" },
        { "Mk1v29", 575272, "aus (on ἐκ, not the verb)", "none" },
        { "Rt1v22", 176627, "zurück, verb lemma kehren", "1-main head H7725G" },
        { "Rt1v22/lemmas", 176627, "zurück of zurückkehren", "3 unit H7725G" },
        { "Rt1v5/lemmas", 176176, "übrig of übrigbleiben", "3 unit H7604" },

        // Rule 4: reflexive unit.
        { "Rt3v4", 177455, "sich hinlegt", "4 unit H7901G" },
        { "Rt3v4", 177467, "dich (PPER, not PRF)", "1-main head H7901G" },

        // Rule 5: subject pronoun → added word.
        { "Gen1v3/before", 34, "Es werde", "5 added 35" },
        { "Gen1v3/before", 38, "es ward", "5 added 39" },
        { "Mk1v29/before", 575271, "sie gingen (participle 3rd plural)", "5 added 575270" },
        { "Mk1v35/before", 575389, "er stand (participle 3rd singular)", "5 added 575388" },
        { "Rt3v4", 177443, "es geschehe", "5 added 177444" },
        { "Rt3v4", 177454, "er sich hinlegt", "5 added 177456" },
        { "Rt3v4", 177475, "du sollst tun (parent is the finite verb)", "5 added 177477" },
        { "Rt3v4", 177469, "er (independent pronoun)", "1-main head H1931" },

        // Two rules fit with different outcomes: rule 1 (suffix H9043) and rule 4 (unit of the verb).
        { "Rt3v4", 177447, "wenn er sich niederlegt", "undecided 1,4" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Decides_the_link_of_a_German_word(string verse, int germanWordId, string word, string expected)
    {
        var result = LinkRoleRules.Decide(Verse(verse));

        result.Describe(germanWordId).ShouldBe(expected, word);
    }

    [Theory]
    [InlineData("Gen1v3")]
    [InlineData("Rt1v5")]
    [InlineData("Rt1v22")]
    [InlineData("Rt3v4")]
    public void Every_real_Hebrew_link_is_decided_or_undecided(string verse)
    {
        var input = Verse(verse);
        var result = LinkRoleRules.Decide(input);

        var real = input.Links.Where(l => !l.IsAddedWord).Select(l => l.Id).Order().ToList();
        result.Decisions.Select(d => d.LinkId).Concat(result.Undecided.Select(u => u.LinkId)).Order().ShouldBe(real);
    }

    [Fact]
    public void Mk1v8_decides_only_the_two_auxiliaries()
    {
        var result = LinkRoleRules.Decide(Verse("Mk1v8"));

        result.Decisions.Select(d => d.GermanWordId).ShouldBe([574860, 574867]);
        result.CountsByRule().ShouldBe(new Dictionary<LinkRoleRule, int> { [LinkRoleRule.R2AuxiliaryUnit] = 2 });
    }

    [Fact]
    public void Two_rules_with_different_outcomes_leave_the_link_undecided_with_a_reason()
    {
        var result = LinkRoleRules.Decide(Verse("Rt3v4"));

        var sich = result.Undecided.ShouldHaveSingleItem();
        sich.GermanWordId.ShouldBe(177447);
        sich.FittingRules.ShouldBe([LinkRoleRule.R1MorphemeHint, LinkRoleRule.R4ReflexiveOrZuUnit]);
        sich.Reason.ShouldContain("H9043");
        sich.Reason.ShouldContain("unit H7901G");
    }

    [Fact]
    public void Rule_1_with_two_fitting_suffixes_is_undecided()
    {
        // Rt 1:5 "ihrem Manne" with a second 3rd person suffix: ihr → her or their, both present.
        var verse = Verse("Rt1v5");
        verse = verse with
        {
            SourceWords = verse.SourceWords
                .Select(s => s.Id == 100013 ? s with { ExpandedStrongTags = s.ExpandedStrongTags + "/H9028=Pp3m=their", Morphology = s.Morphology + "/Sp3mp" } : s)
                .ToList(),
        };

        var result = LinkRoleRules.Decide(verse);

        result.Describe(176183).ShouldBe("undecided ");
        result.Undecided.Single(u => u.GermanWordId == 176183).Reason.ShouldContain("H9024, H9028");
    }

    [Fact]
    public void A_stored_Hebrew_morpheme_is_kept()
    {
        var verse = Verse("Gen1v3").LinkOf(31, l => l with { StrongsNumber = "H0559" });

        var result = LinkRoleRules.Decide(verse);

        result.Describe(31).ShouldBe("none");
        result.Describe(33).ShouldBe("1-main head H0559");
    }

    [Fact]
    public void A_source_word_with_two_main_morphemes_is_undecided()
    {
        var verse = Verse("Gen1v3");
        verse = verse with
        {
            SourceWords = verse.SourceWords
                .Select(s => s.Id == 25 ? s with { ExpandedStrongTags = "{H0935G=בּוֹא=come}//{H1409=גָּד=fortune}", Morphology = "HVqp3ms//Ncmsa" } : s)
                .ToList(),
        };

        var result = LinkRoleRules.Decide(verse);

        result.Describe(36).ShouldBe("undecided ");
        result.Undecided.Single(u => u.GermanWordId == 36).Reason.ShouldContain("several main morphemes");
    }

    [Theory]
    [InlineData("Mk1v35", 0)]
    [InlineData("Mk1v29", 0)]
    [InlineData("Rt1v5", 0)]
    [InlineData("Rt1v22", 0)]
    public void Rule_6_does_nothing_when_particles_keep_their_text(string verse, int expected)
    {
        LinkRoleRules.Decide(Verse(verse)).LemmaResets.Count.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Mk1v35/lemmas", 575393, "hinausgehen", "hinaus")]
    [InlineData("Mk1v29/lemmas", 575272, "ausgehen", "aus")]
    public void Rule_6_resets_a_particle_lemma_to_its_text(string verse, int wordId, string oldLemma, string newLemma)
    {
        var result = LinkRoleRules.Decide(Verse(verse));

        var reset = result.LemmaResets.ShouldHaveSingleItem();
        reset.ShouldBe(new ParticleLemmaReset(wordId, reset.PositionInVerse, oldLemma, newLemma));
        result.CountsByRule()[LinkRoleRule.R6ParticleLemmaReset].ShouldBe(1);
    }

    [Fact]
    public void Rule_6_keeps_the_verb_lemma()
    {
        var result = LinkRoleRules.Decide(Verse("Mk1v35/lemmas"));

        result.LemmaResets.ShouldNotContain(r => r.GermanWordId == 575392);
    }

    [Fact]
    public void Zu_before_an_infinitive_on_a_lamed_infinitive_fits_rules_1_and_4()
    {
        // "zu essen" for לֶאֱכֹל (H9005 + H0398 infinitive construct): zu renders the lamed (rule 1) or belongs to the verb (rule 4).
        var verse = new LinkRoleVerse(1, SourceLanguage.Hebrew,
            [new(1, 1, "zu", "zu", "PTKVZ"), new(2, 2, "essen", "essen", "VVINF")],
            [new(10, 1, 7, null), new(11, 2, 7, null)],
            [new(7, "HR/Vqcc", "H9005=ל=to/{H0398=אָכַל=to eat}")]);

        var result = LinkRoleRules.Decide(verse);

        result.Describe(1).ShouldBe("undecided 1,4");
        result.Describe(2).ShouldBe("1-main head H0398");
    }

    // ---- rule 3b: separable verb whose lemma lacks the particle ----------------------------------------------------------------

    private static readonly HashSet<string> Known = new(["aufstehen", "zurückkehren", "hinausgehen"], StringComparer.OrdinalIgnoreCase);

    [Theory]
    [InlineData("Mk1v35", 575390, 575388, "stehen", "aufstehen", "3b unit G0450")]
    [InlineData("Rt1v22", 176627, 176625, "kehren", "zurückkehren", "3b unit H7725G")]
    public void Rule_3b_joins_particle_and_verb_lemma_when_the_combination_is_known(string verse, int particleId, int verbId, string oldLemma, string newLemma, string expected)
    {
        var result = LinkRoleRules.Decide(Verse(verse), Known);

        result.Describe(particleId).ShouldBe(expected);
        result.VerbLemmas.ShouldHaveSingleItem().ShouldBe(new SeparableVerbLemma(verbId, result.VerbLemmas[0].PositionInVerse, oldLemma, newLemma, particleId));
        result.CountsByRule()[LinkRoleRule.R3bSeparableVerb].ShouldBe(1);
    }

    [Fact]
    public void Rule_3b_leaves_an_unknown_combination_undecided()
    {
        var result = LinkRoleRules.Decide(Verse("Mk1v35"), new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        result.Describe(575390).ShouldBe("undecided ");
        result.Undecided.Single(u => u.GermanWordId == 575390).Reason.ShouldBe("separable verb, unknown combination: aufstehen");
        result.VerbLemmas.ShouldBeEmpty();
    }

    [Fact]
    public void Rule_3b_is_off_without_a_lemma_set_and_rule_3_wins_once_the_lemma_is_joined()
    {
        LinkRoleRules.Decide(Verse("Mk1v35")).Describe(575390).ShouldBe("none");

        var joined = LinkRoleRules.Decide(Verse("Mk1v35/lemmas"), Known);
        joined.Describe(575390).ShouldBe("3 unit G0450");
        joined.VerbLemmas.ShouldBeEmpty();
    }

    [Fact]
    public void Rule_3b_with_two_particles_wanting_different_lemmas_on_one_verb_is_undecided()
    {
        // A second PTKVZ "hin" on the same morpheme as "stand": aufstehen and hinstehen both known.
        var input = Verse("Mk1v35");
        var auf = input.Links.Single(l => l.GermanWordId == 575390);
        input = input with
        {
            Words = [.. input.Words, new LinkRoleGermanWord(999, 99, "hin", "hin", "PTKVZ")],
            Links = [.. input.Links, auf with { Id = 9999, GermanWordId = 999 }],
        };

        var result = LinkRoleRules.Decide(input, new HashSet<string>(["aufstehen", "hinstehen"], StringComparer.OrdinalIgnoreCase));

        result.VerbLemmas.ShouldBeEmpty();
        result.Undecided.Where(u => u.GermanWordId is 575390 or 999).Select(u => u.Reason).ShouldAllBe(r => r.StartsWith("separable verb, several particles"));
    }
}
