namespace UnshackledWord.Domain.Models.Study;

/// <summary>One row of the catalog <c>GreekEditions</c> (roadmap 0025): a printed edition, translation, manuscript or version STEP names in its edition column.</summary>
public sealed class GreekEditionRow
{
    public short Id { get; init; }
    public string Code { get; init; } = default!;
    public string Name { get; init; } = default!;
    public short? Year { get; init; }
    public string Kind { get; init; } = default!;
    public bool IsMain { get; init; }
    public short Sort { get; init; }
}

/// <summary>A <c>StepGreekWordEditions</c> row joined to the catalog: one edition that has the Greek word.</summary>
public sealed class GreekWordEditionRow
{
    public int StepWordId { get; init; }
    public string Code { get; init; } = default!;
    public string Kind { get; init; } = default!;
    public bool IsMain { get; init; }
    public short Sort { get; init; }

    /// <summary>+n = n words later, -n = n words earlier, null = same order (or <see cref="ShiftRaw"/> is set).</summary>
    public short? Shift { get; init; }
    public string? ShiftRaw { get; init; }
}
