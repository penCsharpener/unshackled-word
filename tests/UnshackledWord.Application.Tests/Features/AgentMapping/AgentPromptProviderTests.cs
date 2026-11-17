using UnshackledWord.Application.Features.AgentMapping;

namespace UnshackledWord.Application.Tests.Features.AgentMapping;

public class AgentPromptProviderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "agent-prompts-" + Guid.NewGuid().ToString("N"));

    public AgentPromptProviderTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() => Directory.Delete(_dir, true);

    [Fact]
    public void NewestVersionWins_PlaceholderIsReplaced_LineEndingsAreNormalised()
    {
        File.WriteAllText(Path.Combine(_dir, "greek-v1.md"), "old");
        File.WriteAllText(Path.Combine(_dir, "greek-v10.md"), "Schema:\r\n" + AgentAnswerSchema.Placeholder + "  \r\n\r\n");
        File.WriteAllText(Path.Combine(_dir, "greek-v2.md"), "middle");
        File.WriteAllText(Path.Combine(_dir, "hebrew-v1.md"), "hebrew");

        var prompt = new AgentPromptProvider(_dir).Get("greek");

        prompt.Version.ShouldBe("greek-v10");
        prompt.StaticPrefix.ShouldBe("Schema:\n" + AgentAnswerSchema.Json + "\n");
        prompt.Language.ShouldBe("greek");
    }

    [Fact]
    public void SameVersionIsByteIdenticalOnEveryRead_AndAVersionCanBeFixed()
    {
        File.WriteAllText(Path.Combine(_dir, "hebrew-v1.md"), "v1 text " + AgentAnswerSchema.Placeholder);
        File.WriteAllText(Path.Combine(_dir, "hebrew-v2.md"), "v2 text");
        var provider = new AgentPromptProvider(_dir);

        provider.Get("hebrew").StaticPrefix.ShouldBe(provider.Get("hebrew").StaticPrefix);
        provider.GetVersion("hebrew", "hebrew-v1").StaticPrefix.ShouldContain("v1 text");
        provider.GetVersion("hebrew", "hebrew-v1").Version.ShouldBe("hebrew-v1");
    }

    [Fact]
    public void MissingFile_Throws()
    {
        Should.Throw<FileNotFoundException>(() => new AgentPromptProvider(_dir).Get("greek"));
        Should.Throw<FileNotFoundException>(() => new AgentPromptProvider(_dir).GetVersion("greek", "greek-v9"));
    }

    [Fact]
    public void TheRepositoryPromptFiles_ExistAndHaveAStablePrefixWithoutVolatileContent()
    {
        var dir = AgentPromptProvider.ResolveDirectory(new TestConfiguration(new Dictionary<string, string?>()));
        var provider = new AgentPromptProvider(dir);

        foreach (var language in new[] { "greek", "hebrew" })
        {
            var prompt = provider.Get(language);

            // v2 (roadmap 0018.9, links with roles) is the default for new runs; v1 stays loadable for runs that started with it.
            prompt.Version.ShouldBe($"{language}-v2");
            provider.GetVersion(language, $"{language}-v1").StaticPrefix.ShouldContain(AgentAnswerSchema.Json);
            AgentPromptProvider.CarriesRoles(prompt.Version).ShouldBeTrue();
            AgentPromptProvider.CarriesRoles($"{language}-v1").ShouldBeFalse();
            prompt.StaticPrefix.ShouldContain(AgentAnswerSchema.Json);
            prompt.StaticPrefix.ShouldNotContain(AgentAnswerSchema.Placeholder);
            prompt.StaticPrefix.ShouldContain("[UNCONFIRMED]");
            prompt.StaticPrefix.ShouldNotMatch(@"\b20\d\d-\d\d-\d\d\b", "no dates in the cached prefix");
            prompt.StaticPrefix.ShouldNotContain("\r");
            prompt.StaticPrefix.Split("## Example ").Length.ShouldBe(6, "five worked examples");
        }
    }
}
