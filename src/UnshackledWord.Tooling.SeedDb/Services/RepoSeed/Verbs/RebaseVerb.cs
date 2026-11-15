using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Infrastructure.Seed;

namespace UnshackledWord.Tooling.SeedDb.Services.RepoSeed.Verbs;

/// <summary>
/// <c>rebase --confirm-backup [--out DIR]</c>: the maintainer's step after a parser or base-text fix (roadmap 0010.11). The work is done by
/// <see cref="SeedRebaseService"/> (also behind the maintainer page): exports the seed, empties and rebuilds words and verses from the fixed
/// <c>assets</c>, imports morphology and mappings again through the anchored importers and recomputes the revision's base links; the
/// reports land in <c>temp/seed-report</c>. Refuses without <c>--confirm-backup</c> and while Mapping v2 holds rows. Not transactional as a
/// whole: if it fails after the rebuild started, the exported seed is the way back.
/// </summary>
public sealed class RebaseVerb : ISeedVerb
{
    private readonly SeedRebaseService _service;
    private readonly IFileService _fileService;
    private readonly DatabaseSeedSettings _settings;
    private readonly ILogger<RebaseVerb> _logger;

    public RebaseVerb(SeedRebaseService service, IFileService fileService, IOptions<AppSettings> options, ILogger<RebaseVerb> logger)
    {
        _service = service;
        _fileService = fileService;
        _settings = options.Value.DatabaseSeeding;
        _logger = logger;
    }

    public string Name => "rebase";

    public async Task<int> RunAsync(VerbArguments arguments, CancellationToken token)
    {
        var layout = new SeedLayout(_fileService, _settings, arguments.Get("--out"));
        var result = await _service.RebaseAsync(layout, arguments.Has("--confirm-backup"), null, token);

        if (result.Refused is not null)
        {
            _logger.LogError("Rebase refused ({Reason}); nothing was changed. Reports: {Reports}", result.Refused, layout.ReportRoot);

            return 2;
        }

        _logger.LogInformation("Reports: {Reports}. Refresh the data-quality snapshot (POST api/dataQuality/refresh).", layout.ReportRoot);

        return 0;
    }
}
