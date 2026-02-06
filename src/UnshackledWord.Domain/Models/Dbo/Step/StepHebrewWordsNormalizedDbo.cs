using System.Runtime.InteropServices;

namespace UnshackledWord.Domain.Models.Dbo.Step;

public class StepHebrewWordsNormalizedDbo : IEntityId, IEquatable<StepHebrewWordsNormalizedDbo>
{
    public const string DbName = "\"unshackled-word\".\"StepHebrewWordsNormalized\"";
    public int Id { get; set; }
    public bool IsRoot { get; init; }
    public string? Grammar { get; init; }
    public string? SuffixCode { get; init; }
    public string Hebrew { get; init; } = default!;
    public string StrongsNumber { get; init; } = default!;
    public ICollection<StepHebrewWordDbo> StepHebrewWords { get; set; } = [];

    public bool Equals(StepHebrewWordsNormalizedDbo? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return IsRoot == other.IsRoot && Grammar == other.Grammar && Hebrew == other.Hebrew && SuffixCode == other.SuffixCode && StrongsNumber == other.StrongsNumber;
    }

    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != GetType())
        {
            return false;
        }

        return Equals(obj as StepHebrewWordsNormalizedDbo);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(IsRoot, Grammar, Hebrew, SuffixCode, StrongsNumber);
    }
}
