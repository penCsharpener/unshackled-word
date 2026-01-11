namespace UnshackledWord.Application.Abstractions.Step;

public sealed class StepGreekWordFilter
{
    public int[] IncludedBibleBookIds { get; set; } = default!;
    public int[] IncludeChapters { get; set; } = default!;

}
