using UnshackledWord.Domain.WebApi.Admin;
using UnshackledWord.Seeding.ElberfelderParser;
using UnshackledWord.Seeding.RevisionSeed;
using UnshackledWord.Seeding.SttsSeed;

namespace UnshackledWord.Seeding.Catalog;

/// <summary>
/// Every step the setup page shows, in run order (roadmap 0031, 03-seed-catalog.md). The SeedDb CLI runs the steps with a runner in this order,
/// the page groups them by <see cref="SeedStep.Group"/>. Each step's <c>Requires</c> comes earlier in the list.
/// Runners that have not moved into this library yet (0031.7 to 0031.9) have no <c>Runner</c> here; SeedDb maps their keys to the types.
/// </summary>
public static class SeedCatalog
{
    private const string StepHost = "raw.githubusercontent.com";
    private const string GithubHost = "github.com";

    public static readonly IReadOnlyList<SeedStep> Steps =
    [
        // base: wired in 0031.5
        Step("books", SetupGroups.Base, SetupSources.Migration, null, [], ["BibleBooks", "Languages"], wired: true, runnable: false),
        Step("stts", SetupGroups.Base, SetupSources.Assets, typeof(SttsSeedRunner), [], ["SttsTags"], wired: true),
        Step("revision-levels", SetupGroups.Base, SetupSources.Assets, typeof(RevisionLevelSeedRunner), [], ["Revisions"], wired: true),
        Step("elb1871-text", SetupGroups.Base, SetupSources.Assets, typeof(ElberfelderTextRunner), ["books"], ["Elb1871Words", "BibleVerseCountingMapping"], wired: true, resumable: false),
        Step("elb1871-verses", SetupGroups.Base, SetupSources.Assets, typeof(Elb1871VerseRunner), ["elb1871-text"], ["Elb1871Verses"], wired: true),

        // not wired yet: the runners still live in Tooling.SeedDb (see SeedRunnerMap there)
        Step("morphology", SetupGroups.Other, SetupSources.Assets, null, ["elb1871-text"], ["Elb1871Morphology"]),
        Step("tsk", SetupGroups.Other, SetupSources.Assets, null, ["books"], ["Tsk"]),
        Step("tsk-curation", SetupGroups.Other, SetupSources.Assets, null, ["tsk", "elb1871-text"], ["TskCurations"]),
        Step("sr-gnt", SetupGroups.Other, SetupSources.Download, null, ["books"], ["SrGntWords"], sourceHost: GithubHost),
        Step("sbl", SetupGroups.Other, SetupSources.Download, null, ["books"], ["SblText", "SblApparatus"], sourceHost: GithubHost),
        Step("step-text", SetupGroups.Step, SetupSources.Download, null, ["books"], ["StepGreekWords", "StepHebrewWords", "StepGreekWordEditions", "GreekEditions"], sourceHost: StepHost, resumable: false),
        Step("step-strongs", SetupGroups.Step, SetupSources.Download, null, ["step-text"], ["StepUnifiedStrongs"], sourceHost: StepHost),
        Step("step-morphology", SetupGroups.Step, SetupSources.Download, null, ["step-text"], ["StepGreekMorphology", "StepHebrewMorphology"], sourceHost: StepHost),
        Step("step-lexicon", SetupGroups.Step, SetupSources.Download, null, ["step-strongs"], ["StepStrongsLexicon"], sourceHost: StepHost),
        Step("step-relationship", SetupGroups.Step, SetupSources.Download, null, ["step-text"], ["StepHebrewWordsNormalized", "StepHebrewWordsNormalizedToHebrewWords"], sourceHost: StepHost),
        // after the Hebrew normalization above: the Old Testament tokens come from StepHebrewWordsNormalized (roadmap 0027.2)
        Step("verse-similarity", SetupGroups.Derived, SetupSources.Assets, null, ["step-relationship"], ["VerseSimilarities"]),
        // after the STEP download, which holds TVTMS (roadmap 0028.3)
        Step("lxx-text", SetupGroups.Other, SetupSources.Download, null, ["step-text"], ["LxxVerses", "LxxWords"], sourceHost: GithubHost),
        // the confirmed LXX tags and links of the repo seed (roadmap 0028.8); links may be empty
        Step("lxx-tags", SetupGroups.Other, SetupSources.Assets, null, ["lxx-text"], ["LxxWordTags", "LxxHebrewLinks"], note: SetupNotes.NothingToImport),
        Step("step-strongs-text", SetupGroups.Step, SetupSources.Download, null, ["step-text", "elb1871-text"], ["StepStrongsToText"], sourceHost: StepHost),
        Step("ai-mappings", SetupGroups.Other, SetupSources.Assets, null, ["elb1871-text", "step-text"], ["Elb1871GreekMapping", "Elb1871HebrewMapping"], resumable: false),
        Step("revision-seed", SetupGroups.Other, SetupSources.Assets, null, ["revision-levels", "ai-mappings"], ["RevisionVerses", "RevisionWords", "RevisionMapping"], note: SetupNotes.NothingToImport),
        // a refresh of the materialized views, no runner class (0031.9)
        Step("stats-views", SetupGroups.Derived, SetupSources.Assets, null, ["ai-mappings", "morphology"], ["StatsStrongs", "StatsStrongsRenderings", "StatsGermanLemma", "StatsGermanForms", "StatsVocabRenderings", "DqWordFacts"]),
    ];

    public static SeedStep? Find(string key) => Steps.FirstOrDefault(s => s.Key == key);

    private static SeedStep Step(
        string key,
        string group,
        string source,
        Type? runner,
        string[] requires,
        string[] tables,
        string? sourceHost = null,
        bool resumable = true,
        bool wired = false,
        bool runnable = true,
        string? note = null)
        => new(key, group, source, sourceHost, runner, [.. tables.Select(t => new SeedTable(t))], requires, resumable, wired, runnable, note);
}
