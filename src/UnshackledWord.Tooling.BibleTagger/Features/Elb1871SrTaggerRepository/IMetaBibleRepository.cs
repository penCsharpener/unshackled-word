using UnshackledWord.Domain.Models.Dbo;

namespace UnshackledWord.Tooling.BibleTagger.Features.Elb1871SrTaggerRepository;

public interface IMetaBibleRepository
{
    Task<List<BibleBookDbo>> GetBibleBooksAsync(int languageId, CancellationToken token = default);
}