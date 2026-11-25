namespace UnshackledWord.Tooling.SeedDb.Services.RepoSeed.Verbs;

/// <summary>A maintainer command of the SeedDb tool (<c>dotnet run --project src/UnshackledWord.Tooling.SeedDb -- export-seed</c>).</summary>
public interface ISeedVerb
{
    /// <summary>The first command line argument that selects this verb.</summary>
    string Name { get; }

    /// <summary>Returns the process exit code.</summary>
    Task<int> RunAsync(VerbArguments arguments, CancellationToken token);
}

/// <summary>
/// A command that needs no database. <see cref="VerbWorker"/> looks these up first and resolves them alone, because resolving the other
/// verbs opens a database connection (their repositories connect in the constructor). Register as <see cref="IOfflineSeedVerb"/> only.
/// </summary>
public interface IOfflineSeedVerb : ISeedVerb;

/// <summary>The command line: the verb is the first argument (when it is not an option), the rest are <c>--name value</c> options and bare flags.</summary>
public sealed class VerbArguments
{
    private readonly string[] _args;

    public VerbArguments(string[] args)
    {
        _args = args;
    }

    public string? Verb => _args.Length > 0 && !_args[0].StartsWith('-') ? _args[0] : null;

    public bool Has(string option) => _args.Contains(option, StringComparer.Ordinal);

    public string? Get(string option)
    {
        var index = Array.IndexOf(_args, option);

        return index >= 0 && index + 1 < _args.Length && !_args[index + 1].StartsWith("--", StringComparison.Ordinal)
            ? _args[index + 1]
            : null;
    }

    /// <summary>Every value of a repeatable option (<c>--show a --show b</c>), in command line order.</summary>
    public IReadOnlyList<string> GetAll(string option)
    {
        var values = new List<string>();
        for (var i = 0; i + 1 < _args.Length; i++)
        {
            if (_args[i] == option && !_args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                values.Add(_args[i + 1]);
            }
        }

        return values;
    }
}
