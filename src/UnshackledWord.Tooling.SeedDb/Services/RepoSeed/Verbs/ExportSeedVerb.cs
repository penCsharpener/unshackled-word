using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Infrastructure.Seed;

namespace UnshackledWord.Tooling.SeedDb.Services.RepoSeed.Verbs;

/// <summary>
/// <c>export-seed [--only morphology|mappings|revision] [--out DIR]</c>: writes the repo seed from the configured database (read-only) into
/// <c>assets/seed</c> (or DIR). The maintainer runs it and commits the diff.
/// </summary>
public sealed class ExportSeedVerb : ISeedVerb
{
    private readonly SeedExportRunner _runner;
    private readonly IFileService _fileService;
    private readonly DatabaseSeedSettings _settings;
    private readonly ILogger<ExportSeedVerb> _logger;

    public ExportSeedVerb(SeedExportRunner runner, IFileService fileService, IOptions<AppSettings> options, ILogger<ExportSeedVerb> logger)
    {
        _runner = runner;
        _fileService = fileService;
        _settings = options.Value.DatabaseSeeding;
        _logger = logger;
    }

    public string Name => "export-seed";

    public async Task<int> RunAsync(VerbArguments arguments, CancellationToken token)
    {
        var only = arguments.Get("--only") ?? "all";

        if (only is not ("all" or "morphology" or "mappings" or "revision"))
        {
            _logger.LogError("--only {Only} is not supported; supported: morphology, mappings, revision (default: all)", only);
            return 2;
        }

        var layout = new SeedLayout(_fileService, _settings, arguments.Get("--out"));
        _logger.LogInformation("Exporting {Kind} seed to {Path}", only, layout.SeedRoot);

        var kind = only switch
        {
            "morphology" => SeedExportKind.Morphology,
            "mappings" => SeedExportKind.Mappings,
            "revision" => SeedExportKind.Revision,
            _ => SeedExportKind.All
        };

        await _runner.ExportAsync(kind, layout, token);

        return 0;
    }
}
