namespace UnshackledWord.Domain.Models.Dbo.Step;

public sealed class StepPersonLexiconDbo : IEntityId
{
    public const string DbName = "\"unshackled-word\".\"StepPersonLexicon\"";

    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public int LxxRefId { get; set; }
    public string? Strongs { get; set; } = default!;
    public string? Note { get; set; }
    public string? OriginalSpelling { get; set; }
    public string? Tribe { get; set; }
    public string? Gender { get; set; }
    public string? Briefest { get; set; }
    public string? Brief { get; set; }
    public string? Short { get; set; }
    public string? Article { get; set; }
    public List<StepPersonLexiconRelationsDbo> Relations { get; set; } = default;
}
