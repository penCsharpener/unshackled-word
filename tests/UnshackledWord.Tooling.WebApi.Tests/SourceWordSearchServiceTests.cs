using UnshackledWord.Domain.Models.DataQuality;
using UnshackledWord.Domain.WebApi.Elberfelder.SearchSourceWords;
using UnshackledWord.Tooling.WebApi.Endpoints.DataQuality;
using UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.Search.SourceWords;

namespace UnshackledWord.Tooling.WebApi.Tests;

public class SourceWordSearchServiceTests
{
    private sealed class FakeRefs : ISourceWordRefs
    {
        public List<(string Strongs, string Form)> Asked { get; } = [];

        public Task<IReadOnlyList<SourceWordRefRow>> ReadAsync(IReadOnlyList<(string Strongs, string Form)> forms, int perForm, CancellationToken ct)
        {
            Asked.AddRange(forms);
            return Task.FromResult<IReadOnlyList<SourceWordRefRow>>(
                [.. forms.Select(f => new SourceWordRefRow { Strongs = f.Strongs, Form = f.Form, BibleBookId = 43, Chapter = 1, Verse = 1 })]);
        }
    }

    private static LexiconEntry Entry(string key, string lemma, string translit, string gloss) => new()
    {
        Key = key,
        Lang = key[..1],
        Lemma = lemma,
        Transliteration = translit,
        Gloss = gloss,
        FoldedScript = TranslitFolding.FoldScript(lemma),
        FoldedTranslit = TranslitFolding.Latin(translit),
        Skeleton = TranslitFolding.Skeleton(translit),
        GlossWords = gloss.ToLowerInvariant().Split(' '),
    };

    private static SourceFormEntry Form(string key, string form, int count) => new(key, form, TranslitFolding.FoldScript(form), count);

    private static (SourceWordSearchService Service, FakeRefs Refs) Create(IEnumerable<SourceFormEntry>? forms = null)
    {
        var lexicon = StrongsLexiconIndex.FromEntries(
        [
            Entry("G3056", "λόγος", "logos", "word"),
            Entry("G3004", "λέγω", "legō", "say"),
            Entry("G0026", "ἀγάπη", "agapē", "love"),
            Entry("H1697", "דָּבָר", "dabar", "word"),
            Entry("H4428", "מֶלֶךְ", "melek", "king"),
            Entry("H0430", "אֱלֹהִים", "elohim", "God"),
        ]);
        var index = SourceFormIndex.FromEntries(forms ??
        [
            Form("G3056", "λόγος", 200),
            Form("G3056", "λόγου", 120),
            Form("G3056", "λόγῳ", 40),
            Form("G3004", "λέγει", 300),
            Form("G3004", "λόγους", 1),
            Form("G0026", "ἀγάπη", 60),
            Form("H1697", "דָּבָר", 50),
            Form("H1697", "וְהַדָּבָר", 3),
            Form("H4428", "מֶלֶךְ", 2500),
            Form("H4428", "הַמֶּלֶךְ", 900),
        ]);
        var refs = new FakeRefs();
        return (new SourceWordSearchService(lexicon, index, refs), refs);
    }

    [Fact]
    public async Task Greek_without_diacritics_finds_the_lemma_and_groups_the_matching_forms_under_it()
    {
        var (service, _) = Create();

        var response = await service.SearchAsync("λογου", 8, default);

        var lemma = response!.Tokens.Single().Lemmas.First();
        lemma.Strongs.ShouldBe("G3056");
        lemma.Language.ShouldBe("greek");
        lemma.Lemma.ShouldBe("λόγος");
        lemma.Occurrences.ShouldBe(360);
        lemma.MatchedBy.ShouldBe("form");
        lemma.Forms.Select(f => f.Form).ShouldBe(["λόγου"]);
        lemma.Forms[0].Refs.Single().BibleBookId.ShouldBe(43);
    }

    [Fact]
    public async Task A_prefix_lists_lemma_entries_and_all_matching_forms_by_frequency()
    {
        var (service, _) = Create();

        var lemmas = (await service.SearchAsync("λογ", 8, default))!.Tokens.Single().Lemmas;

        lemmas.Select(l => l.Strongs).ShouldBe(["G3056", "G3004"]);
        lemmas[0].Forms.Select(f => f.Form).ShouldBe(["λόγος", "λόγου", "λόγῳ"]);
        lemmas[1].MatchedBy.ShouldBe("form");
        lemmas[1].Forms.Select(f => f.Form).ShouldBe(["λόγους"]);
    }

    [Fact]
    public async Task The_exact_lemma_ranks_before_more_frequent_prefix_matches()
    {
        var (service, _) = Create();

        var lemmas = (await service.SearchAsync("λεγω", 8, default))!.Tokens.Single().Lemmas;

        lemmas[0].Strongs.ShouldBe("G3004");
        lemmas[0].MatchedBy.ShouldBe("script");
    }

