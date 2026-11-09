using UnshackledWord.Application.Features.VerseMapping;
using UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping;

namespace UnshackledWord.Application.Tests.Features;

public class SaveVerseMappingValidatorTests
{
    private static string[] Validate(params VerseMappingLink[] links)
    {
        var result = new SaveVerseMappingValidator().Validate(new VerseMappingSaveInput
        {
            Links = links,
            GermanWordIds = new HashSet<int> { 1, 2, 3 },
            OriginalWordIds = new HashSet<int> { 10, 11 },
        });
        return result.Errors.Select(e => e.ErrorMessage).ToArray();
    }

    [Fact]
    public void ValidMapping_HasNoErrors()
    {
        Validate(
            new VerseMappingLink { ElbWordId = 1, StepWordId = 10 },
            new VerseMappingLink { ElbWordId = 1, StepWordId = 11 },
            new VerseMappingLink { ElbWordId = 2, IsAddedWord = true, ParentElbWordId = 1 }).ShouldBeEmpty();
    }

    [Fact]
    public void EmptyMapping_IsValid()
    {
        Validate().ShouldBeEmpty();
    }

    [Fact]
    public void ForeignGermanWord_IsRejected()
    {
        Validate(new VerseMappingLink { ElbWordId = 99, StepWordId = 10 }).ShouldContain(e => e.Contains("elbWordId"));
    }

    [Fact]
    public void ForeignOriginalWord_IsRejected()
    {
        Validate(new VerseMappingLink { ElbWordId = 1, StepWordId = 99 }).ShouldContain(e => e.Contains("stepWordId"));
    }

    [Fact]
    public void ForeignParent_IsRejected()
    {
        Validate(new VerseMappingLink { ElbWordId = 1, IsAddedWord = true, ParentElbWordId = 99 }).ShouldContain(e => e.Contains("parentElbWordId"));
    }

    [Fact]
    public void OwnParent_IsRejected()
    {
        Validate(new VerseMappingLink { ElbWordId = 1, IsAddedWord = true, ParentElbWordId = 1 }).ShouldContain(e => e.Contains("own parent"));
    }

    [Fact]
    public void AddedWordWithoutParent_IsRejected()
    {
        Validate(new VerseMappingLink { ElbWordId = 1, IsAddedWord = true }).ShouldContain(e => e.Contains("needs a parentElbWordId"));
    }

    [Fact]
    public void AddedWordWithOriginal_IsRejected()
    {
        Validate(new VerseMappingLink { ElbWordId = 1, IsAddedWord = true, ParentElbWordId = 2, StepWordId = 10 })
            .ShouldContain(e => e.Contains("must not have a stepWordId"));
    }

    [Fact]
    public void LinkWithoutOriginal_IsRejected()
    {
        Validate(new VerseMappingLink { ElbWordId = 1 }).ShouldContain(e => e.Contains("needs a stepWordId"));
    }

    [Fact]
    public void DuplicatePair_IsRejected()
    {
        Validate(
            new VerseMappingLink { ElbWordId = 1, StepWordId = 10 },
            new VerseMappingLink { ElbWordId = 1, StepWordId = 10 }).ShouldContain(e => e.Contains("duplicate"));
    }
}
