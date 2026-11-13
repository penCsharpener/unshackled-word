namespace UnshackledWord.Tooling.SeedDb.Services.RepoSeed.Verbs;

/// <summary>Runs the verb named on the command line instead of the seeding pipeline and sets the exit code.</summary>
public sealed class VerbWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly VerbArguments _arguments;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<VerbWorker> _logger;

    public VerbWorker(IServiceScopeFactory scopeFactory, VerbArguments arguments, IHostApplicationLifetime lifetime, ILogger<VerbWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _arguments = arguments;
        _lifetime = lifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var verbs = scope.ServiceProvider.GetServices<ISeedVerb>().ToList();
            var verb = verbs.FirstOrDefault(v => v.Name == _arguments.Verb);

            if (verb is null)
            {
                _logger.LogError("Unknown command {Verb}. Known commands: {Known}", _arguments.Verb, string.Join(", ", verbs.Select(v => v.Name)));
                Environment.ExitCode = 2;
                return;
            }

            Environment.ExitCode = await verb.RunAsync(_arguments, stoppingToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Command {Verb} failed", _arguments.Verb);
            Environment.ExitCode = 1;
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }
}
