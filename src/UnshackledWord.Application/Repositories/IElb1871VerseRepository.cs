using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Application.Repositories;

public interface IElb1871VerseRepository
{
    /// <summary>Verses with HebRefId between start and end (inclusive), ordered by reference.</summary>
    Task<IReadOnlyList<ElbVerse>> GetByHebRefIdRangeAsync(int startRefId, int endRefId, int limit, CancellationToken token = default);

    /// <summary>Verses in the range as shown at the level: revision variant text where one applies, else Elb1871 (level 100 = all Elb1871).</summary>
    Task<IReadOnlyList<EffectiveElbVerse>> GetEffectiveByHebRefIdRangeAsync(int startRefId, int endRefId, int level, CancellationToken token = default);
}
