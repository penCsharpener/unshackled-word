namespace UnshackledWord.Domain.Models.Dbo.Step;

public sealed class StepPersonLexiconRelationsDbo : IEntityId
{
    public const string DbName = "\"unshackled-word\".\"StepPersonLexiconRelations\"";

    public int Id { get; set; }
    public int PersonLexiconId { get; set; }
    public string Name { get; set; } = default!;
    public int BibleBookId { get; set; } = default!;
    public int Chapter { get; set; } = default!;
    public int Verse { get; set; } = default!;
    public string? Strongs { get; set; } = default!;
    /// <summary>
    /// Child, Sibling, Parent, Spouse
    /// </summary>
    public string RelationType { get; set; } = default!;
}
