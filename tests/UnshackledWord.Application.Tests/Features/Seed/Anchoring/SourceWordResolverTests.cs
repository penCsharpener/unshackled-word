using UnshackledWord.Application.Features.Seed.Anchoring;

namespace UnshackledWord.Application.Tests.Features.Seed.Anchoring;

public class SourceWordResolverTests
{
    private const int Verse = 1001001;

    private static SourceKey Key(int position, string word, int verse = Verse) => SourceKey.From(verse, position, word);

    private static AnchorToken Step(int position, string word, int verse = Verse) => Key(position, word, verse).ToToken();

    [Fact]
    public void Resolve_ShouldReturnExact_WhenPositionAndHashAreUnchanged()
    {
        var resolver = SourceWordResolver.Create([Key(1, "alpha"), Key(2, "beta")], [Step(1, "alpha"), Step(2, "beta")]);

        resolver.Resolve(Key(2, "beta")).Class.ShouldBe(AnchorClass.Exact);
    }

    [Fact]
    public void Resolve_ShouldReturnShifted_WhenTheOnlyWordWithThatHashMoved()
    {
        var resolver = SourceWordResolver.Create([Key(1, "alpha"), Key(2, "beta")], [Step(1, "alpha"), Step(2, "neu"), Step(3, "beta")]);

        var match = resolver.Resolve(Key(2, "beta"));

        match.Class.ShouldBe(AnchorClass.Shifted);
        match.New!.Value.Pos.ShouldBe(3);
    }

    [Fact]
    public void Resolve_ShouldKeepTheOrder_WhenTheSameHashOccursTwice()
    {
        // The seed knows the second "und" (position 3) only; STEP inserted a word before the first one.
        var resolver = SourceWordResolver.Create([Key(3, "und")], [Step(1, "neu"), Step(2, "und"), Step(3, "x"), Step(4, "und")]);

        var match = resolver.Resolve(Key(3, "und"));

        match.Class.ShouldBe(AnchorClass.Shifted);
        match.New!.Value.Pos.ShouldBeOneOf(2, 4);
    }

    [Fact]
    public void Resolve_ShouldTellTwoWordsOnOnePositionApartByTheirHash()
    {
        var resolver = SourceWordResolver.Create([Key(1, "alpha"), Key(1, "beta")], [Step(1, "alpha"), Step(1, "beta")]);

        resolver.Resolve(Key(1, "alpha")).New!.Value.Text.ShouldBe(SourceKey.Hash("alpha"));
        resolver.Resolve(Key(1, "beta")).New!.Value.Text.ShouldBe(SourceKey.Hash("beta"));
        resolver.Resolve(Key(1, "alpha")).Class.ShouldBe(AnchorClass.Exact);
    }

    [Fact]
    public void Resolve_ShouldReturnModified_WhenOneWordChangedBetweenEqualNeighbours()
    {
        var resolver = SourceWordResolver.Create([Key(1, "alpha"), Key(2, "beta"), Key(3, "gamma")], [Step(1, "alpha"), Step(2, "betta"), Step(3, "gamma")]);

        resolver.Resolve(Key(2, "beta")).Class.ShouldBe(AnchorClass.Modified);
    }

    [Fact]
    public void Resolve_ShouldReturnOrphaned_WhenTheWordIsGoneOrTheVerseIsMissing()
    {
        var resolver = SourceWordResolver.Create([Key(1, "alpha"), Key(2, "beta", 1001002)], [Step(1, "alpha")]);

        resolver.Resolve(Key(2, "beta", 1001002)).Class.ShouldBe(AnchorClass.Orphaned);
        resolver.Resolve(Key(7, "unbekannt")).Class.ShouldBe(AnchorClass.Orphaned);
    }
}
