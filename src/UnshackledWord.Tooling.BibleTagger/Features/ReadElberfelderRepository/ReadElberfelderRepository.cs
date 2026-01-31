using UnshackledWord.Domain.WebApi.BibleTagger.Reading;
using UnshackledWord.Tooling.BibleTagger.Components.Pages.ReadElberfelder;
using UnshackledWord.Tooling.BibleTagger.Features.ApplicationSetup;

namespace UnshackledWord.Tooling.BibleTagger.Features.ReadElberfelderRepository;

public sealed class ReadElberfelderRepository : IReadElberfelderRepository
{
    private readonly HttpClient _httpClient;

    public ReadElberfelderRepository(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.GetCoreApiClient();
    }

    public async Task<GetWordsOfChapterResponse> GetWordsInChapterAsync(int bookId, int chapter, CancellationToken token = default)
    {
        var response = await _httpClient.GetFromJsonAsync<GetWordsOfChapterResponse>($"elberfelder/chapterWords/bookId/{bookId}/chapterId/{chapter}", token);

        return response;
    }
}

public sealed class VersePosition
{
    public int Verse { get; set; }
    public int PositionInVerse { get; set; }

    public static implicit operator VersePosition(string id)
    {
        var parts = id.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return new() { Verse = -1, PositionInVerse = -1 };
        }

        return new() { Verse = int.Parse(parts[0]), PositionInVerse = int.Parse(parts[1]) };
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddReadElberfelderServices(this IServiceCollection services)
    {
        services.AddScoped<IReadElberfelderRepository, ReadElberfelderRepository>();
        services.AddScoped<WordPopupService>();
        return services;
    }


}
