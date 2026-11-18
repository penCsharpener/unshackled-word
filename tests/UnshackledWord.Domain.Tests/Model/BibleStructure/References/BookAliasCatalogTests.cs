using UnshackledWord.Domain.Models.BibleStructure.References;

namespace UnshackledWord.Domain.Tests.Model.BibleStructure.References;

public class BookAliasCatalogTests
{
    [Theory]
    [InlineData("Offb", 66)]
    [InlineData("offb", 66)]
    [InlineData("OFFB", 66)]
    [InlineData("Offb.", 66)]
    [InlineData("Offenbarung", 66)]
    [InlineData("1Mo", 1)]
    [InlineData("1. Mose", 1)]
    [InlineData("1.Mose", 1)]
    [InlineData("1 Mose", 1)]
    [InlineData("Gen", 1)]
    [InlineData("I Mose", 1)]
    [InlineData("I. Mose", 1)]
    [InlineData("1Kor", 46)]
    [InlineData("1. Kor", 46)]
    [InlineData("1 Kor.", 46)]
    [InlineData("I Kor", 46)]
    [InlineData("1. Korinther", 46)]
    [InlineData("2Kön", 12)]
    [InlineData("2Koen", 12)]
    [InlineData("2 Kön", 12)]
    [InlineData("2Kö", 12)]
    [InlineData("II Kön", 12)]
    [InlineData("2. Könige", 12)]
    [InlineData("Röm", 45)]
    [InlineData("Roem", 45)]
    [InlineData("Römer", 45)]
    [InlineData("Roemer", 45)]
    [InlineData("RÖM", 45)]
    [InlineData("Hl", 22)]
    [InlineData("Hld", 22)]
    [InlineData("Hohelied", 22)]
    [InlineData("Hoheslied", 22)]
    [InlineData("Hohes Lied", 22)]
    [InlineData("Pred", 21)]
    [InlineData("Koh", 21)]
    [InlineData("Prediger", 21)]
    [InlineData("Jud", 65)]
    [InlineData("Judas", 65)]
    [InlineData("Ri", 7)]
    [InlineData("Richter", 7)]
    [InlineData("Hes", 26)]
    [InlineData("Ez", 26)]
    [InlineData("Hesekiel", 26)]
    [InlineData("Ijob", 18)]
    [InlineData("Hi", 18)]
    [InlineData("Hiob", 18)]
    [InlineData("Jak", 59)]
    [InlineData("Phil", 50)]
    [InlineData("Klagelieder Jeremias", 25)]
    [InlineData("III Joh", 64)]
    public void Resolve_German(string token, int bookId)
    {
        var match = BookAliasCatalog.Resolve(token, AliasProfile.German);

        match.ShouldNotBeNull();
        match.Value.BookId.ShouldBe(bookId);
        match.Value.Kind.ShouldBe(BookMatchKind.Exact);
    }

    [Theory]
    [InlineData("Rev")]
    [InlineData("John")]
    [InlineData("Isa")]
    [InlineData("Off")]
    [InlineData("Xyz")]
    [InlineData("")]
    public void Resolve_German_Unknown(string token)
    {
        BookAliasCatalog.Resolve(token, AliasProfile.German).ShouldBeNull();
    }

    [Theory]
    [InlineData("John", 43)]
    [InlineData("Rev", 66)]
    [InlineData("1 Cor", 46)]
    [InlineData("Joh", 43)]
    [InlineData("Jud", 65)]
    [InlineData("Ez", 26)]
    public void Resolve_Legacy(string token, int bookId)
    {
        BookAliasCatalog.Resolve(token, AliasProfile.Legacy)!.Value.BookId.ShouldBe(bookId);
    }

    [Theory]
    [InlineData("Mat", 40)]
    [InlineData("Mrk", 41)]
    [InlineData("Jhn", 43)]
    [InlineData("Php", 50)]
    [InlineData("Jas", 59)]
    [InlineData("1Jn", 62)]
    [InlineData("Ezk", 26)]
    [InlineData("Jol", 29)]
    [InlineData("Nam", 34)]
    [InlineData("Sng", 22)]
    [InlineData("Jud", 65)]
    [InlineData("Jude", 65)]
    [InlineData("Luke", 42)]
    [InlineData("1Jo", 62)]
    [InlineData("Jdg", 7)]
    [InlineData("Ecc", 21)]
    [InlineData("Phil", 50)]
    public void Resolve_Osis(string token, int bookId)
    {
        BookAliasCatalog.Resolve(token, AliasProfile.Osis)!.Value.BookId.ShouldBe(bookId);
    }

    [Theory]
    [InlineData("Sir")]
    [InlineData("2Ma")]
    [InlineData("Wis")]
    [InlineData("3Ki")]
    [InlineData("Bel")]
    public void Resolve_Osis_NonCanonical(string token)
    {
        var match = BookAliasCatalog.Resolve(token, AliasProfile.Osis);

        match.ShouldNotBeNull();
        match.Value.BookId.ShouldBeNull();
        match.Value.Kind.ShouldBe(BookMatchKind.NonCanonical);
        match.Value.NonCanonical.ShouldBe(token);
    }

