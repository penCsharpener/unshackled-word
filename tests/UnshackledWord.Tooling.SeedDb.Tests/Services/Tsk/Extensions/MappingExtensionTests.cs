using UnshackledWord.Tooling.SeedDb.Services.Tsk.Extensions;

namespace UnshackledWord.Tooling.SeedDb.Tests.Services.Tsk.Extensions;

public class MappingExtensionTests
{
    [Theory]
    [InlineData("ps 104:14-17")]
    [InlineData("ge 2:19")]
    [InlineData("job 26:7,8,13")]
    public void ParseTskRef(string tskCrossReference)
    {
        var sut = new TskCrossReference(tskCrossReference);

        var result = sut.ToBibleReference();

        result.Count().ShouldBeGreaterThan(0);
    }
}
