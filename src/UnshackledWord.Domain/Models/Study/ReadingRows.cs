namespace UnshackledWord.Domain.Models.Study;

/// <summary>Common columns of a mapping row joined to the original word. A row without <see cref="StepWordId"/> is an added word.</summary>
public abstract class ReadingRowBase
{
    public bool? IsAddedWord { get; init; }
    public string? GermanWordPart { get; init; }
    public DateTime MappingCreatedAt { get; init; }
    public int? StepWordId { get; init; }
    public int? PositionInVerse { get; init; }
    public string? Original { get; init; }
    public string? Transliteration { get; init; }
    public string? Strongs { get; init; }
    public string? ContextGloss { get; init; }
    public string? MorphologyCode { get; init; }
}

public sealed class GreekReadingRow : ReadingRowBase
{
    public string? Lemma { get; init; }
    public string? Gloss { get; init; }
    public string? StepPartOfSpeech { get; init; }
    public string? StepTense { get; init; }
    public string? StepVoice { get; init; }
    public string? StepMood { get; init; }
    public string? StepPerson { get; init; }
    public string? StepCase { get; init; }
    public string? StepNumber { get; init; }
    public string? StepGender { get; init; }
}

public sealed class HebrewReadingRow : ReadingRowBase
{
    public string? DisambiguatedStrongs { get; init; }
}

/// <summary>One Elberfelder rendering (most common spelling of the German word, counted case-insensitively) of an original-language Strong's number with its number of uses.</summary>
public sealed class RenderingRow
{
    public string Word { get; init; } = default!;
    public int Count { get; init; }
}

/// <summary>STEP person or place entry for a Strong's number: kind is "person" or "place", brief is the one-line STEP description.</summary>
public sealed class NamedEntityRow
{
    public string Kind { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string Brief { get; init; } = default!;
}

public static class CoMappedRelations
{
    public const string SameOriginal = "sameOriginal";
    public const string AddedWord = "addedWord";
    public const string Parent = "parent";
}

/// <summary>Another German word of the verse that shares mapping rows with the opened word.</summary>
public sealed class CoMappedWordRow
{
    public int WordId { get; init; }
    public string WordInContext { get; init; } = default!;
    public int PositionInVerse { get; init; }
    public string Relation { get; init; } = default!;
    public int? StepWordId { get; init; }
}

/// <summary>One part (prefix, root or suffix) of a Hebrew/Aramaic word.</summary>
public sealed class HebrewMorpheme
{
    public int StepHebrewWordsId { get; init; }
    public int PositionInWord { get; init; }
    public bool IsRoot { get; init; }
    public string? Hebrew { get; init; }
    public string? StrongsNumber { get; init; }
    public string? Grammar { get; init; }
    public string? SuffixCode { get; init; }
    public string? PartOfSpeech { get; init; }
    public string? Stem { get; init; }
    public string? Form { get; init; }
    public string? Person { get; init; }
    public string? Gender { get; init; }
    public string? Number { get; init; }
    public string? State { get; init; }
}
