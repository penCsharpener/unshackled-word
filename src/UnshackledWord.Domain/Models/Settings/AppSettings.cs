namespace UnshackledWord.Domain.Models.Settings;

public class AppSettings
{
    public DatabaseSeedSettings DatabaseSeeding { get; set; } = default!;
}

public sealed class DatabaseSeedSettings
{
    public string FolderLocation { get; set; } = default!;
    public string[] SRFileUrls { get; set; } = default!;
    public string BibleKommentareElberfelderUrl { get; set; } = default!;
    public string BibleKommentareSourcePath { get; set; } = default!;
    public string BibleKommentareDestinationPath { get; set; } = default!;
    public string RepoLocationEliranWongLxxRalfs1935 { get; set; } = default!;
    public string Elberfelder1871TextFile { get; set; } = default!;
    public OpenScripturesGithubSettings OpenScripturesGithub { get; set; } = default!;
}

public sealed class OpenScripturesGithubSettings
{
    public string LocalPath { get; set; } = default!;
    public string DownloadDomain { get; set; } = default!;
    public string DownloadPath { get; set; } = default!;
    public string XmlFiles { get; set; } = default!;
}
