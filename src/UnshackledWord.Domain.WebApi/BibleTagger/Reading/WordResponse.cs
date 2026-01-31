namespace UnshackledWord.Domain.WebApi.BibleTagger.Reading;

public record WordResponse
{
    public string WordInContext { get; set; } = default!;
    public string? PlainWord { get; set; }
    public string? Lemma { get; set; }
    public string? Strongs { get; set; }
    public string Id { get; set; } = default!;
}
