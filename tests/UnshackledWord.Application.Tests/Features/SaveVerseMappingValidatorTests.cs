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

    // Word 1 "Gerstenernte." is the compound word of the part tests; 2 and 3 are ordinary words; 10, 11, 12 are original words.
    private static string[] ValidateWithTexts(params VerseMappingLink[] links)
    {
        var result = new SaveVerseMappingValidator().Validate(new VerseMappingSaveInput
        {
            Links = links,
            GermanWordIds = new HashSet<int> { 1, 2, 3 },
            OriginalWordIds = new HashSet<int> { 10, 11, 12 },
            GermanWordTexts = new Dictionary<int, string> { [1] = "Gerstenernte.", [2] = "Im", [3] = "Anfang" },
        });
        return result.Errors.Select(e => e.ErrorMessage).ToArray();
    }

    private static VerseMappingLink Part(int word, int step, int? order, string? part) =>
        new() { ElbWordId = word, StepWordId = step, PartOrder = order, GermanWordPart = part };

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

    [Fact]
    public void CompoundWordWithTwoParts_IsValid()
    {
        ValidateWithTexts(Part(1, 10, 1, "Gersten"), Part(1, 11, 2, "ernte")).ShouldBeEmpty();
    }

    [Fact]
    public void Parts_IgnoreCaseHyphensAndPunctuation()
    {
        ValidateWithTexts(Part(1, 10, 1, "GERSTEN-"), Part(1, 11, 2, "Ernte")).ShouldBeEmpty();
    }

    [Fact]
    public void SeveralLinksOfTheSamePart_AreValid()
    {
        ValidateWithTexts(Part(1, 10, 1, "Gersten"), Part(1, 11, 2, "ernte"), Part(1, 12, 2, "ernte")).ShouldBeEmpty();
    }

    [Fact]
    public void PartWithoutOrder_IsRejected()
    {
        ValidateWithTexts(Part(1, 10, null, "Gersten"), Part(1, 11, 2, "ernte"))
            .ShouldContain(e => e.Contains("both be set or both be empty"));
    }

    [Fact]
    public void OrderWithoutPart_IsRejected()
    {
        ValidateWithTexts(Part(1, 10, 1, null), Part(1, 11, 2, "ernte"))
            .ShouldContain(e => e.Contains("both be set or both be empty"));
    }

    [Fact]
    public void OrderBelowOne_IsRejected()
    {
        ValidateWithTexts(Part(1, 10, 0, "Gersten"), Part(1, 11, 1, "ernte")).ShouldContain(e => e.Contains("1 or higher"));
    }

    [Fact]
    public void PartOrdersWithAGap_AreRejected()
    {
        ValidateWithTexts(Part(1, 10, 1, "Gersten"), Part(1, 11, 3, "ernte")).ShouldContain(e => e.Contains("1..n without gaps"));
    }

    [Fact]
    public void PartOrdersNotStartingAtOne_AreRejected()
    {
        ValidateWithTexts(Part(1, 10, 2, "Gersten"), Part(1, 11, 3, "ernte")).ShouldContain(e => e.Contains("1..n without gaps"));
    }

    [Fact]
    public void PartsThatDoNotSpellTheWord_AreRejected()
    {
        ValidateWithTexts(Part(1, 10, 1, "Gersten"), Part(1, 11, 2, "ernten")).ShouldContain(e => e.Contains("do not spell the word"));
    }

    [Fact]
    public void PartsInTheWrongOrder_AreRejected()
    {
        ValidateWithTexts(Part(1, 10, 1, "ernte"), Part(1, 11, 2, "Gersten")).ShouldContain(e => e.Contains("do not spell the word"));
    }

    [Fact]
    public void WordWithOnlyOnePart_IsRejected()
    {
        ValidateWithTexts(Part(1, 10, 1, "Gerstenernte")).ShouldContain(e => e.Contains("at least two parts"));
    }

    [Fact]
    public void SameOrderWithDifferentTexts_IsRejected()
    {
        ValidateWithTexts(Part(1, 10, 1, "Gersten"), Part(1, 11, 1, "Gerste"), Part(1, 12, 2, "ernte"))
            .ShouldContain(e => e.Contains("different texts"));
    }

    [Fact]
    public void WordWithPartsAndAPlainLink_IsRejected()
    {
        ValidateWithTexts(Part(1, 10, 1, "Gersten"), Part(1, 11, 2, "ernte"), new VerseMappingLink { ElbWordId = 1, StepWordId = 12 })
            .ShouldContain(e => e.Contains("no whole-word link"));
    }

    [Fact]
    public void AddedWordWithParts_IsRejected()
    {
        ValidateWithTexts(new VerseMappingLink { ElbWordId = 1, IsAddedWord = true, ParentElbWordId = 2, PartOrder = 1, GermanWordPart = "Gersten" })
            .ShouldContain(e => e.Contains("added word has no parts"));
    }

    [Fact]
    public void PartLinkedToAForeignOriginalWord_IsRejected()
    {
        ValidateWithTexts(Part(1, 10, 1, "Gersten"), Part(1, 99, 2, "ernte")).ShouldContain(e => e.Contains("stepWordId 99"));
    }

    [Fact]
    public void TwoPartsOnTheSameOriginalWord_AreRejected_AsDuplicatePair()
    {
        ValidateWithTexts(Part(1, 10, 1, "Gersten"), Part(1, 10, 2, "ernte")).ShouldContain(e => e.Contains("duplicate"));
    }

    [Fact]
    public void PartLongerThanTheColumn_IsRejected()
    {
        ValidateWithTexts(Part(1, 10, 1, new string('a', 31)), Part(1, 11, 2, "ernte")).ShouldContain(e => e.Contains("longer than 30"));
    }

    [Fact]
    public void Parts_AreRejected_WhereTheWordTextsAreUnknown()
    {
        Validate(Part(1, 10, 1, "Gersten"), Part(1, 11, 2, "ernte")).ShouldContain(e => e.Contains("not supported here"));
    }

    [Fact]
    public void PlainLinksNeedNoWordTexts()
    {
        ValidateWithTexts(new VerseMappingLink { ElbWordId = 2, StepWordId = 10 }).ShouldBeEmpty();
    }
}
