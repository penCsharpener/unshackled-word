using System.Text.Json;
using UnshackledWord.Application.Tests.Features.AgentMapping;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.Vocab.Forms;

namespace UnshackledWord.Application.Tests.Features.Vocab;

/// <summary>Roadmap 0022.13: validator, package/answer and prompt files of the dictionary-form agent run.</summary>
public class VocabFormTests
{
    [Theory]
    [InlineData("λόγος", "λόγος, -ου, ὁ")]
    [InlineData("μέσος", "μέσος, -η, -ον")]
    [InlineData("λόγος", "λογος, -ου, ὁ")] // accents do not matter
    [InlineData("καί", "καί")]
    [InlineData("πᾶς", "πᾶς, πᾶσα, πᾶν")]
    [InlineData("אָב", "אָב, אָבוֹת")]
    [InlineData("אָב", "אָב (אָבוֹת)")]
    public void Check_AcceptsFormsThatStartWithTheLemma(string lemma, string form) =>
        VocabFormValidator.Check(lemma, form).ShouldBeNull();

    [Theory]
    [InlineData("λόγος", "ἀγάπη, -ης, ἡ")]
    [InlineData("λόγος", "ὁ λόγος, -ου")]
    [InlineData("λόγος", "-ου, ὁ")]
    [InlineData("λόγος", "")]
    [InlineData("λόγος", "   ")]
    [InlineData("λόγος", null)]
    [InlineData("λόγος", "λόγος, -ου,\nὁ")]
    public void Check_RejectsOtherFormsEmptyAndMultiLine(string lemma, string? form) =>
        VocabFormValidator.Check(lemma, form).ShouldNotBeNull();

    [Fact]
    public void Check_RejectsMoreThan120Characters()
    {
        VocabFormValidator.Check("λόγος", "λόγος, " + new string('x', 113)).ShouldBeNull(); // exactly 120
        VocabFormValidator.Check("λόγος", "λόγος, " + new string('x', 114)).ShouldNotBeNull();
    }

    [Theory]
    [InlineData("G:N-M", "M")]
    [InlineData("G:N-F", "F")]
    [InlineData("H:N-N", "N")]
    [InlineData("G:A-NUI", null)]
    [InlineData("G:V-", null)]
    [InlineData(null, null)]
    public void GenderOf_ReadsTheNounMorphology(string? pos, string? gender) =>
        VocabFormPackage.GenderOf(pos).ShouldBe(gender);

    [Fact]
    public void Build_CarriesLemmaPosGenderAndCleanLexiconPerEntry()
    {
        var entries = new[]
        {
            new VocabFormInput(101, "λόγος", "G:N-M", "<b>λόγος</b>, -ου, ὁ\n word, <i>speech</i>"),
            new VocabFormInput(102, "καί", "G:CONJ", null),
        };

        using var doc = JsonDocument.Parse(VocabFormPackage.Build('G', entries));
        var rows = doc.RootElement.GetProperty("entries");

        doc.RootElement.GetProperty("lang").GetString().ShouldBe("greek");
        rows.GetArrayLength().ShouldBe(2);
        rows[0][0].GetString().ShouldBe("e1");
        rows[0][1].GetString().ShouldBe("λόγος");
        rows[0][2].GetString().ShouldBe("G:N-M");
        rows[0][3].GetString().ShouldBe("M");
        rows[0][4].GetString().ShouldBe("λόγος , -ου, ὁ word, speech");
        rows[1][0].GetString().ShouldBe("e2");
        rows[1][3].ValueKind.ShouldBe(JsonValueKind.Null);
        rows[1][4].ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public void ParseAnswer_MapsLocalIdsBack_DropsUnknownDuplicateAndMalformedRows()
    {
        var entries = new[] { new VocabFormInput(101, "λόγος", null, null), new VocabFormInput(102, "καί", null, null) };
        var json = """{"forms":[["e2","καί"],["e1","λόγος, -ου, ὁ"],["e1","zweite"],["e9","x"],["e1"],["e2",5]]}""";

        var rows = VocabFormPackage.ParseAnswer(json, entries);

        rows.ShouldBe([new VocabFormAnswerRow(102, "καί"), new VocabFormAnswerRow(101, "λόγος, -ου, ὁ")]);
        VocabFormPackage.ParseAnswer("""{"other":1}""", entries).ShouldBeEmpty();
        Should.Throw<JsonException>(() => VocabFormPackage.ParseAnswer("nope", entries));
    }

    [Theory]
    [InlineData("vocab-form-greek", "vocab-form-greek-v1")]
    [InlineData("vocab-form-hebrew", "vocab-form-hebrew-v1")]
    public void PromptFiles_LoadWithTheAnswerSchemaAndTheInputColumns(string language, string version)
    {
        var provider = new AgentPromptProvider(AgentPromptProvider.ResolveDirectory(new TestConfiguration(new Dictionary<string, string?>())));

        var prompt = provider.Get(language);

        prompt.Version.ShouldBe(version);
        prompt.StaticPrefix.ShouldContain(AgentAnswerSchema.VocabFormJson.Trim());
        prompt.StaticPrefix.ShouldNotContain(AgentAnswerSchema.Placeholder);
        prompt.StaticPrefix.ShouldContain("[id, lemma, pos, gender, lexicon]");
        prompt.StaticPrefix.ShouldContain("120");
        provider.GetVersion(language, version).StaticPrefix.ShouldBe(prompt.StaticPrefix);
        VocabFormPackage.LanguageOf(language.EndsWith("greek", StringComparison.Ordinal) ? 'G' : 'H').ShouldBe(language);
    }

    [Fact]
    public void MappingPromptLookup_IgnoresTheVocabFormFiles()
    {
        var provider = new AgentPromptProvider(AgentPromptProvider.ResolveDirectory(new TestConfiguration(new Dictionary<string, string?>())));

        provider.Get("greek").Version.ShouldNotStartWith("vocab-form");
        provider.Get("hebrew").Version.ShouldNotStartWith("vocab-form");
    }
}
