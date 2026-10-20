using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Domain.Tests.Model.BibleStructure;

public class GermanReferenceFormatterTests
{
    [Theory]
    [InlineData(43003016, null, "Joh 3,16")]
    [InlineData(43003016, 43003016, "Joh 3,16")]
    [InlineData(62004009, 62004010, "1Joh 4,9-10")]
    [InlineData(45005008, 45006002, "Röm 5,8-6,2")]
    [InlineData(1050026, 2001001, "1Mo 50,26 - 2Mo 1,1")]
    public void Format_Range(int start, int? end, string expected)
    {
        GermanReferenceFormatter.Format(start, end).ShouldBe(expected);
    }
}
