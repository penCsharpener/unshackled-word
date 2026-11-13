using UnshackledWord.Application.Features.VerseSearch;
using UnshackledWord.Domain.WebApi.Elberfelder.SearchVerses;

namespace UnshackledWord.Application.Tests.Features;

public class VerseSearchQueryTests
{
    private static readonly IReadOnlyDictionary<string, string> NoStems = new Dictionary<string, string>();
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> NoLemmas = new Dictionary<string, IReadOnlyList<string>>();

    private static BuiltVerseQuery Build(string input, Dictionary<string, string>? stems = null, Dictionary<string, IReadOnlyList<string>>? lemmas = null)
    {
        return VerseSearchQueryBuilder.Build(VerseSearchQueryParser.Parse(input), stems ?? NoStems, lemmas ?? NoLemmas);
    }

    [Fact]
    public void Parse_PlainWords_AreAndedInOneGroup()
    {
        var query = VerseSearchQueryParser.Parse("Ich bin der Weg");

        query.Groups.Count.ShouldBe(1);
        query.Groups[0].Select(t => t.Words.Single()).ShouldBe(["ich", "bin", "der", "weg"]);
        query.WordCount.ShouldBe(4);
    }

    [Fact]
    public void Parse_QuotedPhrase_IsOneTerm()
    {
        var term = VerseSearchQueryParser.Parse("\"im Anfang\" Gott").Groups.Single().First();

        term.IsPhrase.ShouldBeTrue();
        term.Words.ShouldBe(["im", "anfang"]);
    }

    [Fact]
    public void Parse_UnclosedQuote_TakesTheRestAsPhrase()
    {
        var term = VerseSearchQueryParser.Parse("Gott \"im Anfang").Groups.Single().Last();

        term.IsPhrase.ShouldBeTrue();
        term.Words.ShouldBe(["im", "anfang"]);
    }

    [Fact]
    public void Parse_LeadingMinus_Excludes_ButHyphenInsideAWordStays()
    {
        var terms = VerseSearchQueryParser.Parse("Gott -Engel Zebaoth-Gott").Groups.Single();

        terms.Single(t => t.Negated).Words.ShouldBe(["engel"]);
        terms.Last().Words.ShouldBe(["zebaoth-gott"]);
        terms.Last().Negated.ShouldBeFalse();
    }

    [Theory]
    [InlineData("Gott oder Herr")]
    [InlineData("Gott OR Herr")]
    public void Parse_OderAndOr_SplitAlternatives(string input)
    {
        var query = VerseSearchQueryParser.Parse(input);

        query.Groups.Count.ShouldBe(2);
        query.WordCount.ShouldBe(2);
    }

