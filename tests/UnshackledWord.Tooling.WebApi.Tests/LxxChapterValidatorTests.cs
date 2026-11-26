using UnshackledWord.Domain.WebApi.Lxx;
using Validator = UnshackledWord.Tooling.WebApi.Endpoints.Lxx.GetLxxChapter.GetLxxChapterRequestValidator;

namespace UnshackledWord.Tooling.WebApi.Tests;

/// <summary>Roadmap 0028.4: request checks of the LXX chapter endpoint (Old Testament books only).</summary>
public class LxxChapterValidatorTests
{
    [Theory]
    [InlineData(24, 25, true)]
    [InlineData(19, 119, true)]
    [InlineData(39, 4, true)]
    [InlineData(40, 1, false)]
    [InlineData(66, 1, false)]
    [InlineData(0, 1, false)]
    [InlineData(24, 53, false)]
    [InlineData(24, 0, false)]
    public void Validates(int book, int chapter, bool valid) =>
        new Validator().Validate(new GetLxxChapterRequest { BibleBookId = book, Chapter = chapter }).IsValid.ShouldBe(valid);
}
