using CsvHelper.Configuration.Attributes;

namespace UnshackledWord.Tooling.SeedDb.Services.Tsk.Models;

[Delimiter("|")]
public class TskRow
{
    [Name("book")]
    public int Book { get; set; }
    [Name("chapter")]
    public int Chapter { get; set; }
    [Name("verse")]
    public int Verse { get; set; }
    [Name("sort")]
    public int Sort { get; set; }
    [Name("words")]
    public string Words { get; set; } = default!;
    [Name("refs")]
    public string Refs { get; set; } = default!;
}

public record TskCrossReferenceList(string CrossReferences);
