using UnshackledWord.Domain.Models.Dbo;
using UnshackledWord.Domain.WebApi.BibleTagger.SaveElbGrammar;

namespace UnshackledWord.Domain.WebApi.BibleTagger.GetVerse;

public sealed class GetVerseForElbGrammarResponse
{
    public List<Elb1871WordGrammarDto> ElberfelderWords { get; set; } = default!;
}
