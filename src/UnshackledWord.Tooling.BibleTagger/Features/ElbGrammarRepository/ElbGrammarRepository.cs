using UnshackledWord.Domain.Models.Dbo;
using UnshackledWord.Domain.WebApi.BibleTagger.GetVerse;
using UnshackledWord.Domain.WebApi.BibleTagger.SaveElbGrammar;
using UnshackledWord.Tooling.BibleTagger.Features.ApplicationSetup;

namespace UnshackledWord.Tooling.BibleTagger.Features.ElbGrammarRepository;

public sealed class ElbGrammarRepository : IElbGrammarRepository
{
    private readonly HttpClient _httpClient;

    public ElbGrammarRepository(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.GetCoreApiClient();
    }

    public async Task<GetVerseForElbGrammarResponse> GetVerseAsync(int bookId, int chapter, int verse,
        CancellationToken token = default)
    {
        var response = await _httpClient.GetFromJsonAsync<GetVerseForElbGrammarResponse>($"bt/grammar/bookId/{bookId}/chapterId/{chapter}/verseId/{verse}", token);

        return response!;
    }

    public async Task<SaveElbGrammarResponse> SaveVerseAsync(List<Elb1871WordGrammarDto> elbWords,
        CancellationToken token = default)
    {
        var req = new SaveElbGrammarRequest { ElbWord = elbWords };
        var response = await _httpClient.PostAsJsonAsync($"bt/grammar", req, token);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SaveElbGrammarResponse>(token);

        return result ?? throw new ArgumentNullException();
    }
}
