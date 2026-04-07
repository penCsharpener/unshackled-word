using FastEndpoints;

namespace UnshackledWord.Tooling.WebApi.Endpoints.BibleTagger.BackupElbData.Mappings;

public sealed class Endpoint : Ep.NoReq.NoRes
{
    private readonly BackupFileService _backupFileService;

    public Endpoint(BackupFileService backupFileService)
    {
        _backupFileService = backupFileService;
    }

    public override void Configure()
    {
        Post("dashboard/backup/mappings");
        Group<RouteGroupConfig>();
    }

    public override async Task<object?> ExecuteAsync(CancellationToken ct)
    {
        await _backupFileService.WriteBackupAsync(ct);

        return null;
    }
}
