namespace UnshackledWord.Domain.Models.Settings;

public class AppSettings
{
    public DatabaseSeedSettings DatabaseSeeding { get; set; } = default!;
}

public sealed class DatabaseSeedSettings
{
    public string[] SRFileUrls { get; set; } = default!;
    public string SolutionTempPath { get; set; } = default!;
    public string SolutionAssetsPath { get; set; } = default!;
    public string RepoLocationEliranWongLxxRalfs1935 { get; set; } = default!;
    public string Elberfelder1871TextFile { get; set; } = default!;
    public string GlobalBibleToolsWordsCsvFile { get; set; } = default!;
    public string GlobalBibleToolsLemmaCsvFile { get; set; } = default!;
    public string GlobalBibleToolsDictionaryCsvFile { get; set; } = default!;
    public Elberfelder1871 Elberfelder1871 { get; set; } = default!;
    public string TskWithSummariesDownloadGithubPath { get; set; } = default!;
    public string TskDownloadGithubPath { get; set; } = default!;
    public string TskFilePath { get; set; } = default!;
    public SblSettings SblSettings { get; set; } = default!;
    public ByzantineSettings ByzantineSettings { get; set; } = default!;
    public OpenScripturesGithubSettings OpenScripturesGithub { get; set; } = default!;
    public StepBibleData StepBibleData { get; set; } = default!;
}

public sealed class Elberfelder1871
{
    public string LemmatizerGermanLink { get; set; } = default!;
}

public sealed class OpenScripturesGithubSettings
{
    public string LocalPath { get; set; } = default!;
    public string DownloadDomain { get; set; } = default!;
    public string DownloadPath { get; set; } = default!;
    public string XmlFiles { get; set; } = default!;
}

public sealed class SblSettings
{
    public string TextDownloadUrl { get; set; } = default!;
    public string TextFilePath { get; set; } = default!;
    public string ApparatusDownloadUrl { get; set; } = default!;
    public string ApparatusFilePath { get; set; } = default!;
}

public sealed class ByzantineSettings
{
    public string TextDownloadUrl { get; set; } = default!;
    public string TextFilePath { get; set; } = default!;
}

public sealed class StepBibleData
{
    public string GithubRepoUrl { get; set; } = default!;
    public string AmalgamatedSubPath { get; set; } = default!;
    public string[] AmalgamatedFiles { get; set; } = default!;
    public string StrongsLexiconSubPath { get; set; } = default!;
    public string[] StrongsFiles { get; set; } = default!;
    public string[] MorphologyFiles { get; set; } = default!;
    public string PersonPlaceFile { get; set; } = default!;
    public string VersificationFile { get; set; } = default!;
}
