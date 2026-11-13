using UnshackledWord.Application.Features.Seed.Anchoring;

namespace UnshackledWord.Application.Tests.Features.Seed.Anchoring;

public class GermanWordResolverTests
{
    [Fact]
    public void Resolve_ShouldReturnExact_WhenTheChapterIsUnchanged()
    {
        string[] verses = ["Im Anfang schuf Gott", "Und die Erde"];
        var resolver = GermanWordResolver.Create(AnchorTestData.Chapter1(verses), AnchorTestData.Chapter1(verses));

        var match = resolver.Resolve(1001002, 3, "Erde");

        match.Class.ShouldBe(AnchorClass.Exact);
        match.New!.Value.Pos.ShouldBe(3);
        resolver.ChaptersAligned.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_ShouldFollowTheWord_WhenAWordWasInsertedBeforeIt()
    {
        var resolver = GermanWordResolver.Create(AnchorTestData.Chapter1("Im Anfang schuf Gott"), AnchorTestData.Chapter1("Im allerersten Anfang schuf Gott"));

        var match = resolver.Resolve(1001001, 3, "schuf");

        match.Class.ShouldBe(AnchorClass.Shifted);
        match.New!.Value.Pos.ShouldBe(4);
        resolver.ChaptersAligned.ShouldBe([AnchorTestData.Chapter]);
    }

    [Fact]
    public void Resolve_ShouldReturnModified_WhenATypoWasFixedBetweenEqualWords()
    {
        var resolver = GermanWordResolver.Create(AnchorTestData.Chapter1("Im Anfang schuf Gott"), AnchorTestData.Chapter1("Im Anfang schuff Gott"));

        var match = resolver.Resolve(1001001, 3, "schuf");

        match.Class.ShouldBe(AnchorClass.Modified);
        match.New!.Value.Pos.ShouldBe(3);
    }

    [Fact]
    public void Resolve_ShouldReturnOrphaned_WhenTheWordIsGone()
    {
        var resolver = GermanWordResolver.Create(AnchorTestData.Chapter1("Im Anfang schuf Gott die Himmel"), AnchorTestData.Chapter1("Im Anfang schuf Gott"));

        resolver.Resolve(1001001, 6, "Himmel").Class.ShouldBe(AnchorClass.Orphaned);
    }

    [Fact]
    public void Resolve_ShouldAcceptTheExactPlace_WhenTheManifestIsMissing()
    {
        var resolver = GermanWordResolver.Create([], AnchorTestData.Chapter1("Im Anfang schuf Gott"));

        resolver.Resolve(1001001, 2, "Anfang").Class.ShouldBe(AnchorClass.Exact);
        resolver.Resolve(1001001, 2, "Ende").Class.ShouldBe(AnchorClass.Orphaned);
        resolver.Resolve(1001001, 9, "Anfang").Class.ShouldBe(AnchorClass.Orphaned);
    }

    [Fact]
    public void Resolve_ShouldNotTrustTheManifest_WhenTheKeyCarriesAnotherForm()
    {
        string[] verses = ["Im Anfang schuf Gott"];
        var resolver = GermanWordResolver.Create(AnchorTestData.Chapter1(verses), AnchorTestData.Chapter1("Im Anfang schuff Gott"));

        resolver.Resolve(1001001, 3, "etwas anderes").Class.ShouldBe(AnchorClass.Orphaned);
    }
}
