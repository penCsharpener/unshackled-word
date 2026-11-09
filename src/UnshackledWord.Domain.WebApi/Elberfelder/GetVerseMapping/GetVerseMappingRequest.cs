namespace UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping;

public sealed class GetVerseMappingRequest
{
    public int BibleBookId { get; set; }
    public int ChapterId { get; set; }
    public int VerseId { get; set; }
}
