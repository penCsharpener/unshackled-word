using UnshackledWord.Domain.Models.Dbo;

namespace UnshackledWord.Application.Repositories;

public interface IElb1871TaggingRepository
{
    Task<int> BulkInsertAsync(List<Elb1871SrGntTaggingDbo> taggings, CancellationToken token = default);

    Task<CreateMappingResult> CreateSingleMappingAsync(Elb1871WordDbo elbWord, SrGntWordDbo srWord,
        CancellationToken token = default);
    Task<CreateMappingResult> CreateMappingsAsync(Elb1871WordDbo elbWord, SrGntWordDbo srWord, CancellationToken token = default);
}
