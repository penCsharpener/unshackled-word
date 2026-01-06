namespace UnshackledWord.Domain.WebApi.BibleTagger.CreateElbSrMapping;

public record CreateElbSrResponse
{
    public int InsertedTags { get; set; }
    public List<int> UpdatedElbWordIds { get; set; } = default!;
}
