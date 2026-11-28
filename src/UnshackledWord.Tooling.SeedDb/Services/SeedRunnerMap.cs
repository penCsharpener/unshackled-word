using UnshackledWord.Seeding.Catalog;

namespace UnshackledWord.Tooling.SeedDb.Services;

/// <summary>
/// Runner types of catalog steps whose runner still lives in this project (0031.9 moves it into UnshackledWord.Seeding and
/// shrinks this map to nothing). The library catalog cannot name these types, so the CLI maps the step key here.
/// </summary>
public static class SeedRunnerMap
{
    public static readonly IReadOnlyDictionary<string, Type> Legacy = new Dictionary<string, Type>
    {
        ["verse-similarity"] = typeof(VerseSimilarity.VerseSimilarityRunner),
    };

    /// <summary>The runner of a step: the library type, else the legacy one; null for read-only rows and refresh-only steps (books, stats-views).</summary>
    public static Type? RunnerOf(SeedStep step) => step.Runner ?? (Legacy.TryGetValue(step.Key, out var type) ? type : null);

    /// <summary>The runner types of the whole catalog in run order.</summary>
    public static Type[] RunnerTypes() => [.. SeedCatalog.Steps.Select(RunnerOf).OfType<Type>()];
}
