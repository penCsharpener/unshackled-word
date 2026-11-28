using UnshackledWord.Domain.WebApi.Admin;

namespace UnshackledWord.Seeding.Catalog;

/// <summary>Row count of one table; <see cref="Populated"/> is set for materialized views only (false = never filled, rows are 0).</summary>
public sealed record TableCount(string Name, long Rows, bool? Populated = null);

/// <summary>
/// Turns row counts into the status of every catalog step (03-seed-catalog.md, status rule):
/// green = every table has rows; amber = some tables have rows (also after a failed job that wrote some); a failed job that wrote nothing leaves the step red with <c>Failed</c> set;
/// red = all tables empty; grey = red, but a required step is not green (<c>Missing</c> names them).
/// Pure, so the rule is testable without a database.
/// </summary>
public static class SetupStatusCalculator
{
    /// <param name="catalog">The steps in run order; every <c>Requires</c> key must come earlier.</param>
    /// <param name="counts">Row count per table name; a missing table counts as 0.</param>
    /// <param name="failedKeys">Steps whose last job failed in this API process.</param>
    /// <param name="completedKeys">Steps that ran to the end in this process; a step with a note then counts as green at 0 rows.</param>
    public static List<SetupStepResponse> Compute(
        IReadOnlyList<SeedStep> catalog,
        IReadOnlyDictionary<string, TableCount> counts,
        IReadOnlySet<string> failedKeys,
        IReadOnlySet<string> completedKeys)
    {
        var result = new List<SetupStepResponse>(catalog.Count);
        var green = new HashSet<string>();

        foreach (var step in catalog)
        {
            var tables = step.Tables
                .Select(t => counts.TryGetValue(t.Name, out var c)
                    ? new SetupTableResponse { Name = t.Name, Rows = c.Rows, Populated = c.Populated }
                    : new SetupTableResponse { Name = t.Name })
                .ToList();

            var missing = step.Requires.Where(key => !green.Contains(key)).ToList();
            var failed = failedKeys.Contains(step.Key);
            var ranWithNote = step.Note is not null && completedKeys.Contains(step.Key);
            var status = StatusOf(tables, missing.Count > 0, failed, ranWithNote);
            if (status == SetupStepStatuses.Green)
            {
                green.Add(step.Key);
            }

            result.Add(new SetupStepResponse
            {
                Key = step.Key,
                Group = step.Group,
                Source = step.Source,
                SourceHost = step.SourceHost,
                Status = status,
                Requires = [.. step.Requires],
                Missing = status == SetupStepStatuses.Grey ? missing : [],
                Resumable = step.Resumable,
                Wired = step.Wired,
                Runnable = step.Runnable,
                Rerunnable = step.Rerunnable ? true : null,
                Failed = failed ? true : null,
                Note = ranWithNote && tables.All(t => t.Rows == 0) ? step.Note : null,
                Tables = tables,
            });
        }

        return result;
    }

    private static string StatusOf(List<SetupTableResponse> tables, bool hasMissing, bool failed, bool ranWithNote)
    {
        var filled = tables.Count(t => t.Rows > 0);

        // A failed job leaves the step amber only when it wrote something; with nothing written the step stays red (or grey) and can run again.
        if (failed && filled > 0)
        {
            return SetupStepStatuses.Amber;
        }

        if (ranWithNote)
        {
            return SetupStepStatuses.Green;
        }

        if (tables.Count > 0 && filled == tables.Count)
        {
            return SetupStepStatuses.Green;
        }

        if (filled > 0)
        {
            return SetupStepStatuses.Amber;
        }

        return hasMissing ? SetupStepStatuses.Grey : SetupStepStatuses.Red;
    }
}
