using UnshackledWord.Domain.Models.Dbo;

namespace UnshackledWord.Domain.WebApi.BibleTagger.GetVerse;

public sealed class GetVerseResponse
{
    public IEnumerable<Elb1871WordDbo> ElberfelderWords { get; set; } = default!;
    public IEnumerable<SrGntWordDbo> SrWords { get; set; } = default!;
}
