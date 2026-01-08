using UnshackledWord.Domain.Models.Dbo;
using UnshackledWord.Domain.WebApi.BibleTagger.BackupElbData;
using UnshackledWord.Domain.WebApi.BibleTagger.CreateElbSrMapping;
using UnshackledWord.Domain.WebApi.BibleTagger.GetVerse;

namespace UnshackledWord.Tooling.BibleTagger.Features.Elb1871SrTaggerRepository;

public interface IElbSrTaggerRepository
{
    Task<GetVerseForElbTaggingResponse> GetVerseAsync(int bookId, int chapter, int verse, CancellationToken token = default);

    Task<CreateElbSrResponse> CreateMappingAsync(Elb1871WordDbo elbWords, SrGntWordDbo srWords,
        CancellationToken token = default);

    Task<BackupElbDataResponse> BackupDataAsync(CancellationToken token = default);
}
