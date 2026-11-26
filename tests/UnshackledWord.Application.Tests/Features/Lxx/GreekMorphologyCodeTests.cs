using System.Text.RegularExpressions;
using UnshackledWord.Application.Features.Lxx;
using UnshackledWord.Domain.Models.Grammar;

namespace UnshackledWord.Application.Tests.Features.Lxx;

public class GreekMorphologyCodeTests
{
    /// <summary>
    /// Every distinct <c>StepGreekWords.Morphology</c> of the dev DB (2026-11-26, 1161 codes), dumped once with
    /// <c>SELECT DISTINCT "Morphology" FROM "unshackled-word"."StepGreekWords" ORDER BY 1</c>.
    /// </summary>
    private static string[] StepCodes() =>
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Features", "Lxx", "Fixtures", "step-greek-morphology-codes.txt"))
            .Where(l => l.Length > 0)
            .ToArray();

    [Fact]
    public void EveryStepNtCode_Parses()
    {
        var codes = StepCodes();
        codes.Length.ShouldBe(1161);

        var failures = codes
            .Select(c => (Code: c, Ok: GreekMorphologyCode.TryParse(c, out _, out var error), Error: error))
            .Where(r => !r.Ok)
            .Select(r => $"{r.Code}: {r.Error}")
            .ToList();

        failures.ShouldBeEmpty();
    }

    /// <summary>The keys come from the lenient decoder: for every STEP code they must be complete for what the code spells out.</summary>
    [Fact]
    public void EveryStepNtCode_HasCompleteKeys()
    {
        var cng = new Regex("^(?:[123][SP]?)?[NGDAV][SP][MFN]$");
        var incomplete = new List<string>();
        foreach (var code in StepCodes())
        {
            var parse = GreekMorphologyCode.Parse(code)!;
            var keys = parse.Keys;
            var ok = keys.PartOfSpeech is not null;
            if (code.StartsWith("V-"))
            {
                ok &= keys.Tense is not null && keys.Voice is not null && keys.Mood is not null;
                ok &= keys.Mood is MorphologyKey.Infinitive or MorphologyKey.Participle || (keys.Person is not null && keys.Number is not null);
            }

            // the segments after the head (verbs: after tense/voice/mood, V-APM-2S is no case + number + gender)
            var segments = code.Split('+')[0].Trim().Split('-').Skip(code.StartsWith("V-") ? 2 : 1);
            if (segments.Any(cng.IsMatch))
            {
                ok &= keys.Case is not null && keys.Number is not null && keys.Gender is not null;
            }

            if (!ok)
            {
                incomplete.Add(code);
            }
        }

        incomplete.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("N-DSF", "Nomen Dativ Singular feminin", "noun dative singular feminine")]
    [InlineData("V-AAI-3S", "Verb Aorist Aktiv Indikativ 3. Person Singular", "verb aorist active indicative 3rd person singular")]
    [InlineData("V-2AAI-3S", "Verb 2. Aorist Aktiv Indikativ 3. Person Singular", "verb second aorist active indicative 3rd person singular")]
    [InlineData("V-PNP-NSM", "Verb Präsens Medium oder Passiv (Deponens) Partizip Nominativ Singular maskulin",
        "verb present middle or passive (deponent) participle nominative singular masculine")]
    [InlineData("V-AAN", "Verb Aorist Aktiv Infinitiv", "verb aorist active infinitive")]
    [InlineData("T-NSM", "Artikel Nominativ Singular maskulin", "article nominative singular masculine")]
    [InlineData("P-1GS", "Personalpronomen 1. Person Genitiv Singular", "personal pronoun 1st person genitive singular")]
    [InlineData("P-GSM", "Personalpronomen Genitiv Singular maskulin", "personal pronoun genitive singular masculine")]
    [InlineData("F-3ASM", "Reflexivpronomen 3. Person Akkusativ Singular maskulin", "reflexive pronoun 3rd person accusative singular masculine")]
    [InlineData("S-1PASF", "Possessivpronomen 1. Person (Besitzer Plural) Akkusativ Singular feminin",
        "possessive pronoun 1st person (possessor plural) accusative singular feminine")]
    [InlineData("A-NSM-C", "Adjektiv Nominativ Singular maskulin Komparativ", "adjective nominative singular masculine comparative")]
    [InlineData("N-GSF-LG", "Nomen Genitiv Singular feminin, Herkunftsbezeichnung (Ort)", "noun genitive singular feminine, gentilic (place)")]
    [InlineData("N-PRI", "Eigenname (indeklinabel)", "proper noun (indeclinable)")]
    [InlineData("A-NUI + G3638", "Zahlwort (indeklinabel), verbunden mit G3638", "numeral (indeclinable), combined with G3638")]
    [InlineData("CONJ", "Konjunktion", "conjunction")]
    [InlineData("PRT-N", "Partikel, verneint", "particle, negative")]
    [InlineData("ADV-C", "Adverb Komparativ", "adverb comparative")]
    [InlineData("V-AOI-2P-ATT", "Verb Aorist Passiv (Deponens) Indikativ 2. Person Plural, attische Form",
        "verb aorist passive (deponent) indicative 2nd person plural, Attic form")]
    public void Describe_GermanAndEnglish(string code, string german, string english)
    {
        var parse = GreekMorphologyCode.Parse(code)!;

        parse.German.ShouldBe(german);
        parse.English.ShouldBe(english);
    }

    [Fact]
    public void Parse_ReturnsStructure()
    {
        var parse = GreekMorphologyCode.Parse("  s-1pasf ")!;

        parse.Code.ShouldBe("S-1PASF");
        parse.Head.ShouldBe("S");
        parse.PronounKind.ShouldBe("possessive");
        parse.PossessorNumber.ShouldBe(MorphologyKey.Plural);
        parse.Keys.Person.ShouldBe(MorphologyKey.First);
        parse.Keys.Case.ShouldBe(MorphologyKey.Accusative);
        parse.Keys.Number.ShouldBe(MorphologyKey.Singular);
        parse.Keys.Gender.ShouldBe(MorphologyKey.Feminine);

        var crasis = GreekMorphologyCode.Parse("PREP+G1537")!;
        crasis.Code.ShouldBe("PREP + G1537");
        crasis.CrasisStrongs.ShouldBe("G1537");
        crasis.Keys.PartOfSpeech.ShouldBe(MorphologyKey.Preposition);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("XYZ")]
    [InlineData("V-XYZ")]
    [InlineData("V")]
    [InlineData("V-AAI")]
    [InlineData("V-AAP-3S")]
    [InlineData("V-AAN-3S")]
    [InlineData("N-DS")]
    [InlineData("N-DSF-Q")]
    [InlineData("N-DSF-C-C")]
    [InlineData("N-NUI")]
    [InlineData("T-PRI")]
    [InlineData("P-3GS")]
    [InlineData("F-ASM")]
    [InlineData("S-1ASF")]
    [InlineData("Z-NSM")]
    [InlineData("PREP + 1537")]
    [InlineData("N--DSF")]
    public void Invalid_IsRejected(string? code)
    {
        GreekMorphologyCode.TryParse(code, out var parse, out var error).ShouldBeFalse();
        parse.ShouldBeNull();
        error.ShouldNotBeNullOrWhiteSpace();
    }
}
