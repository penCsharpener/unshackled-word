namespace UnshackledWord.Domain.Models.Dbo.Step;

public sealed class StepOtherLexiconDbo : IEntityId
{
    public const string DbName = "\"unshackled-word\".\"StepOtherLexicon\"";

    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public int LxxRefId { get; set; }
    public string Strongs { get; set; } = default!;
    public string? Note { get; set; }
    public string? Type { get; set; }
    public string? OriginalSpelling { get; set; }
    public string StepBibleLink { get; set; } = default!;
    public string? Briefest { get; set; }
    public string Brief { get; set; } = default!;
    public string Short { get; set; } = default!;
    public string Article { get; set; } = default!;
}
