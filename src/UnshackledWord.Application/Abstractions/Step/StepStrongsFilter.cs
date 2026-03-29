using UnshackledWord.Domain.Extensions;

namespace UnshackledWord.Application.Abstractions.Step;

public sealed class StepStrongsFilter
{
    public string[]? Columns { get; set; }
    public int[]? IncludeExtendedStrongs { get; set; }

    public string GetSelectColumns()
    {
        if (Columns.IsNullOrEmpty())
        {
            return "*";
        }

        return $"\"{Columns.JoinStrings("\", \"")}\"";
    }
}
