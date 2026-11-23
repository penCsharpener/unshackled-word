using System.Text.Json;
using System.Text.Json.Serialization;

namespace UnshackledWord.Infrastructure.Seed;

/// <summary>
/// <c>assets/seed/manifest.json</c>: the format version, the base text the seed was exported against and, per seed kind, the files with
/// row counts and SHA-256. <see cref="ExportedAt"/> is the only value that is not derived from the data.
/// </summary>
public sealed class SeedManifest
{
    public int Format { get; set; } = SeedLayout.FormatVersion;

    /// <summary>UTC time of the export as ISO-8601; kept as it was when a re-export produced identical seed files.</summary>
    public string ExportedAt { get; set; } = string.Empty;

    public SeedBaseText? BaseText { get; set; }

    public SeedKind? Morphology { get; set; }

    /// <summary>The mapping CSVs in <c>assets/Elb1871Mappings</c> (format 2, roadmap 0010.8); absent while the folder still holds format 1.</summary>
    public SeedKind? Mappings { get; set; }

    /// <summary>The revision seed in <c>assets/seed/revision</c> (levels, verses, mapping; roadmap 0011.11); absent until a revision export ran.</summary>
    public SeedKind? Revision { get; set; }

    /// <summary>The TSK curation change file <c>assets/seed/tsk/curations.csv</c> (roadmap 0024.7); absent until a TSK export ran.</summary>
    public SeedKind? Tsk { get; set; }

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NewLine = "\n"
    };
}

/// <summary>The base text file the seed's German words come from.</summary>
public sealed class SeedBaseText
{
    public string File { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;

    /// <summary>Number of words a fresh parse of the file yields (not necessarily the number of words in the exporting database).</summary>
    public int Words { get; set; }
}

/// <summary>The files of one seed kind. <see cref="SeedVersion"/> identifies the content and is what <c>SeedImports</c> stores.</summary>
public sealed class SeedKind
{
    /// <summary>Format of the files of this kind when it differs from the manifest's format (mappings: 2); null otherwise.</summary>
    public int? Format { get; set; }

    /// <summary>Folder of the files relative to <c>assets</c> when they are not below <c>assets/seed</c> (mappings: <c>Elb1871Mappings</c>).</summary>
    public string? Folder { get; set; }

    public string SeedVersion { get; set; } = string.Empty;
    public int Rows { get; set; }
    public List<SeedFileEntry> Files { get; set; } = [];
    public List<SeedFileEntry> AnchorFiles { get; set; } = [];
}

public sealed class SeedFileEntry
{
    public string Path { get; set; } = string.Empty;
    public int Rows { get; set; }
    public string Sha256 { get; set; } = string.Empty;
}
