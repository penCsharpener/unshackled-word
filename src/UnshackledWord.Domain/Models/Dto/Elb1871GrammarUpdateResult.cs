namespace UnshackledWord.Domain.Models.Dto;

public sealed class Elb1871GrammarUpdateResult
{
    public List<int> UpdatedIds { get; set; } = [];
    public string UpdatedPlainWord { get; set; } = default!;
    public string UpdatedLemma { get; set; } = default!;
    public string? UpdatedPartOfSpeech { get; set; }
}
