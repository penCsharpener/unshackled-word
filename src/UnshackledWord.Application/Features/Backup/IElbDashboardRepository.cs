namespace UnshackledWord.Application.Features.Backup;

public interface IElbDashboardRepository
{
    Task<Dictionary<int, List<ElbMappingBackup>>> CreateBackupAsync(CancellationToken ct = default);
}
