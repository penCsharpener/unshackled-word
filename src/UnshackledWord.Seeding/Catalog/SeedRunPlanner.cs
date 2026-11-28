using UnshackledWord.Domain.WebApi.Admin;

namespace UnshackledWord.Seeding.Catalog;

/// <summary>The steps to run (catalog order) and the job kind, or the reason the request is rejected (HTTP 400).</summary>
public sealed record SeedRunPlan(IReadOnlyList<string> Keys, string Kind, string? Error)
{
    public bool IsValid => Error is null;

    public static SeedRunPlan Invalid(string error) => new([], string.Empty, error);
}

/// <summary>
/// Turns a <see cref="SetupRunRequest"/> into the steps to run (roadmap 0031.6). Pure: it reads the current step states, never the database.
/// Explicit keys must be red (or grey with every prerequisite green or earlier in the plan; amber only for resumable steps), wired and runnable.
/// Groups and "all" take the wired, runnable steps that are red or grey and skip the rest (green steps, unwired steps, steps that wait on an unwired one).
/// A step whose prerequisite is neither green nor earlier in the plan is rejected (explicit keys) or skipped (group / all).
/// </summary>
public static class SeedRunPlanner
{
    public const string NotRed = "not red";

    public static SeedRunPlan Plan(IReadOnlyList<SeedStep> catalog, IReadOnlyList<SetupStepResponse> states, SetupRunRequest request)
    {
        var modes = (request.Keys is { Count: > 0 } ? 1 : 0) + (string.IsNullOrWhiteSpace(request.Group) ? 0 : 1) + (request.All ? 1 : 0);
        if (modes != 1)
        {
            return SeedRunPlan.Invalid("Give exactly one of keys, group or all.");
        }

        var byKey = states.ToDictionary(s => s.Key);
        return request.Keys is { Count: > 0 } keys
            ? PlanKeys(catalog, byKey, keys)
            : PlanBulk(catalog, byKey, request);
    }

    private static SeedRunPlan PlanKeys(IReadOnlyList<SeedStep> catalog, Dictionary<string, SetupStepResponse> byKey, List<string> keys)
    {
        foreach (var key in keys)
        {
            if (!byKey.TryGetValue(key, out var state))
            {
                return SeedRunPlan.Invalid($"Unknown step '{key}'.");
            }

            if (!state.Wired)
            {
                return SeedRunPlan.Invalid($"Step '{key}' is not wired yet.");
            }

            if (!state.Runnable)
            {
                return SeedRunPlan.Invalid($"Step '{key}' cannot be run (read-only).");
            }

            var runnable = state.Status is SetupStepStatuses.Red or SetupStepStatuses.Grey
                || (state.Status == SetupStepStatuses.Amber && state.Resumable);
            if (!runnable)
            {
                return SeedRunPlan.Invalid($"Step '{key}' is {NotRed} (status {state.Status}).");
            }
        }

        var ordered = catalog.Where(s => keys.Contains(s.Key)).Select(s => s.Key).ToList();
        var inPlan = new HashSet<string>();
        foreach (var key in ordered)
        {
            var missing = Missing(byKey[key], byKey, inPlan);
            if (missing.Count > 0)
            {
                return SeedRunPlan.Invalid($"Step '{key}' needs {string.Join(", ", missing)} first.");
            }

            inPlan.Add(key);
        }

        return new SeedRunPlan(ordered, ordered.Count == 1 ? ordered[0] : string.Join(',', ordered), null);
    }

    private static SeedRunPlan PlanBulk(IReadOnlyList<SeedStep> catalog, Dictionary<string, SetupStepResponse> byKey, SetupRunRequest request)
    {
        if (!request.All && !SetupGroups.All.Contains(request.Group!))
        {
            return SeedRunPlan.Invalid($"Unknown group '{request.Group}'.");
        }

        var inPlan = new List<string>();
        var inPlanSet = new HashSet<string>();
        foreach (var step in catalog)
        {
            if (!request.All && step.Group != request.Group)
            {
                continue;
            }

            var state = byKey[step.Key];
            var eligible = state.Wired && state.Runnable && state.Status is SetupStepStatuses.Red or SetupStepStatuses.Grey;
            if (!eligible || Missing(state, byKey, inPlanSet).Count > 0)
            {
                continue;
            }

            inPlan.Add(step.Key);
            inPlanSet.Add(step.Key);
        }

        return inPlan.Count == 0
            ? SeedRunPlan.Invalid($"Nothing to run: no red step is ready.")
            : new SeedRunPlan(inPlan, request.All ? "all" : $"group:{request.Group}", null);
    }

    private static List<string> Missing(SetupStepResponse state, Dictionary<string, SetupStepResponse> byKey, HashSet<string> inPlan)
        => [.. state.Requires.Where(r => !inPlan.Contains(r) && byKey[r].Status != SetupStepStatuses.Green)];
}
