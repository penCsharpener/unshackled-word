using UnshackledWord.Application.Features.LinkRoles;
using UnshackledWord.Application.Features.LinkRoles.Backfill;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Application.Tests.Features.LinkRoles;

/// <summary>Rule decisions → row changes with exact before/after values, the report, and the guards of the backfill change set (roadmap 0018.4).</summary>
public class LinkRoleChangeSetBuilderTests
{
    private const int Verse = 1001003;
    private static readonly HashSet<string> NoLemmas = new(StringComparer.OrdinalIgnoreCase);

    private static LinkRoleWordRow Word(int id, int position, string text, string? lemma, string stts, string? person = null, string? number = null, string? gender = null, string? @case = null, int verse = Verse) =>
        new() { Id = id, HebRefId = verse, PositionInVerse = position, Text = text, Lemma = lemma, Stts = stts, Person = person, Number = number, Gender = gender, Case = @case, Chapter = 1, Verse = verse % 1000 };

    private static LinkRoleMappingRow Row(int id, int wordId, int? stepWordId, string? strongs = null, bool? added = false, int? parent = null, int verse = Verse) =>
        new() { Id = id, ElbWordId = wordId, HebRefId = verse, StepWordId = stepWordId, StrongsNumber = strongs, IsAddedWord = added, ParentGermanWordId = parent };

    private static LinkRoleBookData Book(IReadOnlyList<LinkRoleWordRow> words, IReadOnlyList<LinkRoleMappingRow>? greek = null, IReadOnlyList<LinkRoleMappingRow>? hebrew = null,
        IReadOnlyList<LinkRoleSourceRow>? greekSources = null, IReadOnlyList<LinkRoleSourceRow>? hebrewSources = null) =>
        new(1, "Genesis", words, greek ?? [], hebrew ?? [], greekSources ?? [], hebrewSources ?? []);

    private static LinkRoleChangeSet Build(LinkRoleBookData book, IReadOnlySet<string>? known = null)
    {
        var builder = new LinkRoleChangeSetBuilder(known ?? NoLemmas);
        builder.AddBook(book);
        return builder.Build();
    }

    // Gen 1:3 "Und Gott sprach": ו and אָמַר on one Hebrew word.
    private static LinkRoleBookData Gen1v3(string? undStrongs = null, string? sprachStrongs = null) => Book(
        [Word(31, 1, "Und", "und", "KON"), Word(32, 2, "Gott", "Gott", "NN"), Word(33, 3, "sprach", "sprechen", "VVFIN")],
        hebrew: [Row(1, 31, 21, undStrongs), Row(2, 32, 22), Row(3, 33, 21, sprachStrongs)],
        hebrewSources:
        [
            new LinkRoleSourceRow { Id = 21, Morphology = "Hc/Vqw3ms", MorphemeTags = "H9001=ו=&/{H0559=אָמַר=to say}" },
            new LinkRoleSourceRow { Id = 22, Morphology = "HNcmpa", MorphemeTags = "{H0430G=אֱלֹהִים=God}" },
        ]);

    [Fact]
    public void Hebrew_rule_1_writes_the_morpheme_and_keeps_every_other_column()
    {
        var set = Build(Gen1v3());

        set.Mappings.ShouldBe(
        [
            new MappingRowChange('H', 1, Verse, 31, "1", new MappingRowValues("head", null, false, null, 21), new MappingRowValues("head", "H9001", false, null, 21)),
            new MappingRowChange('H', 2, Verse, 32, "1-main", new MappingRowValues("head", null, false, null, 22), new MappingRowValues("head", "H0430G", false, null, 22)),
            new MappingRowChange('H', 3, Verse, 33, "1-main", new MappingRowValues("head", null, false, null, 21), new MappingRowValues("head", "H0559", false, null, 21)),
        ]);
        set.Report.Decided[LinkRoleChangeSetBuilder.Hebrew].ShouldBe(new Dictionary<string, int> { ["1"] = 1, ["1-main"] = 2 });
        set.Report.Changed[LinkRoleChangeSetBuilder.Hebrew].ShouldBe(new Dictionary<string, int> { ["1"] = 1, ["1-main"] = 2 });
        (set.Report.Verses, set.Report.Links, set.Report.MappingRows, set.Report.LemmaRows).ShouldBe((1, 3, 3, 0));
    }

