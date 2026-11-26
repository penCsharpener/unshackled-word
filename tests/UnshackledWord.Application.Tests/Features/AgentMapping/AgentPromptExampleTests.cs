using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.Lemmas;
using UnshackledWord.Domain.Models.Grammar;
using UnshackledWord.Domain.WebApi.Maintainer;
using static UnshackledWord.Application.Tests.Features.AgentMapping.LemmaExampleVerses;

namespace UnshackledWord.Application.Tests.Features.AgentMapping;

/// <summary>
/// The worked examples of the 0018 prompt files (roadmap 0018.9): every example package must be exactly what the package builder makes of the
/// example verse (source text, lemma and gloss aside, they are not in the fixture), and every example answer must pass the validator of its mode.
/// Fixture <c>Fixtures/lemma-prompt-examples.json</c>: the 10 example verses of 0016 exported from uw_0018_test after the 0018.4 role backfill,
/// with the lexicon rows of their forms and the STTS table.
/// </summary>
public class AgentPromptExampleTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly Dictionary<string, int> Books = new() { ["Mk"] = Mark, ["1Mo"] = Genesis, ["Rt"] = Ruth };

    internal sealed class PromptFixture
    {
        public List<ExampleVerse> Verses { get; set; } = [];
        public List<AgentFormLemmaRow> Lexicon { get; set; } = [];
        public List<SttsTag> Stts { get; set; } = [];
    }

    internal sealed record PromptExample(string Ref, string Package, string Answer);

    /// <summary>
    /// Prompt file, run mode, and the soft flags each example answer raises (none may be rejected). <c>lemma-unknown</c> comes from the
    /// fixture's small lexicon (only the forms of the 10 verses), not from a wrong lemma.
    /// </summary>
    public static TheoryData<string, string, string> PromptFiles() => new()
    {
        { "lemma-greek-v1", AgentModes.Lemma, "Mk 1:1: ; Mk 1:6: lemma-unknown; Mk 1:8: ; Mk 1:29: lemma-unknown; Mk 1:35: homograph,lemma-unknown" },
        { "lemma-hebrew-v1", AgentModes.Lemma, "1Mo 1:1: ; 1Mo 1:3: ; Rt 1:4: lemma-unknown; Rt 1:5: homograph,lemma-unknown; Rt 1:22: lemma-unknown" },
        { "greek-v2", AgentModes.Redo, "Mk 1:1: ; Mk 1:6: lemma-unknown; Mk 1:8: ; Mk 1:29: lemma-unknown; Mk 1:35: lemma-unknown" },
        { "hebrew-v2", AgentModes.Redo, "1Mo 1:1: ; 1Mo 1:3: ; Rt 1:4: lemma-unknown; Rt 1:5: lemma-unknown,many-to-one; Rt 1:22: lemma-unknown" },
    };

    [Theory]
    [MemberData(nameof(PromptFiles))]
    public async Task EveryExample_IsTheBuiltPackage_AndItsAnswerPassesTheValidator(string file, string mode, string expectedFlags)
    {
        var fixture = LoadFixture();
        var examples = Examples(file);
        examples.Count.ShouldBe(5, $"{file}: five worked examples");
        AgentModes.TryParse(mode, out var agentMode).ShouldBeTrue();

        var context = Context();

        var flags = new List<string>();
        var details = new List<string>();
        foreach (var example in examples)
        {
            var (book, chapter, verse) = ParseRef(example.Ref);
            var (envelope, map) = await BuildAsync(fixture, book, chapter, verse, agentMode, file);

            var built = JsonNode.Parse(JsonSerializer.Serialize(envelope.Package, Web))!;
            var shown = JsonNode.Parse(example.Package)!;
            var packageId = shown["packageId"]!.GetValue<string>();
            Normalise(built);
            Normalise(shown);
            JsonNode.DeepEquals(built, shown).ShouldBeTrue($"{file} {example.Ref}: the example package differs from the built one.\nbuilt: {built.ToJsonString(Web)}\nshown: {shown.ToJsonString(Web)}");

            using var answer = JsonDocument.Parse(example.Answer);
            var parsed = AgentAnswerParser.Parse(answer.RootElement);
            parsed.PackageId.ShouldBe(packageId, $"{file} {example.Ref}: answer packageId");
            var result = new AgentProposalValidator().Validate(map, parsed, context).Single();

            result.Messages.ShouldBeEmpty($"{file} {example.Ref}: the example answer is rejected");
            result.Proposal.ShouldNotBeNull();
            flags.Add($"{example.Ref}: {string.Join(",", result.Flags.Select(f => f.Code).Distinct().Order())}");
            details.Add(string.Join(" | ", result.Flags.Select(f => f.Message)));
        }

        string.Join("; ", flags).ShouldBe(expectedFlags, string.Join("\n", details));
    }

    [Fact]
    public void TheLemmaPrompts_CarryTheSharedBlock_AndAreServedForLemmaRuns()
    {
        var provider = new AgentPromptProvider(AgentPromptProvider.ResolveDirectory(new TestConfiguration(new Dictionary<string, string?>())));
        var greek = provider.Get(AgentPromptProvider.LanguageFor(Mark, AgentMode.Lemma));
        var hebrew = provider.Get(AgentPromptProvider.LanguageFor(Genesis, AgentMode.Lemma));

        greek.Version.ShouldBe("lemma-greek-v1");
        hebrew.Version.ShouldBe("lemma-hebrew-v1");
        foreach (var prompt in new[] { greek, hebrew, provider.Get("greek"), provider.Get("hebrew") })
        {
            prompt.StaticPrefix.ShouldContain("# Lemma rules (shared block, roadmap 0018)");
            prompt.StaticPrefix.ShouldContain("# Link roles (shared block, roadmap 0018)");
            prompt.StaticPrefix.ShouldNotContain(AgentAnswerSchema.Placeholder);
            prompt.StaticPrefix.ShouldNotMatch(@"\b20\d\d-\d\d-\d\d\b", "no dates in the cached prefix");
        }

        greek.StaticPrefix.ShouldContain(AgentAnswerSchema.LemmaJson);
        Shared(greek.StaticPrefix).ShouldBe(Shared(hebrew.StaticPrefix), "the shared block is pasted unchanged into every 0018 prompt");
        Shared(provider.Get("greek").StaticPrefix).ShouldBe(Shared(greek.StaticPrefix));
        Shared(provider.Get("hebrew").StaticPrefix).ShouldBe(Shared(greek.StaticPrefix));
    }

    // ---- mapping modes with roles (prompt v2) ---------------------------------------------------------------------------------------

    [Fact]
    public async Task MappingV2_Package_CarriesRolesAndMorphemes_V1PackageDoesNot()
    {
        var fixture = LoadFixture();
        var (v2, map2) = await BuildAsync(fixture, Genesis, 1, 3, AgentMode.Redo, "hebrew-v2");
        var (v1, map1) = await BuildAsync(fixture, Genesis, 1, 3, AgentMode.Redo, "hebrew-v1");

        map2.Roles.ShouldBeTrue();
        map1.Roles.ShouldBeFalse();
        v2.Package.Verses[0].Hints.ShouldContain(h => h.Length == 4 && h[2] == "head" && h[3] == "H0559");
        v1.Package.Verses[0].Hints.ShouldAllBe(h => h.Length == 2);
        v2.Package.Verses[0].Src[0].Length.ShouldBe(7);
        v1.Package.Verses[0].Src[0].Length.ShouldBe(6);
    }

    [Fact]
    public async Task MappingV2_RolesAndPartLemmas_ReachTheProposal()
    {
        var fixture = LoadFixture();
        var (_, map) = await BuildAsync(fixture, Ruth, 1, 22, AgentMode.Redo, "hebrew-v2");
        var answer = Examples("hebrew-v2").Single(e => e.Ref == "Rt 1:22").Answer;
        using var json = JsonDocument.Parse(answer);

        var result = new AgentProposalValidator().Validate(map, AgentAnswerParser.Parse(json.RootElement), Context()).Single();

        result.Messages.ShouldBeEmpty();
        var verse = map.Verses["v1"];
        var links = result.Proposal!.Links;
        links.Where(l => !l.IsAddedWord).ShouldAllBe(l => l.Role != null);
        links.Single(l => l.ElbWordId == verse.German["g14"].Id).StrongsNumber.ShouldBe("H9009");
        links.Single(l => l.ElbWordId == verse.German["g5"].Id).Role.ShouldBe("unit");
        links.Where(l => l.ElbWordId == verse.German["g28"].Id).OrderBy(l => l.PartOrder).Select(l => l.PartLemma).ShouldBe(["Gerste", "Ernte"]);
    }

    [Fact]
    public async Task MappingV2_TwoHeadsOnOneMorpheme_Rejected_V1AnswerWithRoles_Rejected()
    {
        var fixture = LoadFixture();
        var (_, map2) = await BuildAsync(fixture, Mark, 1, 8, AgentMode.Redo, "greek-v2");
        var (_, map1) = await BuildAsync(fixture, Mark, 1, 8, AgentMode.Redo, "greek-v1");

        // habe (g3) as a second head of ἐβάπτισα next to getauft (g7).
        var twoHeads = new AgentProposalValidator().Validate(map2, AgentTestData.Answer("""{"v":"v1","links":[["g3","s3","head"]]}"""), Context()).Single();
        twoHeads.Status.ShouldBe(AgentVerseStatus.Rejected);

        var badRole = new AgentProposalValidator().Validate(map2, AgentTestData.Answer("""{"v":"v1","links":[["g3","s3","aux"]]}"""), Context()).Single();
        badRole.Messages.ShouldContain(m => m.Contains("role must be"));

        var v1 = new AgentProposalValidator().Validate(map1, AgentTestData.Answer("""{"v":"v1","links":[["g3","s3","unit"]]}"""), Context()).Single();
        v1.Messages.ShouldContain(m => m.Contains("this prompt version has no roles"));

        var plain = new AgentProposalValidator().Validate(map1, AgentTestData.Answer("""{"v":"v1","links":[["g3","s3"]]}"""), Context()).Single();
        plain.Messages.ShouldBeEmpty();
        plain.Proposal!.Links.ShouldAllBe(l => l.Role == null);
    }

    private static AgentValidationContext Context()
    {
        var fixture = LoadFixture();
        return new AgentValidationContext
        {
            Stts = new SttsCatalog(fixture.Stts),
            KnownLemmas = fixture.Lexicon.Select(l => l.Lemma).Concat(fixture.Verses.SelectMany(v => v.German).Select(g => g.Lemma ?? string.Empty)).ToHashSet(StringComparer.Ordinal),
            Options = AgentTestData.Options(),
        };
    }

    private static string Shared(string prompt)
    {
        var start = prompt.IndexOf("# Lemma rules (shared block", StringComparison.Ordinal);
        var roles = prompt.IndexOf("# Link roles (shared block", StringComparison.Ordinal);
        var end = prompt.IndexOf("\n# ", roles + 1, StringComparison.Ordinal);
        var specifics = prompt.IndexOf("\nGreek specifics:", roles, StringComparison.Ordinal) is >= 0 and var g ? g : prompt.IndexOf("\nHebrew specifics:", roles, StringComparison.Ordinal);
        return prompt[start..Math.Min(end < 0 ? prompt.Length : end, specifics < 0 ? prompt.Length : specifics)].Trim();
    }

    private static (int Book, int Chapter, int Verse) ParseRef(string reference)
    {
        var match = Regex.Match(reference, @"^(?<book>\S+) (?<c>\d+):(?<v>\d+)$");
        match.Success.ShouldBeTrue(reference);
        return (Books[match.Groups["book"].Value], int.Parse(match.Groups["c"].Value), int.Parse(match.Groups["v"].Value));
    }

    /// <summary>Package id and the source columns the fixture leaves out (text, lemma, gloss) are not compared.</summary>
    private static void Normalise(JsonNode package)
    {
        package["packageId"] = "p";
        foreach (var verse in package["verses"]!.AsArray())
        {
            foreach (var row in verse!["src"]!.AsArray())
            {
                var cells = row!.AsArray();
                cells[1] = string.Empty;
                cells[2] = null;
                cells[4] = null;
            }
        }
    }

    internal static List<PromptExample> Examples(string file)
    {
        var path = Path.Combine(AgentPromptProvider.ResolveDirectory(new TestConfiguration(new Dictionary<string, string?>())), file + ".md");
        var text = File.ReadAllText(path).Replace("\r\n", "\n");
        var sections = Regex.Split(text, @"\n## Example \d+: ").Skip(1);
        var examples = new List<PromptExample>();
        foreach (var section in sections)
        {
            // The heading line: "Mk 1:1 [UNCONFIRMED]", or "Mk 1:1" once the maintainer has confirmed the example.
            var heading = section[..section.IndexOf('\n')];
            var reference = (heading.IndexOf(" [", StringComparison.Ordinal) is >= 0 and var tag ? heading[..tag] : heading).Trim();
            var blocks = Regex.Matches(section, "```json\n(.*?)\n```", RegexOptions.Singleline).Select(m => m.Groups[1].Value).ToList();
            blocks.Count.ShouldBe(2, $"{file} {reference}: a package and an answer");
            examples.Add(new PromptExample(reference, blocks[0], blocks[1]));
        }

        return examples;
    }

    private static async Task<(AgentPackageEnvelope Envelope, AgentPackageMap Map)> BuildAsync(PromptFixture fixture, int book, int chapter, int verse, AgentMode mode, string promptVersion)
    {
        var example = fixture.Verses.Single(v => v.Book == book && v.Chapter == chapter && v.Verse == verse);
        var data = Substitute.For<IAgentDataRepository>();
        data.GetRangeAsync(book, chapter, verse, chapter, verse, Arg.Any<CancellationToken>()).Returns(new AgentRangeData
        {
            GermanWords = example.German,
            SourceWords = example.Source,
            Mappings = example.Mappings,
        });
        data.GetFormLemmasAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns(fixture.Lexicon);
        data.GetConfirmedPairsAsync(Arg.Any<bool>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns([]);

        var store = Substitute.For<IAgentStoreRepository>();
        store.CreateRunAsync(Arg.Any<AgentRunInsert>(), Arg.Any<CancellationToken>()).Returns(77);
        var maps = new List<AgentPackageInsert>();
        store.InsertPackageAsync(Arg.Do<AgentPackageInsert>(maps.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var built = await new AgentPackageBuilder(data, store, Substitute.For<IConfiguration>(), TimeProvider.System).BuildAsync(new AgentBuildRequest
        {
            Range = new AgentRange { BibleBookId = book, FromChapter = chapter, FromVerse = verse, ToChapter = chapter, ToVerse = verse },
            Mode = mode,
            UserId = 3,
            PromptVersion = promptVersion,
        });

        built.ShouldNotBeNull();
        built.Packages.Count.ShouldBe(1, $"{book} {chapter}:{verse} is one package");
        return (built.Packages[0], AgentPackageMap.FromJson(maps.Single().LocalIdMapJson));
    }

    private static PromptFixture LoadFixture([CallerFilePath] string here = "")
    {
        var path = Path.Combine(Path.GetDirectoryName(here)!, "Fixtures", "lemma-prompt-examples.json");
        return JsonSerializer.Deserialize<PromptFixture>(File.ReadAllText(path), Web) ?? new PromptFixture();
    }
}
