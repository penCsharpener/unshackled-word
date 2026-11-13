using UnshackledWord.Domain.Models.Settings;

namespace UnshackledWord.Tooling.WebApi.Models;

public class AppSettings
{
    public string BackupLocationPath { get; set; } = default!;

    /// <summary>Only <c>SolutionAssetsPath</c> and <c>Elberfelder1871TextFile</c> are used here (read-only, for the seed export download).</summary>
    public DatabaseSeedSettings? DatabaseSeeding { get; set; }
}
