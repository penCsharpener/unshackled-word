using UnshackledWord.Domain.WebApi.Elberfelder.GetSimilarVerses;
using ChapterValidator = UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.GetSimilarVerses.GetSimilarVersesRequestValidator;
using VerseValidator = UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.GetSimilarVersesOfVerse.GetSimilarVersesOfVerseRequestValidator;

namespace UnshackledWord.Tooling.WebApi.Tests;

/// <summary>Roadmap 0027.3: request checks of the similar-verse endpoints (same book/chapter rules as the chapter cross references).</summary>
public class SimilarVersesValidatorTests
{
    [Theory]
    [InlineData(58, 4, null, true)]
    [InlineData(58, 4, 1, true)]
    [InlineData(58, 4, 30, true)]
    [InlineData(58, 4, 0, false)]
    [InlineData(58, 4, 31, false)]
    [InlineData(0, 1, null, false)]
    [InlineData(67, 1, null, false)]
    [InlineData(58, 14, null, false)]
    [InlineData(19, 119, null, true)]
    public void Chapter_Validates(int book, int chapter, int? limit, bool valid) =>
        new ChapterValidator().Validate(new GetSimilarVersesRequest { BibleBookId = book, Chapter = chapter, Limit = limit }).IsValid.ShouldBe(valid);

    [Theory]
    [InlineData(58004012, true)]
    [InlineData(19119176, true)]
    [InlineData(58004000, false)]
    [InlineData(58014001, false)]
    [InlineData(67001001, false)]
    [InlineData(1000001, false)]
    [InlineData(0, false)]
    public void Verse_Validates(int hebRefId, bool valid) =>
        new VerseValidator().Validate(new GetSimilarVersesOfVerseRequest { HebRefId = hebRefId }).IsValid.ShouldBe(valid);
}
