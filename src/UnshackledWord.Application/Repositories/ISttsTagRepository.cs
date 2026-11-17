using UnshackledWord.Domain.Models.Grammar;

namespace UnshackledWord.Application.Repositories;

/// <summary>Reads the "SttsTags" table (source of truth for the STTS tags).</summary>
public interface ISttsTagRepository
{
    Task<IReadOnlyList<SttsTag>> GetAllAsync(CancellationToken ct);
}
