namespace UnshackledWord.Domain.Models.Study;

/// <summary>An original word of a chapter query; <see cref="HebRefId"/> (BBCCCVVV) says which Elberfelder verse it belongs to.</summary>
public sealed class ChapterOriginalWordRow
{
    public int Id { get; set; }
    public int HebRefId { get; set; }
    public int Position { get; set; }
    public string Original { get; set; } = string.Empty;
    public string? Lemma { get; set; }
    public string? Strongs { get; set; }
    public string? MorphologyCode { get; set; }
}

/// <summary>A stored link of a chapter query: German word (and its verse) to an original word, added words excluded.</summary>
public sealed class ChapterMappingRow
{
    public int ElbWordId { get; set; }
    public int StepWordId { get; set; }
    public int GermanVerse { get; set; }
}
