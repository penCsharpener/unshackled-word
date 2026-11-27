using System.Text.Json;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.Vocab;
using UnshackledWord.Application.Features.Vocab.Roots;
using UnshackledWord.Application.Tests.Features.AgentMapping;

namespace UnshackledWord.Application.Tests.Features.Vocab;

/// <summary>Roadmap 0030.4: validator rules, package/answer and prompt files of the vocab root agent run.</summary>
public class VocabRootRunTests
{
    private static readonly VocabRootValidationContext Context = new(
        [
            new("G3140", "μαρτυρεω", "μαρτυρέω", true),
            new("G4198", "πορευομαι", "πορεύω", true),
            new("G4198", "πορευω", "πορεύω", true),
            new("G2192", "εχω", "ἔχω", true),
            new("G2064", "ερχομαι", "ἔρχομαι", true),
            new("G3056", "λογοσ", "λόγος", true),
            new("G3089", "λυω", "λύω", true),
            new("G4901", "συνεπιμαρτυρεω", "συνεπιμαρτυρέω", true),
            new("G1607", "εκπορευομαι", "ἐκπορεύομαι", true),
            new("G0576", "απεχω", "ἀπέχω", true),
        ],
        new Dictionary<string, string> { ["H3789"] = "כתב", ["H8104"] = "שמר" });

    private static VocabRootInput Greek(string lemma, string key, VocabRootExisting? existing = null) => new(1, lemma, key, "G:V-", null, null, existing);

    private static VocabRootAnswerRow Answer(string? rootKey, string[]? prefixes = null, bool simple = false, string? letters = null, string confidence = "high") =>
        new(1, simple, prefixes ?? [], rootKey, letters, confidence, null);

    private static VocabRootVerdict CheckGreek(VocabRootInput entry, VocabRootAnswerRow answer, bool checkPrefill = false) =>
        VocabRootAnswerValidator.Check('G', entry, answer, Context, checkPrefill);

    [Fact]
    public void Greek_TwoPrefixes_AreAcceptedWithTheDictionaryForms()
    {
        var verdict = CheckGreek(Greek("συνεπιμαρτυρέω", "G4901"), Answer("G3140", ["G4862", "G1909"]));

        verdict.Kind.ShouldBe(VocabRootVerdictKind.Accept);
        verdict.Plan!.RootText.ShouldBe("μαρτυρέω");
        verdict.Plan.RootKey.ShouldBe("G3140");
        verdict.Plan.PrefixKeys.ShouldBe(["G4862", "G1909"]);
        verdict.Plan.Prefixes.ShouldBe("σύν + ἐπί");
        verdict.Plan.IsSimple.ShouldBeFalse();
    }

    [Theory]
    [InlineData("ἐκπορεύω", "G1607", "G1537", "G4198")] // the root keeps its own voice: πορεύω and πορεύομαι are one word
    [InlineData("ἀπέχω", "G0576", "G0575", "G2192")] // elision: ἀπό + ἔχω
    [InlineData("ἐξέρχομαι", "G1831", "G1537", "G2064")] // assimilated form ἐξ
    public void Greek_ElisionAndAssimilation_AreAccepted(string lemma, string key, string prefix, string root) =>
        CheckGreek(Greek(lemma, key), Answer(root, [prefix])).Kind.ShouldBe(VocabRootVerdictKind.Accept);

    [Fact]
    public void Greek_SimpleWord_IsItsOwnRootWithoutPrefixes()
    {
        var verdict = CheckGreek(Greek("λόγος", "G3056"), Answer(null, simple: true));

        verdict.Kind.ShouldBe(VocabRootVerdictKind.Accept);
        verdict.Plan!.RootText.ShouldBe("λόγος");
        verdict.Plan.RootKey.ShouldBe("G3056");
        verdict.Plan.PrefixKeys.ShouldBeEmpty();
        verdict.Plan.IsSimple.ShouldBeTrue();
    }

    [Fact]
    public void Greek_RootKeyOfTheWordWithoutPrefixes_IsSimple() =>
        CheckGreek(Greek("λύω", "G3089"), Answer("G3089")).Plan!.IsSimple.ShouldBeTrue();

    [Fact]
    public void Greek_InventedPrefixKey_IsRejected()
    {
        var verdict = CheckGreek(Greek("ἐκπορεύω", "G1607"), Answer("G4198", ["G9999"]));

        verdict.Kind.ShouldBe(VocabRootVerdictKind.Rejected);
        verdict.Reason.ShouldContain("G9999");
    }