    [Theory]
    [InlineData("Weish")]
    [InlineData("1Makk")]
    public void Resolve_German_NonCanonical(string token)
    {
        BookAliasCatalog.Resolve(token, AliasProfile.German)!.Value.Kind.ShouldBe(BookMatchKind.NonCanonical);
    }

    [Theory]
    [InlineData("Offen", 66)]
    [InlineData("Apost", 44)]
    [InlineData("Matth", 40)]
    public void Resolve_Prefix(string token, int bookId)
    {
        var match = BookAliasCatalog.Resolve(token, AliasProfile.German, allowPrefix: true);

        match.ShouldNotBeNull();
        match.Value.BookId.ShouldBe(bookId);
        match.Value.Kind.ShouldBe(BookMatchKind.Prefix);
    }

    [Fact]
    public void Resolve_PrefixOff_DoesNotMatch()
    {
        BookAliasCatalog.Resolve("Offen", AliasProfile.German).ShouldBeNull();
    }

    [Fact]
    public void Resolve_Prefix_AmbiguousOrTooShort_ReturnsNull()
    {
        BookAliasCatalog.Resolve("Phili", AliasProfile.German, allowPrefix: true).ShouldBeNull();
        BookAliasCatalog.Resolve("Jo", AliasProfile.German, allowPrefix: true).ShouldBeNull();
    }

    [Fact]
    public void Candidates_Phili_ReturnsPhilipperAndPhilemon()
    {
        BookAliasCatalog.Candidates("Phili", AliasProfile.German).ShouldBe([50, 57]);
    }

    [Fact]
    public void Candidates_ExactToken_ReturnsOnlyThatBook()
    {
        BookAliasCatalog.Candidates("Phil", AliasProfile.German).ShouldBe([50]);
    }

    [Fact]
    public void Candidates_Empty_ReturnsNone()
    {
        BookAliasCatalog.Candidates("", AliasProfile.German).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("1. Kor.", "1kor")]
    [InlineData("I Kor", "1kor")]
    [InlineData("II Sam", "2sam")]
    [InlineData("Offenbarung", "offenbarung")]
    [InlineData("Hohes Lied", "hoheslied")]
    [InlineData("Ijob", "ijob")]
    [InlineData("Röm", "roem")]
    [InlineData("1Kö", "1koe")]
    public void Normalize_Examples(string input, string expected)
    {
        BookAliasCatalog.Normalize(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData(AliasProfile.German)]
    [InlineData(AliasProfile.Legacy)]
    [InlineData(AliasProfile.Osis)]
    public void Profile_EveryAliasMapsToOneBook(AliasProfile profile)
    {
        // the dictionary guarantees one book per key; verify normalised keys are stable and ids valid
        foreach (var (key, bookId) in BookAliasCatalog.Aliases(profile))
        {
            BookAliasCatalog.Normalize(key).ShouldBe(key);
            bookId.ShouldBeInRange(1, 66);
        }
    }

    [Theory]
    [InlineData(AliasProfile.German)]
    [InlineData(AliasProfile.Osis)]
    public void Profile_EveryBookHasAnAlias(AliasProfile profile)
    {
        var covered = BookAliasCatalog.Aliases(profile).Values.ToHashSet();

        for (var id = 1; id <= 66; id++)
        {
            covered.ShouldContain(id);
        }
    }

    [Fact]
    public void BuildIndex_DuplicateAliasForTwoBooks_Throws()
    {
        Should.Throw<InvalidOperationException>(() => BookAliasCatalog.BuildIndex([("Jud", 7), ("jud.", 65)]));
    }

    [Fact]
    public void BuildIndex_SameAliasSameBook_IsFine()
    {
        BookAliasCatalog.BuildIndex([("Jud", 65), ("jud.", 65)]).Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("Jud", AliasProfile.German, 65)]
    [InlineData("Jud", AliasProfile.Osis, 65)]
    [InlineData("Ri", AliasProfile.German, 7)]
    [InlineData("Hes", AliasProfile.German, 26)]
    [InlineData("Ez", AliasProfile.German, 26)]
    [InlineData("Ezk", AliasProfile.Osis, 26)]
    [InlineData("Pred", AliasProfile.German, 21)]
    [InlineData("Koh", AliasProfile.German, 21)]
    [InlineData("Ecc", AliasProfile.Osis, 21)]
    [InlineData("Hl", AliasProfile.German, 22)]
    [InlineData("Sng", AliasProfile.Osis, 22)]
    [InlineData("Jak", AliasProfile.German, 59)]
    [InlineData("Jas", AliasProfile.Osis, 59)]
    [InlineData("Phil", AliasProfile.German, 50)]
    [InlineData("Phil", AliasProfile.Osis, 50)]
    public void Collisions_ArePinned(string token, AliasProfile profile, int bookId)
    {
        BookAliasCatalog.Resolve(token, profile)!.Value.BookId.ShouldBe(bookId);
    }

    [Fact]
    public void Collisions_JudgesIsNotJud()
    {
        BookAliasCatalog.Resolve("Jdg", AliasProfile.Osis)!.Value.BookId.ShouldBe(7);
        BookAliasCatalog.Resolve("Ri", AliasProfile.Osis).ShouldBeNull();
    }
}
