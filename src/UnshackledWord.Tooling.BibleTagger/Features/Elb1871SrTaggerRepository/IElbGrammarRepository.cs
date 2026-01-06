using UnshackledWord.Domain.Models.Dbo;
using UnshackledWord.Domain.WebApi.BibleTagger.GetVerse;
using UnshackledWord.Domain.WebApi.BibleTagger.SaveElbGrammar;

namespace UnshackledWord.Tooling.BibleTagger.Features.Elb1871SrTaggerRepository;

public interface IElbGrammarRepository
{
    Task<GetVerseForElbGrammarResponse> GetVerseAsync(int bookId, int chapter, int verse,
        CancellationToken token = default);

    Task<SaveElbGrammarResponse> SaveVerseAsync(List<Elb1871WordGrammarDto> elbWords,
        CancellationToken token = default);
}
