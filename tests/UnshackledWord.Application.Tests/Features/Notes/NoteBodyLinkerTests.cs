using UnshackledWord.Application.Features.Notes;

namespace UnshackledWord.Application.Tests.Features.Notes;

public class NoteBodyLinkerTests
{
    [Fact]
    public void DoneWhenExample() =>
        NoteBodyLinker.Link("Vgl. Röm 12,1-2 und am 3. Tag").ShouldBe("Vgl. [Röm 12,1-2](/bible/45/12?v=1-2#v1) und am 3. Tag");

    [Fact]
    public void SingleVerse() => NoteBodyLinker.Link("Röm 8,28").ShouldBe("[Röm 8,28](/bible/45/8?v=28#v28)");

    [Fact]
    public void RangeAcrossChapters() => NoteBodyLinker.Link("1. Mose 1,1–2,3").ShouldBe("[1. Mose 1,1–2,3](/bible/1/1?v=1-31#v1)");

    [Fact]
    public void GroupsGetOneLinkEach() =>
        NoteBodyLinker.Link("Röm 8,28; 12,1-2").ShouldBe("[Röm 8,28](/bible/45/8?v=28#v28); [12,1-2](/bible/45/12?v=1-2#v1)");

    [Fact]
    public void WholeChapter() => NoteBodyLinker.Link("Joh 3").ShouldBe("[Joh 3](/bible/43/3)");

    [Fact]
    public void OrdinalIsNoReference() => NoteBodyLinker.Link("am 3. Tag").ShouldBe("am 3. Tag");

    [Fact]
    public void CommonWordWithVerseIsLinked() => NoteBodyLinker.Link("Am 3,7").ShouldBe("[Am 3,7](/bible/30/3?v=7#v7)");

    [Theory]
    [InlineData("siehe `Röm 8,28` dort")]
    [InlineData("siehe [Röm 8,28](https://example.org/x) dort")]
    [InlineData("siehe [Joh 3](/bible/43/3) dort")]
    [InlineData("siehe <https://example.org/Röm 8,28> dort")]
    [InlineData("siehe https://example.org/Joh 3 dort")]
    [InlineData("```\nRöm 8,28\n```")]
    [InlineData("~~~text\nRöm 8,28\n~~~\n")]
    [InlineData("``code `Röm 8,28` code``")]
    public void ProtectedAreasStayUnchanged(string markdown) => NoteBodyLinker.Link(markdown).ShouldBe(markdown);

    [Fact]
    public void ProseAroundFenceIsLinked() =>
        NoteBodyLinker.Link("Joh 3\n\n```\nJoh 3\n```\n\nJoh 3").ShouldBe("[Joh 3](/bible/43/3)\n\n```\nJoh 3\n```\n\n[Joh 3](/bible/43/3)");

    [Fact]
    public void UnclosedBacktickIsText() => NoteBodyLinker.Link("a ` Joh 3").ShouldBe("a ` [Joh 3](/bible/43/3)");

    [Fact]
    public void BoldAndListSurvive() =>
        NoteBodyLinker.Link("**Joh 3**\n- Röm 8,28").ShouldBe("**[Joh 3](/bible/43/3)**\n- [Röm 8,28](/bible/45/8?v=28#v28)");

    [Fact]
    public void EmptyStaysEmpty() => NoteBodyLinker.Link("").ShouldBe("");
}
