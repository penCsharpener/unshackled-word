using UnshackledWord.Application.Features.Notes;
using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Application.Repositories;

public interface ITskRepository
{
    /// <summary>
    /// TSK cross-references that start at verses in the given Elberfelder (HebRefId) range, inclusive.
    /// Source and targets are translated to HebRefId. Ordered by source verse, then TSK order; hidden links included, with their curation
    /// (<c>Rating</c>, <c>Comment</c>); <c>CrossReferenceCuration.Arrange</c> applies hiding and display order.
    /// </summary>
    Task<IReadOnlyList<TskCrossReference>> GetOutgoingAsync(int startHebRefId, int endHebRefId, int limit, CancellationToken token = default);

    /// <summary>
    /// The words of the revision variant active at <paramref name="level"/> (same selection as the reader and the notes, <c>RevisionSql.EffectiveVerses</c>)
    /// for these source verses of one chapter; verses without a variant at that level return nothing (roadmap 0024.5, word links on revision levels).
    /// </summary>
    Task<IReadOnlyList<NoteVariantWordRow>> GetVariantWordsAsync(IReadOnlyCollection<int> hebRefIds, int level, CancellationToken token = default);
}
