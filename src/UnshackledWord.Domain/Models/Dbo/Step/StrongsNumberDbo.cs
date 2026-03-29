namespace UnshackledWord.Domain.Models.Dbo.Step;

public class StrongsNumberDbo : IEntityId, IEquatable<StrongsNumberDbo>
{
    public const string DbName = "\"unshackled-word\".\"StrongsNumbers\"";

    public int Id { get; set; }
    public StrongsLanguage LanguageId { get; init; }
    public int Number { get; init; }
    public string? Extra { get; init; }
    public bool IsRoot { get; set; }
    public bool CoversNextWord { get; set; }
    public int? StepHebrewWordId { get; set; }
    public int? StepGreekWordId { get; set; }
    public int Order { get; set; }

    public override bool Equals(object? obj)
    {
        return Equals(obj as StrongsNumberDbo);
    }

    public bool Equals(StrongsNumberDbo? other)
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

    public static bool operator ==(StrongsNumberDbo? left, StrongsNumberDbo? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(StrongsNumberDbo? left, StrongsNumberDbo? right)
    {
        return !Equals(left, right);
    }
}
