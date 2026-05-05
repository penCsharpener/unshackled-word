namespace UnshackledWord.Tooling.WebApi.Models;

public class AppSettings
{
    public string BackupLocationPath { get; set; } = default!;
    public string SolutionTempPath { get; set; } = default!;
    public string SolutionAssetsPath { get; set; } = default!;
}
