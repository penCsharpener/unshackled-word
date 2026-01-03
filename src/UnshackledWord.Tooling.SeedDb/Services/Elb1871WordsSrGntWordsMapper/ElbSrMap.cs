using CsvHelper.Configuration.Attributes;

namespace UnshackledWord.Tooling.SeedDb.Services.Elb1871WordsSrGntWordsMapper;

public sealed class ElbSrMap
{
    public int BibleBookId { get; set; }
    public int ChapterId { get; set; }
    public int VerseId { get; set; }
    public string Elb1871Words { get; set; } = default!;

    [Ignore]
    public string[] Elb1871WordList { get; set; } = [];
    public string SrGntWord { get; set; } = default!;
}
