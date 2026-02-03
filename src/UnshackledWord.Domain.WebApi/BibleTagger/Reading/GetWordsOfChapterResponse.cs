namespace UnshackledWord.Domain.WebApi.BibleTagger.Reading;

public record GetWordsOfChapterResponse
{
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public List<WordResponse> Words { get; set; }
}
