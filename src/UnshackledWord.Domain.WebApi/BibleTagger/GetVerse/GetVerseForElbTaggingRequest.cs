namespace UnshackledWord.Domain.WebApi.BibleTagger.GetVerse;

public sealed class GetVerseForElbTaggingRequest
{
    public int BibleBookId { get; set; }
    public int ChapterId { get; set; }
    public int VerseId { get; set; }
}
