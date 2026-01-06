namespace UnshackledWord.Domain.WebApi.BibleTagger.GetVerse;

public sealed class GetVerseRequest
{
    public int BibleBookId { get; set; }
    public int ChapterId { get; set; }
    public int VerseId { get; set; }
}
