using UnshackledWord.Application.Features.AgentMapping;

namespace UnshackledWord.Application.Tests.Features.AgentMapping;

public class AgentProfilesTests
{
    private static AgentProfiles Parse(params (string Key, string Value)[] settings) =>
        AgentProfiles.From(new TestConfiguration(settings.ToDictionary(s => "AgentMapping:Profiles:" + s.Key, s => (string?)s.Value)));

    [Fact]
    public void Profile_IsReadWithModelPricesAndCaps()
    {
        var profiles = Parse(
            ("ollama:Kind", "ollama"),
            ("ollama:Model", "gemma4:31b"),
            ("ollama:ApiKey", "k"),
            ("ollama:MaxTokensPerRun", "400000"),
            ("ollama:MaxVersesPerPackage", "12"),
            ("ollama:Models:gemma4_31b:Tag", "gemma4:31b"),
            ("ollama:Models:gemma4_31b:InputPricePerMillion", "0.5"),
            ("ollama:Models:gemma4_31b:CachedInputPricePerMillion", "0.1"),
            ("ollama:Models:gemma4_31b:OutputPricePerMillion", "1.5"),
            ("ollama:Models:gemma4_31b:MaxCostPerRun", "2"));

        profiles.Validate().ShouldBeEmpty();
        var p = profiles.Find("OLLAMA")!;
        p.BaseUrl.ShouldBe("https://ollama.com", "the default base url of the kind");
        p.EffectiveMaxTokens("gemma4:31b").ShouldBe(400000);
        p.EffectiveMaxCost("gemma4:31b").ShouldBe(2m, "a model cap wins over the profile");
        p.EffectiveMaxVerses("gemma4:31b").ShouldBe(12);
        p.ModelSettings("gemma4:31b").OutputPricePerMillion.ShouldBe(1.5m);
    }

    [Fact]
    public void Cost_UsesCachedPriceForCachedTokens()
    {
        var p = Parse(("a:Kind", "openai"), ("a:Model", "m"), ("a:Models:m:InputPricePerMillion", "2"), ("a:Models:m:CachedInputPricePerMillion", "0.5"), ("a:Models:m:OutputPricePerMillion", "8")).Find("a")!;

        // 600k fresh * 2 + 400k cached * 0.5 + 100k output * 8, all per 1M
        p.Cost("m", 1_000_000, 400_000, 100_000).ShouldBe(1.2m + 0.2m + 0.8m);
    }

    [Fact]
    public void ModelWithoutCachedPrice_ChargesCachedTokensAtTheInputPrice()
    {
        var p = Parse(("a:Kind", "openai"), ("a:Model", "m"), ("a:Models:m:InputPricePerMillion", "2")).Find("a")!;

        p.Cost("m", 1_000_000, 500_000, 0).ShouldBe(2m);
    }

    [Fact]
    public void KeyIsRequired_ForCloudKinds_AndForRemoteOpenAiHosts_ButNotForLocalServers()
    {
        var profiles = Parse(
            ("claude:Kind", "anthropic"), ("claude:Model", "m"),
            ("gem:Kind", "gemini"), ("gem:Model", "m"),
            ("cloud:Kind", "ollama"), ("cloud:Model", "m"),
            ("local:Kind", "ollama"), ("local:Model", "m"), ("local:BaseUrl", "http://localhost:11434"),
            ("vllm:Kind", "openai"), ("vllm:Model", "m"), ("vllm:BaseUrl", "http://127.0.0.1:8000/v1"),
            ("keyed:Kind", "openai"), ("keyed:Model", "m"), ("keyed:ApiKey", "k"));

        profiles.Find("claude")!.IsUsable.ShouldBeFalse();
        profiles.Find("gem")!.IsUsable.ShouldBeFalse();
        profiles.Find("cloud")!.IsUsable.ShouldBeFalse();
        profiles.Find("cloud")!.UnusableReason.ShouldContain("UNSHACKLEDWORD_AgentMapping__Profiles__cloud__ApiKey");
        profiles.Find("local")!.IsUsable.ShouldBeTrue();
        profiles.Find("vllm")!.IsUsable.ShouldBeTrue();
        profiles.Find("keyed")!.IsUsable.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ReportsStructuralProblems()
    {
        var errors = Parse(
            ("bad:Kind", "mistral"), ("bad:Model", ""), ("bad:BaseUrl", "ftp://x"), ("bad:SchemaMode", "magic"), ("bad:MaxParallel", "0"),
            ("neg:Kind", "openai"), ("neg:Model", "m"), ("neg:Models:m:InputPricePerMillion", "-1")).Validate();

        errors.ShouldContain(e => e.Contains("bad:Kind"));
        errors.ShouldContain(e => e.Contains("bad:BaseUrl"));
        errors.ShouldContain(e => e.Contains("bad:SchemaMode"));
        errors.ShouldContain(e => e.Contains("bad:Model"));
        errors.ShouldContain(e => e.Contains("bad:MaxParallel"));
        errors.ShouldContain(e => e.Contains("neg:Models"));
    }

    [Fact]
    public void NoConfiguration_MeansNoProfiles()
    {
        AgentProfiles.From(null).Items.ShouldBeEmpty();
        Parse().Validate().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("gemma4:31b", "gemma4_31b", true)]
    [InlineData("Gemma4:31B", "gemma4:31b", true)]
    [InlineData("gemma4:31b", "gemma4:12b", false)]
    public void TagsMatchAcrossColonAndUnderscore(string a, string b, bool same) => AgentProfiles.SameTag(a, b).ShouldBe(same);

    [Fact]
    public void Budget_EstimateAddsThePrefixPerPackage_CachedAfterTheFirst_WithMargin()
    {
        var p = Parse(("a:Kind", "openai"), ("a:Model", "m"), ("a:Models:m:InputPricePerMillion", "1"), ("a:Models:m:CachedInputPricePerMillion", "0.1"), ("a:Models:m:OutputPricePerMillion", "2")).Find("a")!;

        var e = AgentRunBudget.Estimate([1000, 1000], prefixTokens: 4000, p, "m");

        // input 2 * (4000 + 1000) = 10000, cached 4000, output 2 * 300 = 600, all * 1.3
        e.InputTokens.ShouldBe(13000);
        e.CachedInputTokens.ShouldBe(5200);
        e.OutputTokens.ShouldBe(780);
        e.TotalTokens.ShouldBe(13780);
        e.CostUsd.ShouldBe((7800 * 1m + 5200 * 0.1m + 780 * 2m) / 1_000_000m);
    }
}
