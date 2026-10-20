using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Domain.Tests.Model.BibleStructure;

public class BibleReferenceParserTests
{
    [Theory]
    [InlineData("Joh 3,16", 43003016, 43003016)]
    [InlineData("Joh 3,16-18", 43003016, 43003018)]
    [InlineData("John 3:16", 43003016, 43003016)]
    [InlineData("joh 3, 16 - 18", 43003016, 43003018)]
    [InlineData("Röm 8", 45008000, 45008999)]
    [InlineData("Roem 8-9", 45008000, 45009999)]
    [InlineData("1Mo 1,1-2,3", 1001001, 1002003)]
    [InlineData("1. Mose 1,1", 1001001, 1001001)]
    [InlineData("1 Kor. 13,4", 46013004, 46013004)]
    [InlineData("Jud 3", 65003000, 65003999)]
    [InlineData("Offb 22,21", 66022021, 66022021)]
    public void Parse_ValidReference(string input, int start, int end)
    {
        var result = BibleReferenceParser.Parse(input);

        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Range!.StartRefId.ShouldBe(start);
        result.Range.EndRefId.ShouldBe(end);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Joh")]
    [InlineData("Xyz 3,16")]
    [InlineData("Joh 3,0")]
    [InlineData("Joh 3,18-16")]
    [InlineData("Joh 4-3")]
    public void Parse_InvalidReference_ReturnsError(string input)
    {
        var result = BibleReferenceParser.Parse(input);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNullOrWhiteSpace();
    }
}
