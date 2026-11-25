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
    public SweteSettings Swete { get; set; } = default!;

    /// <summary>
    /// Later starts replace the mapping rows of every verse that was not changed since the last seed import with the repo's newer
    /// mapping seed (roadmap 0010.10). Verses changed locally are kept and listed in the report. Set to false to never touch mappings
    /// after the first import. The morphology seed is never updated after the first import.
    /// </summary>
    public bool UpdateMappingSeed { get; set; } = true;

    /// <summary>
    /// Later starts replace every revision variant that was not changed since the last seed import with the repo's newer revision seed
    /// (roadmap 0011.12); variants changed locally are kept and listed in the report. Set to false to never touch the revision after
    /// the first import.
    /// </summary>
    public bool UpdateRevisionSeed { get; set; } = true;
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

/// <summary>
/// Swete's Septuagint (1930) from eliranwong/LXX-Swete-1930 (GPL-3.0, downloaded, never redistributed; roadmap 0028). The files are fetched
/// from the pinned <see cref="Commit"/> so a rerun always sees the same text.
/// </summary>
public sealed class SweteSettings
{
    /// <summary>Raw file base of the repository without commit, e.g. <c>https://raw.githubusercontent.com/eliranwong/LXX-Swete-1930</c>.</summary>
    public string RawBaseUrl { get; set; } = default!;

    /// <summary>Full commit hash the files are read from. Changing it makes the downloader fetch the files again.</summary>
    public string Commit { get; set; } = default!;

    public string VersificationFile { get; set; } = "00-Swete_versification.csv";
    public string WordsFile { get; set; } = "01-Swete_word_with_punctuations.csv";
}
