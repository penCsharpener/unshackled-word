using CsvHelper.Configuration;
using UnshackledWord.Application.Features.Seed.Mappings;

namespace UnshackledWord.Infrastructure.Seed.Mappings;

/// <summary>
/// Column order and names of <c>Elb1871Mappings/NN-Book.csv</c> in format 2:
/// <c>Ref,Pos,Form,Part,SrcRef,SrcPos,SrcHash,Added,ParentPos,ParentForm,ParentRef</c>. <c>ParentRef</c> is the one column beyond the
/// roadmap's list: empty unless the parent word sits in another verse than the word.
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
    }
}

/// <summary>Tells the two mapping CSV formats apart by their header line.</summary>
public static class MappingSeedFormat
{
    public const int Legacy = 1;
    public const int Anchored = SeedLayout.MappingsFormat;

    /// <summary>Format 2 has the <c>SrcRef</c> column, format 1 (<c>HebRefId,PositionInVerse,…,StepWord,…</c>) does not.</summary>
    public static int DetectFromHeader(string? headerLine)
    {
        var columns = (headerLine ?? string.Empty).Split(',', StringSplitOptions.TrimEntries);

        return columns.Contains("SrcRef", StringComparer.Ordinal) ? Anchored : Legacy;
    }

    /// <summary>The format of a file, read from its first line; format 1 when the file is empty.</summary>
    public static async Task<int> DetectAsync(string path, CancellationToken token = default)
    {
        using var reader = new StreamReader(path, SeedLayout.Utf8NoBom);

        return DetectFromHeader(await reader.ReadLineAsync(token));
    }
}
