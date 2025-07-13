using CsvHelper.Configuration.Attributes;

namespace UnshackledWord.Tooling.SeedDb.Services.GlobalBibleTools;

public sealed class GbtLemma
{
    [Name("id")]
    public string LemmaId { get; set; } = null!;
    [Name("grammar")]
    public string Grammar { get; set; } = null!;
    [Name("lemma_id")]
    public string StrongsNumber { get; set; } = null!;
}