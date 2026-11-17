using CsvHelper.Configuration;
using UnshackledWord.Application.Features.Seed.Mappings;

namespace UnshackledWord.Infrastructure.Seed.Mappings;

/// <summary>
/// Column order and names of <c>Elb1871Mappings/NN-Book.csv</c> in format 3:
/// <c>Ref,Pos,Form,Part,SrcRef,SrcPos,SrcHash,Added,ParentPos,ParentForm,ParentRef,Role,Strongs,PartLemma,MultiHead</c>. <c>ParentRef</c> is
/// empty unless the parent word sits in another verse than the word. The last four columns (roadmap 0018) are empty for the defaults
/// (<c>head</c>, none, none, false); format 2 files lack them and import with those defaults.
/// </summary>
public sealed class MappingSeedLineMap : ClassMap<MappingSeedLine>
{
    public MappingSeedLineMap()
    {
        Map(x => x.Ref).Index(0).Name("Ref");
        Map(x => x.Pos).Index(1).Name("Pos");
        Map(x => x.Form).Index(2).Name("Form");
        Map(x => x.Part).Index(3).Name("Part");
        Map(x => x.SrcRef).Index(4).Name("SrcRef");
        Map(x => x.SrcPos).Index(5).Name("SrcPos");
        Map(x => x.SrcHash).Index(6).Name("SrcHash");
        Map(x => x.Added).Index(7).Name("Added");
        Map(x => x.ParentPos).Index(8).Name("ParentPos");
        Map(x => x.ParentForm).Index(9).Name("ParentForm");
        Map(x => x.ParentRef).Index(10).Name("ParentRef");
        Map(x => x.Role).Index(11).Name("Role").TypeConverter<RoleConverter>().Optional();
        Map(x => x.Strongs).Index(12).Name("Strongs").Optional();
        Map(x => x.PartLemma).Index(13).Name("PartLemma").Optional();
        Map(x => x.MultiHead).Index(14).Name("MultiHead").TypeConverter<TrueOnlyFlagConverter>().Optional();
    }
}

/// <summary>
/// Tells the mapping CSV formats apart by their header line (the manifest has no per-file format): 1 = legacy, 2 = has <c>SrcRef</c>,
/// 3 = also has <c>Role</c> (roadmap 0018). The importer reads 2 and 3 alike; a format 2 file just has the defaults.
/// </summary>
public static class MappingSeedFormat
{
    public const int Legacy = 1;
    public const int Anchored = 2;
    public const int Roles = SeedLayout.MappingsFormat;

    /// <summary>True for every format the anchored importer reads (2 and 3).</summary>
    public static bool IsAnchored(int format) => format >= Anchored;

    /// <summary>Format 2 has the <c>SrcRef</c> column, format 1 (<c>HebRefId,PositionInVerse,…,StepWord,…</c>) does not.</summary>
    public static int DetectFromHeader(string? headerLine)
    {
        var columns = (headerLine ?? string.Empty).Split(',', StringSplitOptions.TrimEntries);

        if (!columns.Contains("SrcRef", StringComparer.Ordinal))
        {
            return Legacy;
        }

        return columns.Contains("Role", StringComparer.Ordinal) ? Roles : Anchored;
    }

    /// <summary>The format of a file, read from its first line; format 1 when the file is empty.</summary>
    public static async Task<int> DetectAsync(string path, CancellationToken token = default)
    {
        using var reader = new StreamReader(path, SeedLayout.Utf8NoBom);

        return DetectFromHeader(await reader.ReadLineAsync(token));
    }
}
