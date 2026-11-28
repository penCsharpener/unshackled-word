namespace UnshackledWord.Application.Abstractions;

/// <summary>
/// Told after a seed step wrote data (roadmap 0031.6), so in-process caches that were filled while the tables were empty
/// (<c>SourceFormIndex</c>, <c>StrongsLexiconIndex</c>, <c>SttsDriftCheck</c>) reload. Implementations must not throw for ordinary failures;
/// the caller logs and ignores exceptions so a broken cache never fails a seed job.
/// </summary>
public interface ISeedDataChanged
{
    Task SeedDataChangedAsync(string stepKey, CancellationToken token);
}
