namespace UnshackledWord.Application.Features.LinkRoles.Backfill;

/// <summary>Kind of the change-set row of an apply (<c>Elb1871MorphologyChangeSets.Kind</c>) and of its undo.</summary>
public static class LinkRoleChangeSetKinds
{
    public const string Apply = "linkRoles";
    public const string Undo = "linkRolesUndo";
}

/// <summary>Mapping table of a row: <c>G</c> = Elb1871GreekMapping, <c>H</c> = Elb1871HebrewMapping.</summary>
public static class MappingLang
{
    public const char Greek = 'G';
    public const char Hebrew = 'H';

    public static char Of(SourceLanguage language) => language == SourceLanguage.Hebrew ? Hebrew : Greek;
}

/// <summary>An Elb1871GreekMapping / Elb1871HebrewMapping row with every column the backfill reads or writes.</summary>
public sealed record LinkRoleMappingRow
{
    public int Id { get; init; }
    public int ElbWordId { get; init; }
    public int HebRefId { get; init; }
    public int? StepWordId { get; init; }
    public string? StrongsNumber { get; init; }
    public bool? IsAddedWord { get; init; }
    public int? ParentGermanWordId { get; init; }
    public string? GermanWordPart { get; init; }
    public string Role { get; init; } = "head";

    public MappingRowValues Values => new(Role, StrongsNumber, IsAddedWord, ParentGermanWordId, StepWordId);
}

/// <summary>The columns a backfill change writes, as stored (NULL included), so undo restores them exactly.</summary>
public sealed record MappingRowValues(string Role, string? StrongsNumber, bool? IsAddedWord, int? ParentGermanWordId, int? StepWordId);

/// <summary>One mapping row the apply changes, with the rule that decided it.</summary>
public sealed record MappingRowChange(char Lang, int MappingId, int HebRefId, int ElbWordId, string Rule, MappingRowValues Before, MappingRowValues After);

/// <summary>One Elb1871Morphology lemma the apply changes (rule 3b: verb, rule 6: particle), keyed like the morphology row.</summary>
public sealed record LemmaRowChange(int ElbWordId, int HebRefId, int PositionInVerse, string Rule, string? OldLemma, string NewLemma);

/// <summary>An Elb1871Words row with its morphology and verse reference.</summary>
public sealed record LinkRoleWordRow
{
    public int Id { get; init; }
    public int HebRefId { get; init; }
    public int PositionInVerse { get; init; }
    public string Text { get; init; } = string.Empty;
    public string? Lemma { get; init; }
    public string? Stts { get; init; }
    public string? Person { get; init; }
    public string? Number { get; init; }
    public string? Gender { get; init; }
    public string? Case { get; init; }
    public int Chapter { get; init; }
    public int Verse { get; init; }

    public LinkRoleGermanWord ToGermanWord() => new(Id, PositionInVerse, Text, Lemma, Stts, Person, Number, Gender, Case);
}

/// <summary>A StepGreekWords / StepHebrewWords row as the rules need it.</summary>
public sealed record LinkRoleSourceRow
{
    public int Id { get; init; }
    public string? Morphology { get; init; }
    public string? MorphemeTags { get; init; }
}

/// <summary>Everything of one book the backfill needs: words, the rows of both mapping tables and the source words they point at.</summary>
public sealed record LinkRoleBookData(
    int BookId,
    string BookName,
    IReadOnlyList<LinkRoleWordRow> Words,
    IReadOnlyList<LinkRoleMappingRow> GreekRows,
    IReadOnlyList<LinkRoleMappingRow> HebrewRows,
    IReadOnlyList<LinkRoleSourceRow> GreekSources,
    IReadOnlyList<LinkRoleSourceRow> HebrewSources);

/// <summary>An applied, not yet undone backfill change set.</summary>
public sealed record LinkRoleChangeSetInfo
{
    public int Id { get; init; }
    public int UserId { get; init; }
    public DateTime CreatedAt { get; init; }
    public int MappingRows { get; init; }
    public int LemmaRows { get; init; }
}

/// <summary>Result of an apply: the change set and the rows it wrote.</summary>
public sealed record LinkRoleApplyResult(int ChangeSetId, int MappingRows, int LemmaRows);

/// <summary>Result of an undo: restored rows, and rows skipped because they changed after the apply.</summary>
public sealed record LinkRoleUndoResult(int ChangeSetId, int UndoChangeSetId, int MappingRestored, int MappingSkipped, int LemmasRestored, int LemmasSkipped);
