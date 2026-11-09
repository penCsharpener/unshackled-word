using UnshackledWord.Domain.WebApi.Elberfelder.GetWordDetail;

namespace UnshackledWord.Application.Features.WordDetail;

public interface IWordDetailService
{
    /// <summary>Returns null when no Elberfelder word has this id.</summary>
    Task<GetWordDetailResponse?> GetAsync(int wordId, CancellationToken token = default);
}
