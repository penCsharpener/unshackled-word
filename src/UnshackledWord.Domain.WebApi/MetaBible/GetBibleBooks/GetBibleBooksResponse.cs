using UnshackledWord.Domain.Models.Dbo;

namespace UnshackledWord.Domain.WebApi.MetaBible.GetBibleBooks;

public sealed class GetBibleBooksResponse
{
    public IEnumerable<BibleBookDbo> BibleBooks { get; set; } = default!;
}