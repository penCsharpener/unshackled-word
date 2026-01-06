using UnshackledWord.Domain.Models.Dto;

namespace UnshackledWord.Domain.WebApi.BibleTagger.SaveElbGrammar;

public class SaveElbGrammarResponse
{
    public List<Elb1871GrammarUpdateResult> ModifiedElbWordIds { get; set; } = default!;
}