    [Fact]
    public void Greek_NonPrepositionKeyAsPrefix_IsRejected() =>
        CheckGreek(Greek("ἐκπορεύω", "G1607"), Answer("G4198", ["G3056"])).Kind.ShouldBe(VocabRootVerdictKind.Rejected);

    [Fact]
    public void Greek_RootKeyNotInTheLexicon_IsRejected() =>
        CheckGreek(Greek("ἐκπορεύω", "G1607"), Answer("G7777", ["G1537"])).Kind.ShouldBe(VocabRootVerdictKind.Rejected);

    [Fact]
    public void Greek_RootKeyMissing_IsRejected() =>
        CheckGreek(Greek("ἐκπορεύω", "G1607"), Answer(null, ["G1537"])).Kind.ShouldBe(VocabRootVerdictKind.Rejected);

    [Fact]
    public void Greek_RootIsTheWordItselfWhilePrefixesAreListed_IsRejected()
    {
        var verdict = CheckGreek(Greek("ἐκπορεύω", "G1607"), Answer("G1607", ["G1537"]));

        verdict.Kind.ShouldBe(VocabRootVerdictKind.Rejected);
        verdict.Reason.ShouldContain("itself");
    }

    [Fact]
    public void Greek_SimpleWithPrefixes_IsRejected() =>
        CheckGreek(Greek("λύω", "G3089"), Answer(null, ["G1537"], simple: true)).Kind.ShouldBe(VocabRootVerdictKind.Rejected);

    [Fact]
    public void Greek_PrefixAndRootThatDoNotFormTheWord_AreFlagged()
    {
        var verdict = CheckGreek(Greek("ἐκπορεύω", "G1607"), Answer("G3056", ["G1537"]));

        verdict.Kind.ShouldBe(VocabRootVerdictKind.Flagged);
        verdict.Plan.ShouldBeNull();
    }

    [Fact]
    public void Greek_WrongPrefixForTheStartOfTheWord_IsFlagged() =>
        CheckGreek(Greek("ἐκπορεύω", "G1607"), Answer("G4198", ["G4862"])).Kind.ShouldBe(VocabRootVerdictKind.Flagged);

    [Fact]
    public void Greek_RootOtherThanTheWordWithoutPrefixes_IsFlagged() =>
        CheckGreek(Greek("λόγος", "G3056"), Answer("G3089")).Kind.ShouldBe(VocabRootVerdictKind.Flagged);

    [Fact]
    public void Greek_LowConfidence_IsFlaggedEvenWhenValid() =>
        CheckGreek(Greek("λόγος", "G3056"), Answer(null, simple: true, confidence: "low")).Kind.ShouldBe(VocabRootVerdictKind.Flagged);

    [Fact]
    public void ManualRow_IsNeverTouched() =>
        CheckGreek(Greek("λόγος", "G3056", new VocabRootExisting("manual", null, [], "λόγος", "G3056")), Answer(null, simple: true), checkPrefill: true)
            .Kind.ShouldBe(VocabRootVerdictKind.Dropped);

    [Fact]
    public void PrefillRow_IsReplacedOnlyWithTheCheckOption()
    {
        var entry = Greek("ἐκπορεύω", "G1607", new VocabRootExisting("prefill", "ἐκ", ["G1537"], "πορεύω", "G4198"));
        var answer = Answer("G4198", ["G1537"]);

        CheckGreek(entry, answer).Kind.ShouldBe(VocabRootVerdictKind.Dropped);
        CheckGreek(entry, answer, checkPrefill: true).Kind.ShouldBe(VocabRootVerdictKind.Accept);
    }

    private static VocabRootVerdict CheckHebrew(string lemma, VocabRootAnswerRow answer) =>
        VocabRootAnswerValidator.Check('H', new VocabRootInput(1, lemma, "H4385", "H:N-M", null, null), answer, Context, false);

    [Fact]
    public void Hebrew_NounOfAVerbRoot_IsAcceptedWithTheVerbKey()
    {
        var verdict = CheckHebrew("מִכְתָּב", Answer("H3789", letters: "כתב"));

        verdict.Kind.ShouldBe(VocabRootVerdictKind.Accept);
        verdict.Plan!.RootText.ShouldBe("כתב");
        verdict.Plan.RootKey.ShouldBe("H3789");
    }