    [Theory]
    [InlineData("מלך")]
    [InlineData("מֶלֶךְ")]
    [InlineData("מלכ")]
    public async Task Hebrew_matches_without_points_and_with_final_letters(string typed)
    {
        var (service, _) = Create();

        var lemmas = (await service.SearchAsync(typed, 8, default))!.Tokens.Single().Lemmas;

        lemmas.Select(l => l.Strongs).ShouldBe(["H4428"]);
        lemmas[0].Language.ShouldBe("hebrew");
        lemmas[0].Forms[0].Form.ShouldBe("מֶלֶךְ");
    }

    [Theory]
    [InlineData("G3056")]
    [InlineData("g3056")]
    [InlineData("G03056")]
    public async Task A_strongs_number_returns_the_entry_with_its_most_frequent_forms(string typed)
    {
        var (service, _) = Create();

        var lemma = (await service.SearchAsync(typed, 8, default))!.Tokens.Single().Lemmas.Single();

        lemma.Strongs.ShouldBe("G3056");
        lemma.MatchedBy.ShouldBe("strongs");
        lemma.Forms.Select(f => f.Form).ShouldBe(["λόγος", "λόγου", "λόγῳ"]);
    }

    [Fact]
    public async Task Hebrew_strongs_numbers_are_shown_without_leading_zeros()
    {
        var (service, _) = Create(forms: [Form("H0430", "אֱלֹהִים", 2600)]);

        var lemma = (await service.SearchAsync("h430", 8, default))!.Tokens.Single().Lemmas.Single();

        lemma.Strongs.ShouldBe("H430");
        lemma.Occurrences.ShouldBe(2600);
        SourceWordSearchService.DisplayKey("G0026").ShouldBe("G26");
    }

    [Fact]
    public async Task A_transliteration_finds_the_lemma_without_looking_at_the_forms()
    {
        var (service, _) = Create();

        var lemmas = (await service.SearchAsync("dabar", 8, default))!.Tokens.Single().Lemmas;

        lemmas.Select(l => l.Strongs).ShouldBe(["H1697"]);
        lemmas[0].MatchedBy.ShouldBe("translit");
    }

    [Fact]
    public async Task Every_word_gets_its_own_token_and_punctuation_is_ignored()
    {
        var (service, _) = Create();

        var tokens = (await service.SearchAsync("ἀγάπη· G3056 ἀγάπη", 8, default))!.Tokens;

        tokens.Select(t => t.Token).ShouldBe(["ἀγάπη", "G3056"]);
        tokens[0].Lemmas[0].Strongs.ShouldBe("G26");
    }

    [Fact]
    public async Task The_limit_caps_the_lemma_entries()
    {
        var (service, _) = Create();

        var lemmas = (await service.SearchAsync("λογ", 1, default))!.Tokens.Single().Lemmas;

        lemmas.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Unknown_words_give_an_empty_token()
    {
        var (service, refs) = Create();

        var tokens = (await service.SearchAsync("ωωωω", 8, default))!.Tokens;

        tokens.Single().Lemmas.ShouldBeEmpty();
        refs.Asked.ShouldBeEmpty();
    }

    [Fact]
    public async Task Without_computed_facts_the_search_is_not_ready()
    {
        var (service, _) = Create([]);

        (await service.SearchAsync("λογος", 8, default)).ShouldBeNull();
    }

    [Theory]
    [InlineData("  ἀγάπη,  τί; ", new[] { "ἀγάπη", "τί" })]
    [InlineData("אֶת־הָאָרֶץ", new[] { "אֶתהָאָרֶץ" })]
    [InlineData("αγαπη ἀγάπη", new[] { "αγαπη" })]
    [InlineData("... ;", new string[0])]
    public void Tokenize_removes_punctuation_and_duplicates(string q, string[] expected) =>
        SourceWordSearchService.Tokenize(q).ShouldBe(expected);

    private static SearchSourceWordsRequest Request(string q, int limit = 8) => new() { Q = q, Limit = limit };

    [Theory]
    [InlineData("λόγος", 8, true)]
    [InlineData("G1577 H430", 1, true)]
    [InlineData("", 8, false)]
    [InlineData("   ", 8, false)]
    [InlineData(",;·", 8, false)]
    [InlineData("a b c d e f", 8, false)]
    [InlineData("λόγος", 0, false)]
    [InlineData("λόγος", 31, false)]
    public void The_validator_checks_the_words_and_the_limit(string q, int limit, bool valid) =>
        new SearchSourceWordsRequestValidator().Validate(Request(q, limit)).IsValid.ShouldBe(valid);

    [Fact]
    public void The_validator_rejects_a_too_long_query_and_a_too_long_word()
    {
        var validator = new SearchSourceWordsRequestValidator();

        validator.Validate(Request(new string('α', 51))).IsValid.ShouldBeFalse();
        validator.Validate(Request(string.Join(' ', Enumerable.Repeat("αβ", 100)))).IsValid.ShouldBeFalse();
    }
}
