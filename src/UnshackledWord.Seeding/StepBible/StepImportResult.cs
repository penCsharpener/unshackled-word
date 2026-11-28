namespace UnshackledWord.Seeding.StepBible;

/// <summary>
/// What the STEP word import inserted in this SeedDb run (scoped: <c>SeedingService</c> runs all runners in one scope). Read by later runners
/// that rebuild derived data only after a real import, such as the verse similarity (roadmap 0027.2).
/// </summary>
public sealed class StepImportResult
{
    public int InsertedGreekWords { get; set; }

    public int InsertedHebrewWords { get; set; }

    public bool WordsInserted => InsertedGreekWords > 0 || InsertedHebrewWords > 0;
}
