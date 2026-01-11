namespace UnshackledWord.Application.Abstractions.Step;

public sealed class StepHebrewWordFilter
{
    public int[] IncludedBibleBookIds { get; set; } = default!;
    public int[] IncludeChapters { get; set; } = default!;
}
