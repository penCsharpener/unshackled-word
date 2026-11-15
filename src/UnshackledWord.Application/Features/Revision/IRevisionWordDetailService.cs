using UnshackledWord.Domain.WebApi.Elberfelder.GetWordDetail;

namespace UnshackledWord.Application.Features.Revision;

public interface IRevisionWordDetailService
{
    /// <summary>Word detail of a revision word; null when the id does not exist.</summary>
    Task<GetWordDetailResponse?> GetAsync(int revisionWordId, CancellationToken token = default);
}
