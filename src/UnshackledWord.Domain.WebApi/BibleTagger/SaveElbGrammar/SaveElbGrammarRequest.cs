using UnshackledWord.Domain.Models.Dbo;

namespace UnshackledWord.Domain.WebApi.BibleTagger.SaveElbGrammar;

public class SaveElbGrammarRequest
{
    public List<Elb1871WordGrammarDto> ElbWord { get; set; } = default!;
}
