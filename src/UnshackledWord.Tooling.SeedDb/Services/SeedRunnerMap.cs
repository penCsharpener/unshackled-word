using UnshackledWord.Seeding.Catalog;

namespace UnshackledWord.Tooling.SeedDb.Services;

/// <summary>
/// Runner types of catalog steps whose runner still lives in this project (0031.7 to 0031.9 move them into UnshackledWord.Seeding and
/// shrink this map to nothing). The library catalog cannot name these types, so the CLI maps the step key here.
/// </summary>
public static class SeedRunnerMap
{
    public static readonly IReadOnlyDictionary<string, Type> Legacy = new Dictionary<string, Type>
    {
        ["morphology"] = typeof(RepoSeed.Morphology.MorphologySeedRunner),
        ["tsk"] = typeof(Tsk.TskRunner),
        ["tsk-curation"] = typeof(Tsk.TskCurationSeedRunner),
        ["sr-gnt"] = typeof(StatisticalRestorationGnt.SrRunner),
        ["sbl"] = typeof(SBL.SblRunner),
        ["step-text"] = typeof(StepBible.StepDataBibleTextImporter),
        ["step-strongs"] = typeof(StepBible.StepDataStrongsImporter),
        ["step-morphology"] = typeof(StepBible.StepDataMorphologyImporter),
        ["step-lexicon"] = typeof(StepBible.StepDataLexiconImporter),
        ["step-relationship"] = typeof(StepBible.StepDataRelationshipImporter),
        ["verse-similarity"] = typeof(VerseSimilarity.VerseSimilarityRunner),
        ["lxx-text"] = typeof(Swete.LxxTextImporter),
        ["lxx-tags"] = typeof(Swete.LxxTagSeedRunner),
        ["step-strongs-text"] = typeof(StepBible.StrongsToText.StepStrongsImport),
        ["ai-mappings"] = typeof(AiMappingImport.AiMappingImportRunner),
        ["revision-seed"] = typeof(RevisionSeed.RevisionSeedRunner),
    };

    /// <summary>The runner of a step: the library type, else the legacy one; null for read-only rows and refresh-only steps (books, stats-views).</summary>
    public static Type? RunnerOf(SeedStep step) => step.Runner ?? (Legacy.TryGetValue(step.Key, out var type) ? type : null);

    /// <summary>The runner types of the whole catalog in run order.</summary>
    public static Type[] RunnerTypes() => [.. SeedCatalog.Steps.Select(RunnerOf).OfType<Type>()];
}
