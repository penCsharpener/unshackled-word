using UnshackledWord.Domain.Models.Dbo;
using UnshackledWord.Domain.WebApi.MetaBible.GetBibleBooks;
using UnshackledWord.Tooling.BibleTagger.Features.ApplicationSetup;

namespace UnshackledWord.Tooling.BibleTagger.Features.Elb1871SrTaggerRepository;

public sealed class MetaBibleRepository : IMetaBibleRepository
{
    private readonly HttpClient _httpClient;

    public MetaBibleRepository(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.GetCoreApiClient();
    }

    public async Task<List<BibleBookDbo>> GetBibleBooksAsync(int languageId, CancellationToken token = default)
    {
        var response = await _httpClient.GetFromJsonAsync<GetBibleBooksResponse>($"meta-bible/bible-books/language/{languageId}", token);

        return response!.BibleBooks.ToList();
    }
}