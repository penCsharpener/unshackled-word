namespace UnshackledWord.Domain.WebApi.MetaBible.GetBibleStructure;

public sealed class GetBibleStructureResponse
{
    public List<BibleStructureBook> Books { get; set; } = [];
}

public sealed class BibleStructureBook
{
    public int Id { get; set; }

    /// <summary>"ot" or "nt".</summary>
    public string Testament { get; set; } = default!;

    public string NameDe { get; set; } = default!;
    public string NameEn { get; set; } = default!;
    public string AbbreviationDe { get; set; } = default!;
    public string AbbreviationEn { get; set; } = default!;

    /// <summary>Index 0 = chapter 1; Hebrew (= Elb1871) versification.</summary>
    public int[] VersesPerChapter { get; set; } = [];
}
