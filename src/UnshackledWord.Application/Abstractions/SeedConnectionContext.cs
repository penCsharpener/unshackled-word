namespace UnshackledWord.Application.Abstractions;

/// <summary>
/// Lets a seed job use its own connection string (long <c>Command Timeout</c>, roadmap 0031.6) without changing the singleton
/// <see cref="IDbConnectionFactory"/> for everyone else: while a job's work runs inside <see cref="Use"/>, the factory builds its
/// connections from <see cref="Current"/> (flows with the async call chain only).
/// </summary>
public static class SeedConnectionContext
{
    private static readonly AsyncLocal<string?> CurrentValue = new();

    /// <summary>The connection string of the surrounding seed job, or null outside a job.</summary>
    public static string? Current => CurrentValue.Value;

    public static IDisposable Use(string connectionString)
    {
        var previous = CurrentValue.Value;
        CurrentValue.Value = connectionString;
        return new Restore(previous);
    }

    private sealed class Restore(string? previous) : IDisposable
    {
        public void Dispose() => CurrentValue.Value = previous;
    }
}
