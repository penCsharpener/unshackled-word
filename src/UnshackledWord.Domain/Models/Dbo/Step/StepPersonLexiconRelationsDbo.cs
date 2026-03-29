namespace UnshackledWord.Domain.Models.Dbo.Step;

public sealed class StepPersonLexiconRelationsDbo : IEntityId
{
    public const string DbName = "\"unshackled-word\".\"StepPersonLexiconRelations\"";

    public int Id { get; set; }
    public int PersonLexiconId { get; set; }
    public string Name { get; set; } = default!;
    public int LxxRefId { get; set; }
    public string? Strongs { get; set; } = default!;
    /// <summary>
    /// Child, Sibling, Parent, Spouse
    /// </summary>
    public string RelationType { get; set; } = default!;
}