    [Fact]
    public void A_second_run_on_backfilled_data_changes_nothing()
    {
        var set = Build(Book(
            Gen1v3().Words,
            hebrew: [Row(1, 31, 21, "H9001"), Row(2, 32, 22, "H0430G"), Row(3, 33, 21, "H0559")],
            hebrewSources: Gen1v3().HebrewSources));

        set.Mappings.ShouldBeEmpty();
        set.Report.Changed.ShouldBeEmpty();
    }

    // Mk 1:35 "stand er auf": ἀναστὰς (participle) carries stand, er and auf.
    private static LinkRoleBookData Mk1v35(bool erHasSecondRow = false, string verbLemma = "stehen") => Book(
        [Word(88, 1, "stand", verbLemma, "VVFIN"), Word(89, 2, "er", "er", "PPER", "3", "S", "M", "Nom"), Word(90, 3, "auf", "auf", "PTKVZ")],
        greek: erHasSecondRow
            ? [Row(1, 88, 10, "G0450"), Row(2, 89, 10, "G0450"), Row(3, 90, 10, "G0450"), Row(4, 89, 11, "G4404")]
            : [Row(1, 88, 10, "G0450"), Row(2, 89, 10, "G0450"), Row(3, 90, 10, "G0450")],
        greekSources: [new LinkRoleSourceRow { Id = 10, Morphology = "V-2AAP-NSM" }, new LinkRoleSourceRow { Id = 11, Morphology = "ADV" }]);

    [Fact]
    public void Rule_5_turns_the_pronoun_into_an_added_word_of_the_verb_without_step_word()
    {
        var set = Build(Mk1v35());

        set.Mappings.Single(m => m.MappingId == 2).ShouldBe(new MappingRowChange('G', 2, Verse, 89, "5",
            new MappingRowValues("head", "G0450", false, null, 10),
            new MappingRowValues("head", null, true, 88, null)));
    }

    [Fact]
    public void Rule_5_leaves_a_pronoun_with_another_row_undecided()
    {
        var set = Build(Mk1v35(erHasSecondRow: true));

        set.Mappings.ShouldNotContain(m => m.ElbWordId == 89);
        set.Report.UndecidedByReason[LinkRoleChangeSetBuilder.Greek]["rule 5: subject pronoun with several rows"].ShouldBe(1);
    }

    [Fact]
    public void Rule_3b_makes_the_particle_a_unit_and_joins_the_verb_lemma()
    {
        var set = Build(Mk1v35(), new HashSet<string>(["aufstehen"], StringComparer.OrdinalIgnoreCase));

        set.Mappings.Single(m => m.MappingId == 3).ShouldBe(new MappingRowChange('G', 3, Verse, 90, "3b",
            new MappingRowValues("head", "G0450", false, null, 10),
            new MappingRowValues("unit", "G0450", false, null, 10)));
        set.Lemmas.ShouldBe([new LemmaRowChange(88, Verse, 1, "3b", "stehen", "aufstehen")]);
        set.Report.Changed[LinkRoleChangeSetBuilder.German]["3b"].ShouldBe(1);
        set.Report.Samples["3b"].ShouldHaveSingleItem().Changes.Select(c => $"{c.Word}: {c.Before} → {c.After}")
            .ShouldBe(["auf: head G0450 → unit G0450", "stand: lemma stehen → lemma aufstehen"]);
    }

    [Fact]
    public void Rule_3b_with_an_unknown_combination_is_undecided()
    {
        var set = Build(Mk1v35());

        set.Mappings.ShouldNotContain(m => m.ElbWordId == 90);
        set.Lemmas.ShouldBeEmpty();
        set.Report.UndecidedByReason[LinkRoleChangeSetBuilder.Greek]["separable verb, unknown combination"].ShouldBe(1);
    }

