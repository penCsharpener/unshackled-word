using UnshackledWord.Application.Features.GrammarSearch;
using UnshackledWord.Domain.Models.Grammar;

namespace UnshackledWord.Application.Tests.Features.GrammarSearch;

/// <summary>Roadmap 0029.6: Hebrew/Aramaic keys, space-less spellings of multi-word labels, resolving filters on Hebrew codes and the language check of a slot.</summary>
public class GrammarHebrewSearchTests
{
    private static readonly string[] HebrewCodes =
    [
        "HVqp3ms", "HVqw3ms", "HVNp3ms", "HVhp3ms", "HVqa", "HVqrmsa", "HTd/Ncmsa", "HNcmsc", "HNcfsa", "HR/Ncfsa", "HC/Vqw3ms", "HTo", "HNp", "AVqp3ms", "ANcmsa", "ANcmsc",
    ];

    private static readonly List<MorphologyCodeEntry> Entries =
    [
        .. HebrewCodes.Select(c =>
        {
            var decoded = HebrewMorphologyDecoder.Decode(c);
            return new MorphologyCodeEntry(c, decoded.Root, decoded.IsAramaic);
        }),
    ];

    private static GrammarSlotSpec Slot(string query)
    {
        var result = GrammarQueryParser.Parse(query);
        result.Error.ShouldBeNull();
        return result.Slots.Single();
    }

    private static IReadOnlyList<string> Resolve(string query) =>
        [.. GrammarFilterResolver.Resolve(Slot(query).Filters, Entries, GrammarVocabulary.LanguageHebrew).Order(StringComparer.Ordinal)];

    [Theory]
    [InlineData("Stamm:qal", MorphologyKey.Qal)]
    [InlineData("Stamm:Nifal", MorphologyKey.Niphal)]
    [InlineData("Stamm:niphal", MorphologyKey.Niphal)]
    [InlineData("Stamm:hiphil", MorphologyKey.Hiphil)]
    [InlineData("Stamm:hophal", MorphologyKey.Hophal)]
    [InlineData("Stamm:hithpael", MorphologyKey.Hithpael)]
    [InlineData("Stamm:andererStamm", MorphologyKey.Other)]
    [InlineData("Stamm:anderer", MorphologyKey.Other)]
    [InlineData("Form:qatal", MorphologyKey.Perfect)]
    [InlineData("Form:Perfekt", MorphologyKey.Perfect)]
    [InlineData("Form:Perfekt(Qatal)", MorphologyKey.Perfect)]
    [InlineData("Form:jiqtol", MorphologyKey.Imperfect)]
    [InlineData("Form:weqatal", MorphologyKey.SequentialPerfect)]
    [InlineData("Form:wayyiqtol", MorphologyKey.SequentialImperfect)]
    [InlineData("Form:InfAbs", MorphologyKey.InfinitiveAbsolute)]
    [InlineData("Form:Infinitivusabsolutus", MorphologyKey.InfinitiveAbsolute)]
    [InlineData("Form:InfConstr", MorphologyKey.InfinitiveConstruct)]
    [InlineData("Form:PartizipAktiv", MorphologyKey.ParticipleActive)]
    [InlineData("Form:PartPass", MorphologyKey.ParticiplePassive)]
    [InlineData("Status:absolutus", MorphologyKey.Absolute)]
    [InlineData("Status:constructus", MorphologyKey.Construct)]
    [InlineData("Status:determiniert", MorphologyKey.Determined)]
    [InlineData("Status:StatusAbsolutus", MorphologyKey.Absolute)]
    public void HebrewValues_AreFoundByLabelAliasAndSpacelessSpelling(string token, string expected) =>
        Slot(token).Filters.Single().Values.Single().Id.ShouldBe(expected);

    [Theory]
    [InlineData("Tempus:FuturII", MorphologyKey.FuturePerfect)]
    [InlineData("Tempus:Futur2", MorphologyKey.FuturePerfect)]
    [InlineData("Tempus:futur", MorphologyKey.Future)]
    [InlineData("Diathese:MediumPassiv", MorphologyKey.MiddleOrPassive)]
    [InlineData("Diathese:MediumoderPassiv", MorphologyKey.MiddleOrPassive)]
    [InlineData("Diathese:medium", MorphologyKey.Middle)]
    public void GreekMultiWordValues_CanBeTypedWithoutSpaces(string token, string expected) =>
        Slot(token).Filters.Single().Values.Single().Id.ShouldBe(expected);

