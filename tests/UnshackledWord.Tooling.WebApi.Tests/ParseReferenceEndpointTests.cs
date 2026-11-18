using UnshackledWord.Application.Features.ScriptureReferences;
using UnshackledWord.Tooling.WebApi.Endpoints.MetaBible.ParseReference;
using UnshackledWord.Tooling.WebApi.RateLimiting;

namespace UnshackledWord.Tooling.WebApi.Tests;

public class ParseReferenceEndpointTests
{
    private static readonly ScriptureReferenceService Service = new();

    [Fact]
    public void Parse_VerseList_ReturnsGermanDisplayAndDeepLink()
    {
        var response = Service.Parse("Offb 2,1-5.7.9-10");

        response.Error.ShouldBeNull();
        var reference = response.References.ShouldHaveSingleItem();
        reference.BookId.ShouldBe(66);
        reference.Display.ShouldBe("Offb 2,1–5.7.9–10");
        reference.Path.ShouldBe("/bible/66/2?v=1-5,7,9-10#v1");
        reference.Spans.Count.ShouldBe(3);
        reference.Groups.ShouldHaveSingleItem().Path.ShouldBe(reference.Path);
    }

    [Fact]
    public void Parse_TwoChapters_ReturnsOneGroupPerChapterWithOwnPath()
    {
        var reference = Service.Parse("Röm 8,28; 12,1-2").References.ShouldHaveSingleItem();

        reference.Display.ShouldBe("Röm 8,28; 12,1–2");
        reference.Path.ShouldBe("/bible/45/8?v=28#v28");
        reference.Groups.Select(g => g.Path).ShouldBe(["/bible/45/8?v=28#v28", "/bible/45/12?v=1-2#v1"]);
        reference.Groups.Select(g => g.Display).ShouldBe(["Röm 8,28", "Röm 12,1–2"]);
    }

    [Fact]
    public void Parse_PlainWord_HasNoReferencesAndNoError()
    {
        var response = Service.Parse("Liebe");

        response.References.ShouldBeEmpty();
        response.Error.ShouldBeNull();
    }

    [Fact]
    public void Parse_EmptyQuery_HasNoReferencesAndNoError()
    {
        var response = Service.Parse("  ");

        response.References.ShouldBeEmpty();
        response.Error.ShouldBeNull();
    }

    [Fact]
    public void Parse_AmbiguousPrefix_ExposesBookCandidates()
    {
        var response = Service.Parse("Phili 1");

        response.References.ShouldBeEmpty();
        response.Error.ShouldNotBeNull().Code.ShouldBe("ambiguousBook");
        response.BookCandidates.ShouldBe([50, 57], ignoreOrder: true);
    }

    [Fact]
    public void Parse_ChapterOutOfRange_ReportsErrorWithArgs()
    {
        var response = Service.Parse("Offb 23");

        var error = response.Error.ShouldNotBeNull();
        error.Code.ShouldBe("chapterOutOfRange");
        error.Args.ShouldNotBeEmpty();
    }

    [Fact]
    public void Parse_BookOnly_HasNoPath()
    {
        var reference = Service.Parse("Hiob").References.ShouldHaveSingleItem();

        reference.BookOnly.ShouldBeTrue();
        reference.BookId.ShouldBe(18);
    }

    [Fact]
    public void Endpoint_UsesGeneralRateLimitTier()
    {
        // No [RateLimitGroup]: the request counts toward the general anonymous budget like other public reads.
        typeof(Endpoint).GetCustomAttributes(typeof(RateLimitGroupAttribute), false).ShouldBeEmpty();
    }
}
