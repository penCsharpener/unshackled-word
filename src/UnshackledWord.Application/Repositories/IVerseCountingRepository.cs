using UnshackledWord.Domain.Models.BibleStructure.References;

namespace UnshackledWord.Application.Repositories;

public interface IVerseCountingRepository
{
    /// <summary>Rows of <c>BibleVerseCountingMapping</c> whose English (<c>LxxRefId</c>) and Hebrew (<c>HebRefId</c>) ref ids differ.</summary>
    Task<IReadOnlyList<VerseCountingRow>> GetDifferingAsync(CancellationToken token = default);
}
