using UnshackledWord.Domain.WebApi.Admin;
using UnshackledWord.Seeding.Derived;
using UnshackledWord.Seeding.ElberfelderParser;
using UnshackledWord.Seeding.AiMappingImport;
using UnshackledWord.Seeding.Morphology;
using UnshackledWord.Seeding.RevisionSeed;
using UnshackledWord.Seeding.SBL;
using UnshackledWord.Seeding.StatisticalRestorationGnt;
using UnshackledWord.Seeding.StepBible;
using UnshackledWord.Seeding.StepBible.StrongsToText;
using UnshackledWord.Seeding.SttsSeed;
using UnshackledWord.Seeding.Swete;
using UnshackledWord.Seeding.Tsk;

namespace UnshackledWord.Seeding.Catalog;

/// <summary>
/// Every step the setup page shows, in run order (roadmap 0031, 03-seed-catalog.md). The SeedDb CLI runs the steps with a runner in this order,
/// the page groups them by <see cref="SeedStep.Group"/>. Each step's <c>Requires</c> comes earlier in the list.
/// Every runnable step has its runner here; only the read-only row (books) has none.
/// </summary>
public static class SeedCatalog
{
    // the STEP files are requested from github.com/STEPBible/STEPBible-Data (raw/refs/heads/master), which redirects to raw.githubusercontent.com
    private const string StepHost = "github.com";
    private const string GithubHost = "github.com";

    public static readonly IReadOnlyList<SeedStep> Steps =
    [
        // base: wired in 0031.5
        Step("books", SetupGroups.Base, SetupSources.Migration, null, [], ["BibleBooks", "Languages"], wired: true, runnable: false),
        Step("stts", SetupGroups.Base, SetupSources.Assets, typeof(SttsSeedRunner), [], ["SttsTags"], wired: true),
        Step("revision-levels", SetupGroups.Base, SetupSources.Assets, typeof(RevisionLevelSeedRunner), [], ["Revisions"], wired: true),
        Step("elb1871-text", SetupGroups.Base, SetupSources.Assets, typeof(ElberfelderTextRunner), ["books"], ["Elb1871Words", "BibleVerseCountingMapping"], wired: true, resumable: false),
        Step("elb1871-verses", SetupGroups.Base, SetupSources.Assets, typeof(Elb1871VerseRunner), ["elb1871-text"], ["Elb1871Verses"], wired: true),

        // step: wired in 0031.7
        // other (0031.8): the runners gate on "table empty" (one transaction or one bulk insert per table), so an amber step is not resumable, except SBL (one gate per table)
        Step("morphology", SetupGroups.Other, SetupSources.Assets, typeof(MorphologySeedRunner), ["elb1871-text"], ["Elb1871Morphology"], wired: true, resumable: false),
        Step("tsk", SetupGroups.Other, SetupSources.Assets, typeof(TskRunner), ["books"], ["Tsk"], wired: true, resumable: false),
        // assets/seed/tsk/curations.csv is a change file that may not exist (then there is nothing to apply); applied only while TskCurations is empty
        Step("tsk-curation", SetupGroups.Other, SetupSources.Assets, typeof(TskCurationSeedRunner), ["tsk", "elb1871-text"], ["TskCurations"], wired: true, resumable: false, note: SetupNotes.NothingToImport),
        Step("sr-gnt", SetupGroups.Other, SetupSources.Download, typeof(SrRunner), ["books"], ["SrGntWords"], sourceHost: GithubHost, wired: true, resumable: false),
        Step("sbl", SetupGroups.Other, SetupSources.Download, typeof(SblRunner), ["books"], ["SblText", "SblApparatus"], sourceHost: GithubHost, wired: true),
        // GreekEditions (the edition catalog) is filled by migration 0047, so it is not a table of this step: it would turn an empty database amber
        Step("step-text", SetupGroups.Step, SetupSources.Download, typeof(StepDataBibleTextImporter), ["books"], ["StepGreekWords", "StepHebrewWords", "StepGreekWordEditions"], sourceHost: StepHost, wired: true),
        Step("step-strongs", SetupGroups.Step, SetupSources.Download, typeof(StepDataStrongsImporter), ["step-text"], ["StepUnifiedStrongs"], sourceHost: StepHost, wired: true),
        Step("step-morphology", SetupGroups.Step, SetupSources.Download, typeof(StepDataMorphologyImporter), ["step-text"], ["StepGreekMorphology", "StepHebrewMorphology"], sourceHost: StepHost, wired: true),
        Step("step-lexicon", SetupGroups.Step, SetupSources.Download, typeof(StepDataLexiconImporter), ["step-strongs"], ["StepStrongsLexicon"], sourceHost: StepHost, wired: true),
        Step("step-relationship", SetupGroups.Step, SetupSources.Download, typeof(StepDataRelationshipImporter), ["step-text"], ["StepHebrewWordsNormalized", "StepHebrewWordsNormalizedToHebrewWords"], sourceHost: StepHost, wired: true),
        // after the Hebrew normalization above: the Old Testament tokens come from StepHebrewWordsNormalized (roadmap 0027.2)
        Step("verse-similarity", SetupGroups.Derived, SetupSources.Assets, typeof(VerseSimilarityRunner), ["step-relationship"], ["VerseSimilarities"], wired: true),
        // after the STEP download, which holds TVTMS (roadmap 0028.3)
        Step("lxx-text", SetupGroups.Other, SetupSources.Download, typeof(LxxTextImporter), ["step-text"], ["LxxVerses", "LxxWords"], sourceHost: GithubHost, wired: true, resumable: false),
        // the confirmed LXX tags and links of the repo seed (roadmap 0028.8); applied only while no tag is confirmed, links may be empty
        Step("lxx-tags", SetupGroups.Other, SetupSources.Assets, typeof(LxxTagSeedRunner), ["lxx-text"], ["LxxWordTags", "LxxHebrewLinks"], wired: true, note: SetupNotes.NothingToImport),
        Step("step-strongs-text", SetupGroups.Step, SetupSources.Download, typeof(StepStrongsImport), ["step-text", "elb1871-text"], ["StepStrongsToText"], sourceHost: StepHost, wired: true),
        Step("ai-mappings", SetupGroups.Other, SetupSources.Assets, typeof(AiMappingImportRunner), ["elb1871-text", "step-text"], ["Elb1871GreekMapping", "Elb1871HebrewMapping"], wired: true, resumable: false),
        // an empty assets/seed/revision (levels only) has nothing to import; on filled tables the runner is the rolling update
        Step("revision-seed", SetupGroups.Other, SetupSources.Assets, typeof(RevisionSeedRunner), ["revision-levels", "ai-mappings"], ["RevisionVerses", "RevisionWords", "RevisionMapping"], wired: true, note: SetupNotes.NothingToImport),
        // the refresh of the six materialized views (DqWordFacts first, StatsVocabRenderings last); idempotent, so the one re-runnable step (0031.9)
        Step("stats-views", SetupGroups.Derived, SetupSources.Assets, typeof(StatsViewsRunner), ["ai-mappings", "morphology"], ["DqWordFacts", "StatsStrongsRenderings", "StatsStrongs", "StatsGermanLemma", "StatsGermanForms", "StatsVocabRenderings"], wired: true, rerunnable: true),
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
        string? note = null,
        bool rerunnable = false)
        => new(key, group, source, sourceHost, runner, [.. tables.Select(t => new SeedTable(t))], requires, resumable, wired, runnable, note, rerunnable);
}
