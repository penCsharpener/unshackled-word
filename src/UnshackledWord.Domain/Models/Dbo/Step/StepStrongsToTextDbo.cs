namespace UnshackledWord.Domain.Models.Dbo.Step;

public sealed class StepStrongsToTextDbo : IEntityId
{
    public const string DbName = "\"unshackled-word\".\"StepStrongsToText\"";

    public int Id { get; set; }
    public StrongsLanguage LanguageId { get; init; }
    public int Number { get; init; }
    public string? Extra { get; init; }
    public bool IsRoot { get; set; }
    public bool CoversNextWord { get; set; }
    public int? StepGreekWordId { get; set; }
    public int? StepHebrewWordId { get; set; }
    public int Order { get; set; }

    public override bool Equals(object? obj)
    {
        return Equals(obj as StepStrongsToTextDbo);
    }

    public bool Equals(StepStrongsToTextDbo? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return LanguageId == other.LanguageId &&
               Number == other.Number &&
               string.Equals(Extra, other.Extra, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(LanguageId, Number, Extra?.ToLowerInvariant());
    }

    public static bool operator ==(StepStrongsToTextDbo? left, StepStrongsToTextDbo? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(StepStrongsToTextDbo? left, StepStrongsToTextDbo? right)
    {
        return !Equals(left, right);
    }
}