    [Fact]
    public void Parse_QuotedOder_IsAWord()
    {
        VerseSearchQueryParser.Parse("\"oder\"").Groups.Single().Single().Words.ShouldBe(["oder"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!! ... ???")]
    [InlineData("-Gott")]
    [InlineData("oder")]
    [InlineData("Gott oder -Engel", true)]
    public void Parse_WithoutPositiveWord_HasNoPositive(string input, bool expectPositive = false)
    {
        VerseSearchQueryParser.Parse(input).HasPositive.ShouldBe(expectPositive);
    }

    [Fact]
    public void Parse_AlternativeWithOnlyAnExclusion_IsDropped()
    {
        VerseSearchQueryParser.Parse("Gott oder -Engel").Groups.Count.ShouldBe(1);
    }

    [Fact]
    public void Build_PlainWords_QuoteTheStems()
    {
        var built = Build("Gott Welt", new() { ["gott"] = "gott", ["welt"] = "welt" });

        built.TsQuery.ShouldBe("(('gott') & ('welt'))");
        built.LiteralTsQuery.ShouldBe(built.TsQuery);
        built.MatchStems.ShouldBe(["gott", "welt"], ignoreOrder: true);
    }

    [Fact]
    public void Build_Phrase_UsesTheFollowedByOperator()
    {
        Build("\"im Anfang\"").TsQuery.ShouldBe("(('im' <-> 'anfang'))");
    }

    [Fact]
    public void Build_Exclusion_UsesNot_AndIsNotMarked()
    {
        var built = Build("Gott -Engel");

        built.TsQuery.ShouldBe("(('gott') & !('engel'))");
        built.MatchStems.ShouldBe(["gott"]);
        built.Tokens.Select(t => t.Text).ShouldBe(["gott"]);
    }

    [Fact]
    public void Build_Alternatives_AreOrGroups()
    {
        Build("Gott Herr oder Engel").TsQuery.ShouldBe("(('gott') & ('herr')) | (('engel'))");
    }

    [Fact]
    public void Build_StemsComeFromTheDictionary()
    {
        var built = Build("Füße", new() { ["füße"] = "fuss" });

        built.TsQuery.ShouldBe("(('fuss'))");
        built.Tokens.Single().Lexemes.ShouldBe(["fuss"]);
    }

    [Fact]
    public void Build_HyphenatedWord_IsOneAndPerLetterRun()
    {
        Build("Zebaoth-Gott").TsQuery.ShouldBe("(('zebaoth' & 'gott'))");
    }

    [Fact]
    public void Build_Lemmas_WidenSingleWords_AndLiteralQueryStaysStemOnly()
    {
        var built = Build("ging", new() { ["ging"] = "ging" }, new() { ["ging"] = ["gehen"] });

        built.TsQuery.ShouldBe("(('ging' | 'gehen':B))");
        built.LiteralTsQuery.ShouldBe("(('ging'))");
        built.MatchLemmas.ShouldBe(["gehen"]);
        built.Tokens.Single().Lemmas.ShouldBe(["gehen"]);
    }

    [Fact]
    public void Build_Lemmas_NeverApplyToPhrasesOrExclusions()
    {
        var lemmas = new Dictionary<string, IReadOnlyList<string>> { ["ging"] = ["gehen"], ["kam"] = ["kommen"], ["er"] = ["er"] };

        var built = Build("\"er ging\" -kam", lemmas: lemmas);

        built.TsQuery.ShouldBe("(('er' <-> 'ging') & !('kam'))");
        built.MatchLemmas.ShouldBeEmpty();
    }

    [Fact]
    public void Build_LemmaEqualToTheStem_IsNotAdded_AndOddLemmasAreSkipped()
    {
        var lemmas = new Dictionary<string, IReadOnlyList<string>> { ["gott"] = ["gott", "gott-vater", "zwei wörter"] };

        Build("Gott", lemmas: lemmas).TsQuery.ShouldBe("(('gott'))");
    }

    [Theory]
    [InlineData("o'brien", "(('o' & 'brien'))")]
    [InlineData("a'b oder c", "(('a' & 'b')) | (('c'))")]
    public void Build_ApostropheSplitsLikeAHyphen(string input, string expected)
    {
        Build(input).TsQuery.ShouldBe(expected);
    }

    [Theory]
    [InlineData("'; DROP TABLE \"Elb1871Verses\"; --")]
    [InlineData("gott & !(herr) | 'x' <-> y:*")]
    [InlineData("\\' ) | ( 'a")]
    public void Build_InjectionStrings_EndUpAsQuotedLexemesOnly(string input)
    {
        var built = Build(input);

        // Only letters survive the parser, so operators and quotes can only come from the builder itself.
        built.TsQuery.Where(c => !char.IsLetter(c)).Distinct().ShouldBeSubsetOf("'()&|!<->: ".ToCharArray());
        built.TsQuery.ShouldNotContain("DROP TABLE \"");
        built.TsQuery.ShouldNotContain(";");
        built.TsQuery.ShouldNotContain("*");
        built.TsQuery.ShouldNotContain("\\");
    }

    [Fact]
    public void Build_QuotesInALexemeAreEscaped()
    {
        var built = Build("gott", new() { ["gott"] = "go'tt\\" });

        built.TsQuery.ShouldBe("(('go''tt\\\\'))");
    }

    [Fact]
    public void Validator_AcceptsNormalQueries()
    {
        var validator = new SearchVersesRequestValidator();

        validator.Validate(new SearchVersesRequest { Q = "Gott Welt geliebt" }).IsValid.ShouldBeTrue();
        validator.Validate(new SearchVersesRequest { Q = "\"ich bin\" -Engel", PageSize = 50, BookId = 43 }).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validator_TreatsAnEmptyBookIdBoundAsZeroAsNoFilter()
    {
        new SearchVersesRequestValidator().Validate(new SearchVersesRequest { Q = "Gott", BookId = 0 }).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("...")]
    [InlineData("-Gott")]
    [InlineData("eins zwei drei vier fünf sechs sieben acht neun")]
    public void Validator_RejectsBadQueries(string q)
    {
        new SearchVersesRequestValidator().Validate(new SearchVersesRequest { Q = q }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validator_RejectsTooLongQueriesAndWords()
    {
        var validator = new SearchVersesRequestValidator();

        validator.Validate(new SearchVersesRequest { Q = new string('a', 201) }).IsValid.ShouldBeFalse();
        validator.Validate(new SearchVersesRequest { Q = new string('a', 51) }).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0, 20, null)]
    [InlineData(1, 9, null)]
    [InlineData(1, 51, null)]
    [InlineData(1, 20, -1)]
    [InlineData(1, 20, 67)]
    public void Validator_RejectsBadPaging(int page, int pageSize, int? bookId)
    {
        new SearchVersesRequestValidator().Validate(new SearchVersesRequest { Q = "Gott", Page = page, PageSize = pageSize, BookId = bookId }).IsValid.ShouldBeFalse();
    }
}
