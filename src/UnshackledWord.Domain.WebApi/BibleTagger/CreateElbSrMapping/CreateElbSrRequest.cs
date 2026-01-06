using UnshackledWord.Domain.Models.Dbo;

namespace UnshackledWord.Domain.WebApi.BibleTagger.CreateElbSrMapping;

public class CreateElbSrRequest
{
    public Elb1871WordDbo Elb1871Word { get; set; } = default!;
    public SrGntWordDbo SrGntWord { get; set; } = default!;
}
