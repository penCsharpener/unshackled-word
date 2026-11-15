using UnshackledWord.Domain.Models.Settings;

namespace UnshackledWord.Tooling.WebApi.Models;

public class AppSettings
{
    public string BackupLocationPath { get; set; } = default!;

    /// <summary>Seed paths (<c>SolutionAssetsPath</c>, <c>SolutionTempPath</c>, <c>Elberfelder1871TextFile</c>) for the maintainer seed page and download; the importers read the same section as the domain settings.</summary>
    public DatabaseSeedSettings? DatabaseSeeding { get; set; }
}
