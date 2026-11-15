using UnshackledWord.Application.Features.Seed.Revision;

namespace UnshackledWord.Application.Tests.Features.Seed.Revision;

public class RevisionVariantHashTests
{
    private static RevisionVerseSeedLine Verse(string text = "Denn also hat Gott", bool reviewed = false) =>
        new() { Ref = 43003016, Level = 200, BaseHash = "abcdef123456", Text = text, Reviewed = reviewed };

    private static RevisionMappingSeedLine Map(int pos, string origin = "carried") =>
        new() { Ref = 43003016, Level = 200, Pos = pos, Form = "hat", SrcRef = 43003016, SrcPos = pos, SrcHash = "aaaa1111", Origin = origin };

    [Fact]
    public void Compute_ShouldNotDependOnTheOrderOfTheMappingLines()
    {
        RevisionVariantHash.Compute(Verse(), [Map(1), Map(2)]).ShouldBe(RevisionVariantHash.Compute(Verse(), [Map(2), Map(1)]));
    }

    [Fact]
    public void Compute_ShouldChange_WhenTextReviewFlagOrAnyMappingLineChanges()
    {
        var baseline = RevisionVariantHash.Compute(Verse(), [Map(1)]);

        RevisionVariantHash.Compute(Verse("Denn also liebte Gott"), [Map(1)]).ShouldNotBe(baseline);
        RevisionVariantHash.Compute(Verse(reviewed: true), [Map(1)]).ShouldNotBe(baseline);
        RevisionVariantHash.Compute(Verse(), [Map(1, "manual")]).ShouldNotBe(baseline);
        RevisionVariantHash.Compute(Verse(), [Map(1), Map(2)]).ShouldNotBe(baseline);
    }
}