    [Fact]
    public void EveryValueWithWhitespaceOrPunctuation_IsReachableAsSpacelessAlias_AndListedInTheVocabulary()
    {
        foreach (var key in GrammarVocabulary.Keys.Where(k => k.Kind == GrammarKeyKind.Morphology))
        {
            foreach (var value in key.Values)
            {
                var spaceless = string.Concat(value.De.Where(char.IsLetterOrDigit));
                var parsed = GrammarQueryParser.Parse($"{key.Name}:{spaceless}");
                parsed.Error.ShouldBeNull($"{key.Name}:{spaceless}");
                parsed.Slots.Single().Filters.Single().Values.ShouldBe([value], $"{key.Name}:{spaceless}");
                if (spaceless != value.De) GrammarVocabulary.ListedAliases(value).ShouldContain(spaceless, $"{key.Name}:{value.De}");
            }
        }
    }

    [Fact]
    public void ExactSpacelessLabelWinsOverPrefix_ButAmbiguousPrefixesStillAsk()
    {
        GrammarQueryParser.Parse("Form:p").Error!.Code.ShouldBe(GrammarErrorCodes.AmbiguousValue);
        GrammarQueryParser.Parse("Stamm:hi").Error!.Code.ShouldBe(GrammarErrorCodes.AmbiguousValue);
        GrammarQueryParser.Parse("Form:imp").Error!.Candidates.ShouldBe(["Imperfekt (Jiqtol)", "Imperativ"]);
    }

    [Fact]
    public void Vocabulary_ListsTheHebrewKeysWithTheirLanguages()
    {
        var keys = GrammarVocabularyContract.Build().Keys;

        foreach (var name in new[] { "Stamm", "Form", "Status" })
        {
            keys.Single(k => k.Name == name).Languages.ShouldBe(["hebrew", "aramaic"]);
        }

        keys.Single(k => k.Name == "Tempus").Languages.ShouldBe(["greek"]);
        keys.Single(k => k.Name == "Wortart").Languages.ShouldBe(["greek", "hebrew", "aramaic"]);
        keys.Single(k => k.Name == "Tempus").Values.Single(v => v.Key == MorphologyKey.FuturePerfect).Aliases.ShouldContain("FuturII");
    }

    [Fact]
    public void Resolve_UsesTheRootSegmentOfMultiSegmentCodes()
    {
        Resolve("Wortart:substantiv Status:absolutus Sprache:hebräisch").ShouldBe(["HNcfsa", "HR/Ncfsa", "HTd/Ncmsa"]);
        Resolve("Wortart:substantiv Status:absolutus Sprache:aramäisch").ShouldBe(["ANcmsa"]);
        Resolve("Wortart:substantiv Geschlecht:maskulin Sprache:hebräisch").ShouldBe(["HNcmsc", "HTd/Ncmsa"]);
        Resolve("Wortart:verb Stamm:qal Form:wajjiqtol").ShouldBe(["HC/Vqw3ms", "HVqw3ms"]);
    }

    [Fact]
    public void Resolve_VerbFeatures()
    {
        Resolve("Stamm:qal Person:3 Numerus:singular").ShouldBe(["AVqp3ms", "HC/Vqw3ms", "HVqp3ms", "HVqw3ms"]);
        Resolve("Stamm:nifal,hifil").ShouldBe(["HVNp3ms", "HVhp3ms"]);
        Resolve("Form:PartAkt").ShouldBe(["HVqrmsa"]);
        Resolve("Form:InfAbs").ShouldBe(["HVqa"]);
    }

    [Fact]
    public void Resolve_Language_SeparatesAramaicFromHebrew()
    {
        Resolve("Sprache:aramäisch").ShouldBe(["ANcmsa", "ANcmsc", "AVqp3ms"]);
        Resolve("Sprache:hebräisch").ShouldNotContain("AVqp3ms");
        Resolve("Sprache:hebräisch").Count.ShouldBe(HebrewCodes.Length - 3);
        Resolve("Sprache:griechisch").ShouldBeEmpty();
        Resolve("Sprache:aramäisch Stamm:qal").ShouldBe(["AVqp3ms"]);
    }

    [Fact]
    public void Resolve_KeyOfOtherLanguage_MatchesNothing_AlsoWhenNegated()
    {
        Resolve("Tempus:aor").ShouldBeEmpty();
        Resolve("-Tempus:aor").ShouldBeEmpty();
        Resolve("Kasus:nom").ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_CodeFilter_MatchesTheWholeCode()
    {
        Resolve("Code:HTd/N*").ShouldBe(["HTd/Ncmsa"]);
        Resolve("Code:HVq*").ShouldBe(["HVqa", "HVqp3ms", "HVqrmsa", "HVqw3ms"]);
    }
}