    [Fact]
    public void Rule_3_on_a_joined_lemma_writes_no_lemma()
    {
        var set = Build(Mk1v35(verbLemma: "aufstehen"), new HashSet<string>(["aufstehen"], StringComparer.OrdinalIgnoreCase));

        set.Mappings.Single(m => m.MappingId == 3).Rule.ShouldBe("3");
        set.Lemmas.ShouldBeEmpty();
    }

    [Fact]
    public void Rule_6_resets_a_particle_lemma_even_without_links()
    {
        var set = Build(Book([Word(70, 1, "ging", "ausgehen", "VVFIN"), Word(71, 2, "aus", "ausgehen", "PTKVZ")]));

        set.Lemmas.ShouldBe([new LemmaRowChange(71, Verse, 2, "6", "ausgehen", "aus")]);
        set.Report.Decided[LinkRoleChangeSetBuilder.German]["6"].ShouldBe(1);
        set.Report.Verses.ShouldBe(1);
    }

    [Fact]
    public void Samples_are_capped_per_rule_and_stable()
    {
        var words = Enumerable.Range(0, 60).SelectMany(v => new[]
        {
            Word(v * 10 + 1, 1, "Und", "und", "KON", verse: 1001000 + v),
            Word(v * 10 + 2, 2, "sprach", "sprechen", "VVFIN", verse: 1001000 + v),
        }).ToList();
        var rows = Enumerable.Range(0, 60).SelectMany(v => new[]
        {
            Row(v * 10 + 1, v * 10 + 1, 21, verse: 1001000 + v),
            Row(v * 10 + 2, v * 10 + 2, 21, verse: 1001000 + v),
        }).ToList();
        var book = Book(words, hebrew: rows, hebrewSources: Gen1v3().HebrewSources);

        var first = Build(book).Report.Samples;
        var second = Build(book).Report.Samples;

        first["1"].Count.ShouldBe(LinkRoleReport.SamplesPerRule);
        first["1"].Select(s => s.HebRefId).ShouldBe(second["1"].Select(s => s.HebRefId));
        first["1"].Select(s => s.HebRefId).ShouldBeInOrder();
        first["1"][0].Reference.ShouldStartWith("Genesis 1:");
    }

    [Theory]
    [InlineData("source word 4711 has several main morphemes: H0935G, H1409", "source word has several main morphemes")]
    [InlineData("rule 1: several morphemes fit: H9023, H9033", "rule 1: several morphemes fit")]
    [InlineData("rules 1 and 4 fit with different outcomes: 1 → head H9043; 4 → unit H7901G", "rules 1 and 4 fit with different outcomes")]
    [InlineData("separable verb, unknown combination: aufstehen", "separable verb, unknown combination")]
    [InlineData("link has no Strong's", "link has no Strong's")]
    public void Undecided_reasons_are_grouped(string reason, string group)
    {
        LinkRoleChangeSetBuilder.ReasonGroup(reason).ShouldBe(group);
    }

    [Fact]
    public void After_keeps_the_Greek_strongs_and_sets_the_Hebrew_morpheme()
    {
        var before = new MappingRowValues("head", "G0907", null, null, 5);

        LinkRoleChangeSetBuilder.After(new LinkRoleDecision(1, 2, LinkRoleRule.R2AuxiliaryUnit, LinkRole.Unit, "G0907"), before, SourceLanguage.Greek)
            .ShouldBe(before with { Role = "unit" });
        LinkRoleChangeSetBuilder.After(new LinkRoleDecision(1, 2, LinkRoleRule.R1MorphemeHint, LinkRole.Head, "H9001"), before with { StrongsNumber = null }, SourceLanguage.Hebrew)
            .ShouldBe(new MappingRowValues("head", "H9001", null, null, 5));
    }
}
