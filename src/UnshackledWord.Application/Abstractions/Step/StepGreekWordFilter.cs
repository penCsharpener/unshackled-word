using UnshackledWord.Domain.Extensions;

namespace UnshackledWord.Application.Abstractions.Step;

public sealed class StepGreekWordFilter
{
    public string[]? Columns { get; set; }
    public int[] IncludedBibleBookIds { get; set; } = default!;
    public int[] IncludeChapters { get; set; } = default!;

    public string GetSelectColumns()
    {
        if (Columns.IsNullOrEmpty())
        {
            return "*";
        }

        return $"\"{Columns.JoinStrings("\", \"")}\"";
    }
}
