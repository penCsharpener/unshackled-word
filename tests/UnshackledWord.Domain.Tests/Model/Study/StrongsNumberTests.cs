using UnshackledWord.Domain.Models.Dbo.Step;
using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Domain.Tests.Model.Study;

public class StrongsNumberTests
{
    [Theory]
    [InlineData("G26", StrongsLanguage.Greek, 26, null, "G0026")]
    [InlineData("g26", StrongsLanguage.Greek, 26, null, "G0026")]
    [InlineData("G0026", StrongsLanguage.Greek, 26, null, "G0026")]
    [InlineData("H1254A", StrongsLanguage.Hebrew, 1254, "A", "H1254A")]
    [InlineData("{H7225G}", StrongsLanguage.Hebrew, 7225, "G", "H7225G")]
    [InlineData("H9003/{H7225G}", StrongsLanguage.Hebrew, 7225, "G", "H7225G")]
    public void Parse_ValidInput(string input, StrongsLanguage language, int number, string? extra, string formatted)
    {
        var result = StrongsNumber.Parse(input);

        result.Value.ShouldBe(new StrongsNumber(language, number, extra));
        result.Value!.Value.ToString().ShouldBe(formatted);
    }

    [Theory]
    [InlineData("26", "Prefix G")]
    [InlineData("", "empty")]
    [InlineData("X12", "Could not parse")]
    public void Parse_InvalidInput_ReturnsError(string input, string message)
    {
        var result = StrongsNumber.Parse(input);

        result.Value.ShouldBeNull();
        result.Error.ShouldContain(message);
    }
}

public class StrongsNumberNormalizeTests
{
    [Theory]
    [InlineData("g26", "G0026", false)]
    [InlineData("H1254A_B", "H1254A", true)]
    [InlineData("H9003/{H7225G}", "H7225G", true)]
    [InlineData("H0853", "H0853", true)]
    public void TryNormalize_ValidInput(string input, string expected, bool hebrew)
    {
        StrongsNumber.TryNormalize(input, out var normalized, out var isHebrew).ShouldBeTrue();
        normalized.ShouldBe(expected);
        isHebrew.ShouldBe(hebrew);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("26")]
    [InlineData("G0")]
    [InlineData("_A")]
    public void TryNormalize_InvalidInput(string? input)
    {
        StrongsNumber.TryNormalize(input, out _, out _).ShouldBeFalse();
    }
}
