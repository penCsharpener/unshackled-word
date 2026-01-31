namespace UnshackledWord.Domain.WebApi.BibleTagger.Reading;

public record GetWordsOfChapterRequest
{
    public int BibleBookId { get; set; }
    public int ChapterId { get; set; }
}