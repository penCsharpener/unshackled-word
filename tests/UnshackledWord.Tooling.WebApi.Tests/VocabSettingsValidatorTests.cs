using UnshackledWord.Domain.WebApi.Vocab;
using SettingsValidator = UnshackledWord.Tooling.WebApi.Endpoints.Vocab.UpdateSettings.Validator;

namespace UnshackledWord.Tooling.WebApi.Tests;

public class VocabSettingsValidatorTests
{
    private static bool IsValid(params string[] disabled) =>
        new SettingsValidator().Validate(new UpdateVocabSettingsRequest { DisabledQuizTypes = disabled.ToList() }).IsValid;

    [Theory]
    [InlineData]
    [InlineData("reverse_multiple_choice")]
    [InlineData("multiple_choice")]
    [InlineData("reverse_multiple_choice", "listening", "verse_context")]
    public void Accepts_known_toggleable_types_when_one_multiple_choice_type_stays_on(params string[] disabled) =>
        IsValid(disabled).ShouldBeTrue();

    [Theory]
    [InlineData("introduction")]
    [InlineData("typing")]
    [InlineData("nonsense")]
    public void Rejects_types_that_cannot_be_turned_off(string type) => IsValid(type).ShouldBeFalse();

    [Fact]
    public void Rejects_turning_off_both_multiple_choice_types() => IsValid("multiple_choice", "reverse_multiple_choice").ShouldBeFalse();
}
