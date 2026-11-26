using UnshackledWord.Infrastructure.Seed;
using UnshackledWord.Infrastructure.Seed.Lxx;

namespace UnshackledWord.Tooling.SeedDb.Services.RepoSeed.Verbs;

/// <summary>
/// <c>lxx-seed export|import [--exact] [--dir DIR]</c> (roadmap 0028.8): the confirmed LXX tags and links to or from <c>assets/seed/lxx</c> (or DIR/lxx), the same
/// code as the maintainer page. <c>import</c> merges; <c>--exact</c> is "Exakt wie Datei" (deletes confirmed rows the files lack, proposals stay).
/// Exit codes: 0 done, 1 failed, 2 usage.
/// </summary>
public sealed class LxxSeedVerb : ISeedVerb
{
    private readonly LxxSeedService _service;
    private readonly ILogger<LxxSeedVerb> _logger;

    public LxxSeedVerb(LxxSeedService service, ILogger<LxxSeedVerb> logger)
    {
        _service = service;
        _logger = logger;
    }

    public string Name => "lxx-seed";

    public async Task<int> RunAsync(VerbArguments arguments, CancellationToken token)
    {
        var action = arguments.Has("export") ? "export" : arguments.Has("import") ? "import" : null;
        if (action is null)
        {
            _logger.LogError("Usage: lxx-seed export|import [--exact] [--dir DIR]");
            return 2;
        }

        try
        {
            var dir = arguments.Get("--dir");
            var layout = dir is null ? _service.CreateRepoLayout() : new SeedLayout(new Infrastructure.Services.FileService(), new Domain.Models.Settings.DatabaseSeedSettings(), dir);
            var progress = new Progress<string>(line => _logger.LogInformation("{Line}", line));
            var report = action == "export"
                ? await _service.ExportAsync(layout, progress, token)
                : await _service.ImportAsync(arguments.Has("--exact"), layout, progress, token);
            _logger.LogInformation("lxx-seed {Action}: {Counts}", action, string.Join(", ", report.Counts.Select(kv => $"{kv.Key}={kv.Value}")));
            return 0;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "lxx-seed {Action} failed", action);
            return 1;
        }
    }
}