    [Fact]
    public void Hebrew_VowelPointsInTheLettersAreStripped() =>
        CheckHebrew("מִכְתָּב", Answer(null, letters: "כְּתַב")).Plan!.RootText.ShouldBe("כתב");

    [Fact]
    public void Hebrew_RootWithoutAVerb_NeedsNoKey()
    {
        var verdict = CheckHebrew("אֶבֶן", Answer(null, letters: "אבן"));

        verdict.Kind.ShouldBe(VocabRootVerdictKind.Accept);
        verdict.Plan!.RootKey.ShouldBeNull();
    }

    [Theory]
    [InlineData("כ")]
    [InlineData("כתבאבג")]
    [InlineData("abc")]
    public void Hebrew_RootLettersOutsideTwoToFourConsonants_AreRejected(string letters) =>
        CheckHebrew("מִכְתָּב", Answer(null, letters: letters)).Kind.ShouldBe(VocabRootVerdictKind.Rejected);

    [Fact]
    public void Hebrew_MissingLetters_AreRejected() => CheckHebrew("מִכְתָּב", Answer("H3789")).Kind.ShouldBe(VocabRootVerdictKind.Rejected);

    [Fact]
    public void Hebrew_RootKeyThatIsNoVerb_IsRejected() =>
        CheckHebrew("מִכְתָּב", Answer("H4385", letters: "כתב")).Kind.ShouldBe(VocabRootVerdictKind.Rejected);

    [Fact]
    public void Hebrew_RootKeyWithOtherConsonants_IsRejected()
    {
        var verdict = CheckHebrew("מִכְתָּב", Answer("H8104", letters: "כתב"));

        verdict.Kind.ShouldBe(VocabRootVerdictKind.Rejected);
        verdict.Reason.ShouldContain("שמר");
    }

    [Fact]
    public void Hebrew_Prefixes_AreRejected() => CheckHebrew("מִכְתָּב", Answer(null, ["G1537"], letters: "כתב")).Kind.ShouldBe(VocabRootVerdictKind.Rejected);

    [Fact]
    public void Hebrew_SimpleAnswer_UsesTheConsonantsOfTheLemma() =>
        CheckHebrew("אֶבֶן", Answer(null, simple: true)).Plan!.RootText.ShouldBe("אבן");

    [Fact]
    public void Hebrew_SimpleVerb_KeepsItsOwnKeyAsRootKey()
    {
        var plan = VocabRootAnswerValidator.Check('H', new VocabRootInput(1, "כָּתַב", "H3789", "H:V", null, null), Answer(null, simple: true), Context, false).Plan!;

        plan.RootText.ShouldBe("כתב");
        plan.RootKey.ShouldBe("H3789");
        plan.IsSimple.ShouldBeTrue();
    }

    [Fact]
    public void Hebrew_SimpleNoun_HasNoRootKey() =>
        CheckHebrew("אֶבֶן", Answer(null, simple: true)).Plan!.RootKey.ShouldBeNull();

    [Fact]
    public void Package_RoundTripsLocalIdsAndDropsUnknownAndDuplicateRows()
    {
        var entries = new[]
        {
            new VocabRootInput(101, "συνεπιμαρτυρέω", "G4901", "G:V-", "mit bezeugen", "<b>συν-επι-μαρτυρέω</b> x", new VocabRootExisting("prefill", "σύν + ἐπί", ["G4862", "G1909"], "μαρτυρέω", "G3140")),
            new VocabRootInput(102, "λόγος", "G3056", "G:N-M", null, null),
        };

        var package = JsonDocument.Parse(VocabRootPackage.Build('G', entries)).RootElement;
        package.GetProperty("lang").GetString().ShouldBe("greek");
        package.GetProperty("entries")[0][0].GetString().ShouldBe("e1");
        package.GetProperty("entries")[0][5].GetString().ShouldBe("συν-επι-μαρτυρέω x");
        package.GetProperty("entries")[0][6].GetProperty("rootKey").GetString().ShouldBe("G3140");
        package.GetProperty("entries")[1][4].ValueKind.ShouldBe(JsonValueKind.Null);

        var rows = VocabRootPackage.ParseAnswer("""
            {"roots":[
              {"id":"e2","simple":true,"prefixes":[],"confidence":"high"},
              {"id":"e1","prefixes":["G4862"," G1909 "],"rootKey":"G3140","confidence":"low","note":"x"},
              {"id":"e1","simple":true},
              {"id":"e9","simple":true}
            ]}
            """, entries);

        rows.Count.ShouldBe(2);
        rows[0].EntryId.ShouldBe(102);
        rows[0].Simple.ShouldBeTrue();
        rows[0].Prefixes.ShouldBeEmpty();
        rows[1].EntryId.ShouldBe(101);
        rows[1].Prefixes.ShouldBe(["G4862", "G1909"]);
        rows[1].Confidence.ShouldBe("low");
        VocabRootPackage.ParseAnswer("""{"other":1}""", entries).ShouldBeEmpty();
        Should.Throw<JsonException>(() => VocabRootPackage.ParseAnswer("nope", entries));
    }

