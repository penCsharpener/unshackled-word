namespace UnshackledWord.Application.Tests.Features.Vocab;

public class FsrsPackageReferenceTests
{
    [Fact]
    public void Only_the_application_project_references_the_fsrs_package()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Directory.Packages.props")))
        {
            root = root.Parent;
        }

        root.ShouldNotBeNull();
        var referencing = new[] { "src", "tests" }
            .SelectMany(d => Directory.EnumerateFiles(Path.Combine(root.FullName, d), "*.csproj", SearchOption.AllDirectories))
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}"))
            .Where(p => File.ReadAllText(p).Contains("FsrsSharp", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFileNameWithoutExtension)
            .ToList();

        referencing.ShouldBe(["UnshackledWord.Application"]);
    }
}
