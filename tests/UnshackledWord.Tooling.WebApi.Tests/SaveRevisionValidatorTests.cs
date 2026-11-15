using UnshackledWord.Domain.WebApi.Revision;
using SaveValidator = UnshackledWord.Tooling.WebApi.Endpoints.Revision.SaveVerse.Validator;

namespace UnshackledWord.Tooling.WebApi.Tests;

public class SaveRevisionValidatorTests
{
    private static SaveRevisionVerseRequest Valid() => new() { BibleBookId = 43, ChapterId = 3, VerseId = 16, Level = 200, Text = "Denn so hat Gott" };

    [Fact]
    public void Validate_Passes_ForAValidRequest() => new SaveValidator().Validate(Valid()).IsValid.ShouldBeTrue();

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public void Validate_Fails_WhenLevelIsOutOfRange(int level)
    {
        var request = Valid();
        request.Level = level;

        new SaveValidator().Validate(request).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n ")]
    public void Validate_Fails_WhenTextIsBlank(string text)
    {
        var request = Valid();
        request.Text = text;

        new SaveValidator().Validate(request).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_Fails_WhenTextExceeds2000Characters()
    {
        var request = Valid();
        request.Text = new string('a', 2001);

        new SaveValidator().Validate(request).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_Fails_WhenTextHasMoreThan300Words()
    {
        var request = Valid();
        request.Text = string.Join(' ', Enumerable.Repeat("a", 301));

        new SaveValidator().Validate(request).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_Passes_At300Words()
    {
        var request = Valid();
        request.Text = string.Join(' ', Enumerable.Repeat("a", 300));

        new SaveValidator().Validate(request).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(67, 1, 1)]
    [InlineData(43, 0, 1)]
    [InlineData(43, 1, 1000)]
    public void Validate_Fails_WhenVerseReferenceIsOutOfRange(int book, int chapter, int verse)
    {
        var request = Valid();
        request.BibleBookId = book;
        request.ChapterId = chapter;
        request.VerseId = verse;

        new SaveValidator().Validate(request).IsValid.ShouldBeFalse();
    }
}