    [Theory]
    [InlineData("vocab-root-greek", "vocab-root-greek-v1")]
    [InlineData("vocab-root-hebrew", "vocab-root-hebrew-v1")]
    public void PromptFiles_LoadWithTheAnswerSchemaAndTheInputColumns(string language, string version)
    {
        var provider = new AgentPromptProvider(AgentPromptProvider.ResolveDirectory(new TestConfiguration(new Dictionary<string, string?>())));

        var prompt = provider.Get(language);

        prompt.Version.ShouldBe(version);
        prompt.StaticPrefix.ShouldContain(AgentAnswerSchema.VocabRootJson.Trim());
        prompt.StaticPrefix.ShouldNotContain(AgentAnswerSchema.Placeholder);
        prompt.StaticPrefix.ShouldContain("[id, lemma, key, pos, gloss, lexicon, existing, problem]");
        provider.GetVersion(language, version).StaticPrefix.ShouldBe(prompt.StaticPrefix);
        AgentAnswerSchema.ForLanguage(language).ShouldBe(AgentAnswerSchema.VocabRootJson);
        VocabRootPackage.LanguageOf(language.EndsWith("greek", StringComparison.Ordinal) ? 'G' : 'H').ShouldBe(language);
    }

    [Fact]
    public void GreekPrompt_ListsEveryPrepositionKeyOfTheValidatorAndTheWorkedExamples()
    {
        var provider = new AgentPromptProvider(AgentPromptProvider.ResolveDirectory(new TestConfiguration(new Dictionary<string, string?>())));
        var prompt = provider.Get("vocab-root-greek").StaticPrefix;

        foreach (var key in new[] { "G0303", "G0473", "G0575", "G1223", "G1519", "G1537", "G1722", "G1909", "G2596", "G3326", "G3844", "G4012", "G4253", "G4314", "G4862", "G5228", "G5259G" })
        {
            VocabRootPrefill.IsPrepositionKey(key).ShouldBeTrue(key);
            prompt.ShouldContain(key);
        }

        foreach (var example in new[] { "συνεπιμαρτυρέω", "ἐκπορεύω", "λύω", "λόγος" })
        {
            prompt.ShouldContain(example);
        }
    }

    [Fact]
    public void GreekPromptExamples_PassTheValidator()
    {
        CheckGreek(Greek("συνεπιμαρτυρέω", "G4901"), Answer("G3140", ["G4862", "G1909"])).Kind.ShouldBe(VocabRootVerdictKind.Accept);
        CheckGreek(Greek("ἐκπορεύω", "G1607"), Answer("G4198", ["G1537"])).Kind.ShouldBe(VocabRootVerdictKind.Accept);
        CheckGreek(Greek("λύω", "G3089"), Answer(null, simple: true)).Kind.ShouldBe(VocabRootVerdictKind.Accept);
        CheckGreek(Greek("λόγος", "G3056"), Answer(null, simple: true)).Kind.ShouldBe(VocabRootVerdictKind.Accept);
    }

    [Fact]
    public void HebrewPrompt_ShowsTheWorkedExamples()
    {
        var provider = new AgentPromptProvider(AgentPromptProvider.ResolveDirectory(new TestConfiguration(new Dictionary<string, string?>())));
        var prompt = provider.Get("vocab-root-hebrew").StaticPrefix;

        prompt.ShouldContain("מִכְתָּב");
        prompt.ShouldContain("H3789");
        prompt.ShouldContain("אֶבֶן");
    }

    [Fact]
    public void MappingPromptLookup_IgnoresTheVocabRootFiles()
    {
        var provider = new AgentPromptProvider(AgentPromptProvider.ResolveDirectory(new TestConfiguration(new Dictionary<string, string?>())));

        provider.Get("greek").Version.ShouldNotStartWith("vocab-root");
        provider.Get("hebrew").Version.ShouldNotStartWith("vocab-root");
    }
}
