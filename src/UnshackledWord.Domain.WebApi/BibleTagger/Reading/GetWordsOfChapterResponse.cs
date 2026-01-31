namespace UnshackledWord.Domain.WebApi.BibleTagger.Reading;

public record GetWordsOfChapterResponse
{
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public Dictionary<string, WordResponse> Words { get; set; }
}