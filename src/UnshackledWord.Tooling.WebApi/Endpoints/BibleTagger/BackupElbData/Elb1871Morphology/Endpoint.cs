using FastEndpoints;
using UnshackledWord.Tooling.WebApi.Endpoints.BibleTagger.BackupElbData.Mappings;

namespace UnshackledWord.Tooling.WebApi.Endpoints.BibleTagger.BackupElbData.Elb1871Morphology;

public sealed class Endpoint : Ep.NoReq.NoRes
{
    private readonly BackupFileService _backupFileService;

    public Endpoint(BackupFileService backupFileService)
    {
        _backupFileService = backupFileService;
    }

    public override void Configure()
    {
        Post("dashboard/backup/elb1871morphology");
        Group<RouteGroupConfig>();
    }

    public override async Task<object?> ExecuteAsync(CancellationToken ct)
    {
        await _backupFileService.WriteElbMorphologyBackupAsync(ct);

        return null;
    }
}
