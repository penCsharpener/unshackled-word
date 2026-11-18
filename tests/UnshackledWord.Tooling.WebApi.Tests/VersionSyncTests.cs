using System.Text.Json;
using System.Text.RegularExpressions;

namespace UnshackledWord.Tooling.WebApi.Tests;

public class VersionSyncTests
{
    [Fact]
    public void Frontend_and_backend_versions_match()
    {
        var root = AppContext.BaseDirectory;
        while (root is not null && !File.Exists(Path.Combine(root, "UnshackledWord.slnx"))) root = Path.GetDirectoryName(root);
        root.ShouldNotBeNull();

        var props = File.ReadAllText(Path.Combine(root, "Directory.Build.props"));
        var backend = Regex.Match(props, "<Version>([^<]+)</Version>").Groups[1].Value;
        var package = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "src", "UnshackledWord.Web", "package.json")));
        var frontend = package.RootElement.GetProperty("version").GetString();

        backend.ShouldNotBeNullOrEmpty();
        frontend.ShouldBe(backend, "bump Directory.Build.props and src/UnshackledWord.Web/package.json together");
    }
}
