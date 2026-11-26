using System.Text.RegularExpressions;
using UnshackledWord.Application.Features.GrammarSearch;

namespace UnshackledWord.Tooling.WebApi.Tests;

/// <summary>The search vocabulary labels (roadmap 0029.3) must equal the <c>grammar.*</c> labels of the frontend translations, or autocomplete and word detail would name the same value differently.</summary>
public partial class GrammarVocabularyLabelTests
{
    // Vocabulary key name -> category below `grammar` in de.ts / en.ts. Sprache has no translation entry (language names are not grammar labels).
    private static readonly Dictionary<string, string> CategoryByKey = new()
    {
        ["Wortart"] = "partOfSpeech",
        ["Tempus"] = "tense",
        ["Diathese"] = "voice",
        ["Modus"] = "mood",
        ["Person"] = "person",
        ["Kasus"] = "case",
        ["Numerus"] = "number",
        ["Geschlecht"] = "gender",
        ["Steigerung"] = "degree",
        ["Stamm"] = "stem",
        ["Form"] = "hebrewForm",
        ["Status"] = "state",
        ["Verbform"] = "verbForm",
    };

    [Fact]
    public void German_labels_equal_de_ts() => AssertLabels("de.ts", v => v.LabelDe);

    [Fact]
    public void English_labels_equal_en_ts_ignoring_case() => AssertLabels("en.ts", v => v.LabelEn, ignoreCase: true);

    [Fact]
    public void Every_value_key_has_a_translation_category_except_languages()
    {
        foreach (var key in GrammarVocabularyContract.Build().Keys.Where(k => (k.Values.Count > 0 || k.GermanValues.Count > 0) && k.Name != "Sprache"))
        {
            CategoryByKey.ContainsKey(key.Name).ShouldBeTrue($"vocabulary key '{key.Name}' has no category mapping to the grammar block of de.ts");
        }
    }

    [Fact]
    public void German_keys_are_listed_and_kasus_is_withheld()
    {
        var vocabulary = GrammarVocabularyContract.Build();
        vocabulary.GermanKeysEnabled.ShouldBeTrue();

        var german = vocabulary.Keys.Where(k => k.GermanPrefixAllowed).Select(k => k.Name).ToList();
        german.ShouldBe(["Wortart", "Tempus", "Modus", "Person", "Numerus", "Geschlecht", "Steigerung", "Verbform", "STTS", "Wort"], ignoreOrder: true);
        vocabulary.Keys.Where(k => k.GermanOnly).Select(k => k.Name).ShouldBe(["Verbform", "STTS", "Wort"], ignoreOrder: true);
        vocabulary.Keys.Single(k => k.Name == "Kasus").GermanPrefixAllowed.ShouldBeFalse();
        vocabulary.Keys.Single(k => k.Name == "Tempus").GermanValues.Select(v => v.LabelDe).ShouldBe(["Präsens", "Präteritum"]);
    }

    private static void AssertLabels(string fileName, Func<Domain.WebApi.Elberfelder.SearchGrammar.GrammarVocabularyValue, string> label, bool ignoreCase = false)
    {
        var categories = ReadGrammarBlock(fileName);
        foreach (var key in GrammarVocabularyContract.Build().Keys)
        {
            if (!CategoryByKey.TryGetValue(key.Name, out var category)) continue;
            categories.ContainsKey(category).ShouldBeTrue($"{fileName}: category grammar.{category} (vocabulary key {key.Name}) is missing");
            foreach (var value in key.Values.Concat(key.GermanValues))
            {
                var path = $"{category}.{value.Key}";
                categories[category].TryGetValue(value.Key, out var expected).ShouldBeTrue($"{fileName}: label {path} is missing (vocabulary key {key.Name})");
                var actual = label(value);
                var equal = ignoreCase ? string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase) : actual == expected;
                equal.ShouldBeTrue($"{path} ({key.Name}): vocabulary label '{actual}' differs from {fileName} label '{expected}'");
            }
        }
    }

    private static Dictionary<string, Dictionary<string, string>> ReadGrammarBlock(string fileName)
    {
        var root = AppContext.BaseDirectory;
        while (root is not null && !File.Exists(Path.Combine(root, "UnshackledWord.slnx"))) root = Path.GetDirectoryName(root);
        root.ShouldNotBeNull();

        var path = Path.Combine(root, "src", "UnshackledWord.Web", "src", "i18n", fileName);
        File.Exists(path).ShouldBeTrue($"{path} not found");
        var text = File.ReadAllText(path);

        var start = GrammarBlockStart().Match(text);
        start.Success.ShouldBeTrue($"{fileName}: no 'grammar: {{' block found");
        var open = start.Index + start.Length - 1;
        var depth = 0;
        var end = -1;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}' && --depth == 0)
            {
                end = i;
                break;
            }
        }

        (end > open).ShouldBeTrue($"{fileName}: the grammar block is not closed");
        var block = text[(open + 1)..end];

        var result = new Dictionary<string, Dictionary<string, string>>();
        foreach (Match category in CategoryRegex().Matches(block))
        {
            var labels = new Dictionary<string, string>();
            foreach (Match entry in LabelRegex().Matches(category.Groups[2].Value)) labels[entry.Groups[1].Value] = entry.Groups[2].Value;
            result[category.Groups[1].Value] = labels;
        }

        result.ShouldNotBeEmpty($"{fileName}: the grammar block holds no categories");
        return result;
    }

    [GeneratedRegex(@"^\s*grammar:\s*\{", RegexOptions.Multiline)]
    private static partial Regex GrammarBlockStart();

    [GeneratedRegex(@"(\w+):\s*\{([^{}]*)\}")]
    private static partial Regex CategoryRegex();

    [GeneratedRegex("(\\w+):\\s*\"((?:[^\"\\\\]|\\\\.)*)\"")]
    private static partial Regex LabelRegex();
}
