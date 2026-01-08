namespace UnshackledWord.Application.Repositories;

public interface IElbDashboardRepository
{
    Task<Dictionary<int, string>> CreateBackupAsync(CancellationToken ct);
}
