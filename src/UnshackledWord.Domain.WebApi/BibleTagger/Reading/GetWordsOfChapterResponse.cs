namespace UnshackledWord.Domain.WebApi.BibleTagger.Reading;

public record GetWordsOfChapterResponse
{
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public List<WordResponse> Words { get; set; }

    /// <summary>Verses showing a variant; null (omitted) when none, so level 100 is unchanged.</summary>
    public List<Elberfelder.RevisedVerseResponse>? RevisedVerses { get; set; }
}
