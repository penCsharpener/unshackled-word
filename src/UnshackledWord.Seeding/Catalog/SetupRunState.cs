namespace UnshackledWord.Seeding.Catalog;

/// <summary>
/// What the setup jobs of this API process did: steps that failed (shown on the row, a failed step with rows is amber) and steps that ran to
/// the end (a step with a "nothing to import" note is then green at 0 rows). In memory only; the row counts stay the truth.
/// </summary>
public sealed class SetupRunState
{
    private readonly object _gate = new();
    private readonly HashSet<string> _failed = [];
    private readonly HashSet<string> _completed = [];
    private string? _current;

    /// <summary>The step a setup job works on right now, or null between jobs.</summary>
    public string? CurrentStep
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
        set
        {
            lock (_gate)
            {
                _current = value;
            }
        }
    }

    public void MarkFailed(string key)
    {
        lock (_gate)
        {
            _completed.Remove(key);
            _failed.Add(key);
        }
    }

    public void MarkCompleted(string key)
    {
        lock (_gate)
        {
            _failed.Remove(key);
            _completed.Add(key);
        }
    }

    public IReadOnlySet<string> FailedKeys()
    {
        lock (_gate)
        {
            return new HashSet<string>(_failed);
        }
    }

    public IReadOnlySet<string> CompletedKeys()
    {
        lock (_gate)
        {
            return new HashSet<string>(_completed);
        }
    }
}
