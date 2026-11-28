namespace UnshackledWord.Seeding.Catalog;

/// <summary>One table a step fills; the setup page shows its row count.</summary>
public sealed record SeedTable(string Name);

/// <summary>
/// One step of the seed catalog (roadmap 0031, 03-seed-catalog.md). <see cref="Runner"/> is null for read-only rows (books) and for runners
/// that have not moved into this library yet; <see cref="Wired"/> says whether the setup page may run the step.
/// </summary>
public sealed record SeedStep(
    string Key,
    string Group,
    string Source,
    string? SourceHost,
    Type? Runner,
    IReadOnlyList<SeedTable> Tables,
    IReadOnlyList<string> Requires,
    bool Resumable,
    bool Wired,
    bool Runnable = true,
    string? Note = null);
