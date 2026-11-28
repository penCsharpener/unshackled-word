using Shouldly;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Seeding.Tsk.Extensions;
using UnshackledWord.Seeding.Tsk.Models;

namespace UnshackledWord.Tooling.SeedDb.TestRunner.Tsk;

public class MappingExtensionTests
{
    [Theory]
    [InlineData("pr 8:22-24;pr 16:4;mr 13:19,20,23;joh 1:1-3,5;heb 1:10-12,17;1jo 1:1,3-5,7,9-11", 13, 8, 5)]
    public void Method_Parses_References_Correctly(string input, int countTotal, int countRefs, int countRanges)
    {
        var tskCr = new TskCrossReferenceList(input);
        var bbRef = tskCr.ToBibleReferences();
        bbRef.Count.ShouldBe(countTotal);
        bbRef.Count(x => x is BibleReference).ShouldBe(countRefs);
        bbRef.Count(x => x is BibleReferenceRange).ShouldBe(countRanges);
    }
}
