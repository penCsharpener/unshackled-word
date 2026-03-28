using UnshackledWord.Domain.Models.Dbo;

namespace UnshackledWord.Domain.WebApi.BibleTagger.GetVerse;

public sealed class GetVerseForElbGrammarResponse
{
    public List<Elb1871WordGrammarDto> ElberfelderWords { get; set; } = default!;
}
