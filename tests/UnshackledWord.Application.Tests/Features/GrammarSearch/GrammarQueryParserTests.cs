using UnshackledWord.Application.Features.GrammarSearch;
using UnshackledWord.Domain.Models.Grammar;

namespace UnshackledWord.Application.Tests.Features.GrammarSearch;

public class GrammarQueryParserTests
{
    private static GrammarParseResult Ok(string q)
    {
        var result = GrammarQueryParser.Parse(q);
        result.Error.ShouldBeNull();
        return result;
    }

    [Fact]
    public void FilterBindsToTheNearestPrecedingWord()
    {
        var slots = Ok("agape Kasus:nom pistis Kasus:gen").Slots;

        slots.Count.ShouldBe(2);
        slots[0].Word.ShouldBe("agape");
        slots[0].Filters.Single().Values.Single().Id.ShouldBe(MorphologyKey.Nominative);
        slots[1].Word.ShouldBe("pistis");
        slots[1].Filters.Single().Values.Single().Id.ShouldBe(MorphologyKey.Genitive);
        slots[0].Text.ShouldBe("agape Kasus:nom");
    }

    [Fact]
    public void FiltersBeforeTheFirstWordFormAnAnyWordSlot()
    {
        var slots = Ok("Tempus:aor Diathese:pass").Slots;

        var slot = slots.ShouldHaveSingleItem();
        slot.Word.ShouldBeNull();
        slot.Filters.Select(f => f.Key.Name).ShouldBe(["Tempus", "Diathese"]);

        var withWord = Ok("Kasus:nom agape").Slots;
        withWord.Count.ShouldBe(2);
        withWord[0].Word.ShouldBeNull();
        withWord[1].Word.ShouldBe("agape");
    }

    [Fact]
    public void CommaIsOrWithinTheKey_AndMinusExcludes()
    {
        var filters = Ok("G26 Kasus:nom,akk -Modus:partizip").Slots.Single().Filters;

        filters[0].Values.Select(v => v.Id).ShouldBe([MorphologyKey.Nominative, MorphologyKey.Accusative]);
        filters[0].Negated.ShouldBeFalse();
        filters[1].Negated.ShouldBeTrue();
        filters[1].Values.Single().Id.ShouldBe(MorphologyKey.Participle);
    }

    [Theory]
    [InlineData("Fall:nom")]
    [InlineData("fall:nom")]
    [InlineData("KASUS:Nominativ")]
    [InlineData("Kasus:NOM")]
    public void KeysAndAliasesMatchFolded(string token) =>
        Ok(token).Slots.Single().Filters.Single().Key.Name.ShouldBe("Kasus");

    [Fact]
    public void ValuesMatchFoldedUmlautsAndPrefixes()
    {
        Ok("Tempus:prasens").Slots.Single().Filters.Single().Values.Single().Id.ShouldBe(MorphologyKey.Present);
        Ok("Tempus:Präs").Slots.Single().Filters.Single().Values.Single().Id.ShouldBe(MorphologyKey.Present);
        Ok("Wortart:praposition").Slots.Single().Filters.Single().Values.Single().Id.ShouldBe(MorphologyKey.Preposition);
    }

    [Fact]
    public void AmbiguousPrefix_ReturnsTheCandidates()
    {
        var error = GrammarQueryParser.Parse("Tempus:p").Error;

        error.ShouldNotBeNull();
        error.Code.ShouldBe(GrammarErrorCodes.AmbiguousValue);
        error.Token.ShouldBe("Tempus:p");
        error.Candidates.ShouldBe(["Präsens", "Perfekt", "Plusquamperfekt"]);
    }

    [Fact]
    public void ExactLabelBeatsALongerPrefix()
    {
        var values = Ok("Tempus:futur").Slots.Single().Filters.Single().Values;

        values.Single().Id.ShouldBe(MorphologyKey.Future);
        Ok("Tempus:futur ii").Slots.Count.ShouldBe(2); // a second token is a word, the typed label has no space
        Ok("Tempus:futur,perf").Slots.Single().Filters.Single().Values.Select(v => v.Id).ShouldBe([MorphologyKey.Future, MorphologyKey.Perfect]);
    }

    [Theory]
    [InlineData("Foo:bar", GrammarErrorCodes.UnknownKey)]
    [InlineData("-Strong:G26", GrammarErrorCodes.UnknownKey)]
    [InlineData("Verbform:finit", GrammarErrorCodes.UnknownKey)]
    [InlineData("dt.Kasus:akk", GrammarErrorCodes.UnknownKey)]
    [InlineData("Kasus:xyz", GrammarErrorCodes.UnknownValue)]
    [InlineData("Kasus:", GrammarErrorCodes.UnknownValue)]
    [InlineData("", GrammarErrorCodes.EmptyQuery)]
    [InlineData("   ", GrammarErrorCodes.EmptyQuery)]
    [InlineData("-agape", GrammarErrorCodes.UnknownWord)]
    public void InvalidQueries_AnswerWithTheCode(string query, string code) =>
        GrammarQueryParser.Parse(query).Error.ShouldNotBeNull().Code.ShouldBe(code);

    [Fact]
    public void ErrorNamesTheOffendingToken()
    {
        var error = GrammarQueryParser.Parse("agape Kasus:nom Foo:bar").Error;

        error.ShouldNotBeNull();
        error.Token.ShouldBe("Foo:bar");
        error.Candidates.ShouldNotBeNull().ShouldContain("Kasus");
    }

    [Fact]
    public void Limits_FourSlots_TwelveFilters()
    {
        Ok("a b c d").Slots.Count.ShouldBe(4);
        var slots = GrammarQueryParser.Parse("a b c d e").Error.ShouldNotBeNull();
        slots.Code.ShouldBe(GrammarErrorCodes.TooManySlots);
        slots.Token.ShouldBe("e");

        Ok(string.Join(' ', Enumerable.Repeat("Kasus:nom", 12))).Slots.Single().Filters.Count.ShouldBe(12);
        var filters = GrammarQueryParser.Parse(string.Join(' ', Enumerable.Repeat("Kasus:nom", 13))).Error.ShouldNotBeNull();
        filters.Code.ShouldBe(GrammarErrorCodes.TooManyFilters);
    }

    [Fact]
    public void ExplicitWordKeys_OpenASlotWithAForcedReading()
    {
        var slots = Ok("Strong:G26 Lemma:agape Kasus:nom").Slots;

        slots[0].Mode.ShouldBe(GrammarWordMode.Strongs);
        slots[0].Word.ShouldBe("G26");
        slots[1].Mode.ShouldBe(GrammarWordMode.Lemma);
        slots[1].Filters.Count.ShouldBe(1);
    }

    [Fact]
    public void CodeFilter_KeepsUpperCasePatterns()
    {
        var filter = Ok("code:v-2a*,N-NSF").Slots.Single().Filters.Single();

        filter.Key.Kind.ShouldBe(GrammarKeyKind.Code);
        filter.CodePatterns.ShouldBe(["V-2A*", "N-NSF"]);
    }
}
