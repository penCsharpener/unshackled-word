using UnshackledWord.Domain.Models.Dbo;

namespace UnshackledWord.Domain.WebApi.BibleTagger.GetVerse;

public sealed class GetVerseForElbTaggingResponse
{
    public List<Elb1871WordDbo> ElberfelderWords { get; set; } = default!;
    public List<SrGntWordDbo> SrWords { get; set; } = default!;
}
