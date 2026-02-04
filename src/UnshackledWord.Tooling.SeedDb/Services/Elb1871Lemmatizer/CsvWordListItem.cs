namespace UnshackledWord.Tooling.SeedDb.Services.Elb1871Lemmatizer;

public sealed class CsvWordListItem
{
    public string Lemma { set; get; } = default!;
    public string Forms { set; get; } = default!;
    public bool IsSame { set; get; }
}