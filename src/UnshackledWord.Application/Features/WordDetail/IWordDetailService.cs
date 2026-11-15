using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder.GetWordDetail;

namespace UnshackledWord.Application.Features.WordDetail;

public interface IWordDetailService
{
    /// <summary>Returns null when no Elberfelder word has this id.</summary>
    Task<GetWordDetailResponse?> GetAsync(int wordId, CancellationToken token = default);

    /// <summary>
    /// Builds the detail from already loaded rows: readings, STEP morphology, lexicon, renderings and cross references. Shared by the
    /// Elb1871 word detail and the revision word detail, which only differ in where word, rows and co-mapped words come from.
    /// </summary>
    Task<GetWordDetailResponse> BuildAsync(ElbWordRow word, IReadOnlyList<ReadingRowBase> rows, IReadOnlyList<CoMappedWordRow> coRows, string mappingSource, CancellationToken token = default);
}
