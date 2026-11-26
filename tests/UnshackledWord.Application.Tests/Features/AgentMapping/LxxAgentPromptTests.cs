using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.AgentMapping.Lxx;

namespace UnshackledWord.Application.Tests.Features.AgentMapping;

/// <summary>
/// The prompt of run mode <c>lxx-tag</c> (roadmap 0028.7): a byte-stable prefix without volatile content, and worked examples whose package is exactly
/// what the builder makes of the fixture verse and whose answer passes the <c>lxx</c> validator branch.
/// </summary>
public class LxxAgentPromptTests
{
    private const string File = "lxx-tag-v1";

    private static AgentPromptProvider Provider() => new(AgentPromptProvider.ResolveDirectory(new TestConfiguration(new Dictionary<string, string?>())));

    [Fact]
    public void ThePrompt_IsServedForLxxRuns_AndHasAStablePrefixWithoutVolatileContent()
    {
        var language = AgentPromptProvider.LanguageFor(LxxAgentTestData.Ruth, AgentMode.LxxTag);
        language.ShouldBe(AgentPromptProvider.LxxTagLanguage);

        var provider = Provider();
        var prompt = provider.Get(language);

        prompt.Version.ShouldBe(File);
        provider.Get(language).StaticPrefix.ShouldBe(prompt.StaticPrefix, "byte-identical on every read (provider prefix cache)");
        provider.GetVersion(language, File).StaticPrefix.ShouldBe(prompt.StaticPrefix);
        AgentPromptProvider.CarriesRoles(prompt.Version).ShouldBeFalse();
        prompt.StaticPrefix.ShouldContain(AgentAnswerSchema.LxxJson);
        prompt.StaticPrefix.ShouldNotContain(AgentAnswerSchema.Placeholder);
        prompt.StaticPrefix.ShouldNotContain(AgentAnswerSchema.Json, Case.Sensitive, "the mapping schema is not the LXX one");
        prompt.StaticPrefix.ShouldContain("[UNCONFIRMED]");
        prompt.StaticPrefix.ShouldNotMatch(@"\b20\d\d-\d\d-\d\d\b", "no dates in the cached prefix");
        prompt.StaticPrefix.ShouldNotContain("\r");
        prompt.StaticPrefix.ShouldNotMatch("[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", "no real package ids");
        prompt.StaticPrefix.Split("## Example ").Length.ShouldBe(4, "three worked examples");
        AgentAnswerSchema.ForLanguage(language).ShouldBe(AgentAnswerSchema.LxxJson);
        AgentAnswerSchema.For(AgentMode.LxxTag).ShouldBe(AgentAnswerSchema.LxxJson);
    }

    [Fact]
    public async Task EveryExample_IsTheBuiltPackage_AndItsAnswerPassesTheValidator()
    {
        var data = new LxxAgentTestData();
        var lexicon = LxxLexicon.Build(data.Lexicon);
        var examples = AgentPromptExampleTests.Examples(File);
        examples.Count.ShouldBe(3);

        var flags = new List<string>();
        foreach (var example in examples)
        {
            var (book, chapter, verse) = ParseRef(example.Ref);
            var (envelope, map) = await data.BuildOneAsync(book, chapter, verse, verse);

            var built = JsonNode.Parse(LxxAgentTestData.Serialize(envelope.Package))!;
            var shown = JsonNode.Parse(example.Package)!;
            built["packageId"] = "p";
            shown["packageId"] = "p";
            JsonNode.DeepEquals(built, shown).ShouldBeTrue($"{example.Ref}: the example package differs from the built one.\nbuilt: {built.ToJsonString()}\nshown: {shown.ToJsonString()}");

            using var answer = JsonDocument.Parse(example.Answer);
            var parsed = LxxAgentAnswerParser.Parse(answer.RootElement);
            parsed.Errors.ShouldBeEmpty();
            parsed.PackageId.ShouldBe("p_example");
            var result = new AgentProposalValidator().ValidateLxx(map, parsed, lexicon).Single();

            result.Messages.ShouldBeEmpty($"{example.Ref}: the example answer is rejected");
            result.Proposal.ShouldNotBeNull();
            result.Flags.Where(f => f.Code != LxxFlagCodes.NoStrongs).ShouldBeEmpty($"{example.Ref}: {string.Join(" | ", result.Flags.Select(f => f.Message))}");
            flags.Add($"{example.Ref}: {string.Join(",", result.Flags.Select(f => $"{f.Code}#{f.Position}"))}");
            (result.Proposal.Words.Count + result.Flags.Count).ShouldBe(map.LxxVerses!["v1"].Words.Count, "every word is tagged or flagged");
        }

        string.Join("; ", flags).ShouldBe("1Mo 1:1: ; Rt 1:4: no-strongs#5,no-strongs#9; Hi 36:28.1: ");
    }

    [Fact]
    public async Task TheRuthExample_KeepsRightPrefills_AndCorrectsTheNtGenitive()
    {
        var data = new LxxAgentTestData();
        var (_, map) = await data.BuildOneAsync(LxxAgentTestData.Ruth, 1, 4, 4);
        using var answer = JsonDocument.Parse(AgentPromptExampleTests.Examples(File).Single(e => e.Ref == "Rt 1:4").Answer);

        var proposal = new AgentProposalValidator().ValidateLxx(map, LxxAgentAnswerParser.Parse(answer.RootElement), LxxLexicon.Build(data.Lexicon)).Single().Proposal!;

        var ruth = proposal.Words.Single(w => w.Position == 14);
        ruth.Morph.ShouldBe("N-NSF-P");
        ruth.Prefilled.ShouldBeFalse("the NT genitive N-GSF-P was corrected");
        proposal.Words.Single(w => w.Position == 7).Prefilled.ShouldBeTrue();
        proposal.Links.Where(l => l.HebNumber == 13).Select(l => l.Position).ShouldBe([18, 19], "ὡς and δέκα render כְּעֶשֶׂר");
        proposal.Note.ShouldBe("No TBESG entry for Μωαβείτιδας and Ὀρφά.");
    }

    private static (int Book, int Chapter, int Verse) ParseRef(string reference)
    {
        var match = Regex.Match(reference, @"^(?<book>\S+) (?<c>\d+):(?<v>\d+)(?:\.\d+)?$");
        match.Success.ShouldBeTrue(reference);
        var book = match.Groups["book"].Value switch
        {
            "1Mo" => LxxAgentTestData.Genesis,
            "Rt" => LxxAgentTestData.Ruth,
            "Hi" => LxxAgentTestData.Job,
            var other => throw new InvalidOperationException(other),
        };

        return (book, int.Parse(match.Groups["c"].Value), int.Parse(match.Groups["v"].Value));
    }
}
