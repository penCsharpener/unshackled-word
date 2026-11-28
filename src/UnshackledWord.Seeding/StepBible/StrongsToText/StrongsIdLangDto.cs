using UnshackledWord.Domain.Models.Dbo.Step;

namespace UnshackledWord.Seeding.StepBible.StrongsToText;

public sealed class StrongsIdLangDto
{
    public int Id { get; set; }
    public string Strongs { get; set; } = default!;
    public StrongsLanguage Language { get; set; }
}