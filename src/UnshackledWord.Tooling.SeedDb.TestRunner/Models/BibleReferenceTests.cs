using Shouldly;
using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Tooling.SeedDb.TestRunner.Models;

public class BibleReferenceTests
{
    [Fact]
    public void BibleReferenceRange_Repeats_Book()
    {
        var range = new BibleReferenceRange(new BibleReference(1, 1, 1), new(2, 2, 2));
        range.ToString().ShouldBe("Gen 1:1 - Exod 2:2");
    }

    [Fact]
    public void BibleReferenceRange_Only_Repeats_Book_Once()
    {
        var range = new BibleReferenceRange(new BibleReference(1, 1, 1), new(1, 2, 1));
        range.ToString().ShouldBe("Gen 1:1-2:1");
    }

    [Fact]
    public void BibleReferenceRange_Only_Repeats_Chapter_Once()
    {
        var range = new BibleReferenceRange(new BibleReference(1, 1, 1), new(1, 1, 2));
        range.ToString().ShouldBe("Gen 1:1-2");
    }
}
