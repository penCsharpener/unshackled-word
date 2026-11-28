namespace UnshackledWord.Seeding;

/// <summary>Config section <c>Seeding</c>: settings of the seed jobs that run inside the API (roadmap 0031.6).</summary>
public sealed class SeedingOptions
{
    public const string SectionName = "Seeding";
    public const int DefaultCommandTimeoutSeconds = 900;

    /// <summary>Command timeout of the connections a seed job uses (the word bulk insert needs more than the default 30 s).</summary>
    public int CommandTimeoutSeconds { get; set; } = DefaultCommandTimeoutSeconds;
}
