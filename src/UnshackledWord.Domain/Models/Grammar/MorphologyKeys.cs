namespace UnshackledWord.Domain.Models.Grammar;

/// <summary>Decoded parts of a morphology code. Values are the stable key strings of the API contract (see <see cref="MorphologyKey"/>); null = not applicable or not decoded.</summary>
public sealed record MorphologyKeys
{
    public static readonly MorphologyKeys Empty = new();

    public string? PartOfSpeech { get; init; }
    public string? Tense { get; init; }
    public string? Voice { get; init; }
    public string? Mood { get; init; }
    public string? Person { get; init; }
    public string? Case { get; init; }
    public string? Number { get; init; }
    public string? Gender { get; init; }
    public string? Degree { get; init; }
    public string? Stem { get; init; }
    public string? HebrewForm { get; init; }
    public string? State { get; init; }

    public bool IsEmpty => this == Empty;
}
