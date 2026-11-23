using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Tsk;

namespace UnshackledWord.Application.Features.CrossReferences;

/// <summary>
/// Display order of curated TSK links (roadmap 0024.3), shared by the chapter list, the verse texts, the word box and the MCP tool:
/// hidden links dropped (unless a curator asks for them), then per source verse the highlighted links first and the rest after, each in TSK order.
/// </summary>
public static class CrossReferenceCuration
{
    /// <param name="rows">Rows as <c>ITskRepository.GetOutgoingAsync</c> returns them: grouped by source verse, TSK order within.</param>
    /// <param name="includeHidden">Curators only: keep hidden links (they keep their TSK position among the non-highlighted ones).</param>
    public static List<TskCrossReference> Arrange(IEnumerable<TskCrossReference> rows, bool includeHidden = false) =>
        rows
            .Where(r => includeHidden || r.Rating != TskRatings.Hide)
            .GroupBy(r => r.SourceHebRefId)
            .SelectMany(verse => verse.OrderBy(r => r.Rating == TskRatings.Highlight ? 0 : 1))
            .ToList();
}
