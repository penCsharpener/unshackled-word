using UnshackledWord.Domain.Models.Dbo;
using UnshackledWord.Domain.WebApi.BibleTagger.CreateElbSrMapping;
using UnshackledWord.Domain.WebApi.BibleTagger.GetVerse;
using UnshackledWord.Tooling.BibleTagger.Features.ApplicationSetup;

namespace UnshackledWord.Tooling.BibleTagger.Features.Elb1871SrTaggerRepository;

public sealed class ElbSrTaggerRepository : IElbSrTaggerRepository
{
    private readonly HttpClient _httpClient;

    public ElbSrTaggerRepository(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.GetCoreApiClient();
    }

    public async Task<GetVerseResponse> GetVerseAsync(int bookId, int chapter, int verse, CancellationToken token = default)
    {
        var response = await _httpClient.GetFromJsonAsync<GetVerseResponse>($"bt/bookId/{bookId}/chapterId/{chapter}/verseId/{verse}", token);

        return response!;
    }

    public async Task<CreateElbSrResponse> CreateMappingAsync(Elb1871WordDbo elbWords, SrGntWordDbo srWords,
        CancellationToken token = default)
    {
        var req = new CreateElbSrRequest { Elb1871Word = elbWords, SrGntWord = srWords };
        var responseMessage = await _httpClient.PostAsJsonAsync($"bt/mapping/create", req, token);

        responseMessage.EnsureSuccessStatusCode();

        var response = await responseMessage.Content.ReadFromJsonAsync<CreateElbSrResponse>(token);

        return response ?? new();
    }
}
