using UnshackledWord.Domain.Models.BibleStructure.References;

namespace UnshackledWord.Domain.Tests.Model.BibleStructure.References;

public class ScriptureScannerGermanTests
{
    private static string[] Labels(string text) =>
        ScriptureScanner.Scan(text, ScriptureScanner.GermanTextOptions).Select(r => text.Substring(r.Start, r.Length)).ToArray();

    [Theory]
    [InlineData("Siehe Röm 8,28 hier", "Röm 8,28")]
    [InlineData("Siehe 1. Mose 1,1–2,3 hier", "1. Mose 1,1–2,3")]
    [InlineData("Siehe 1Mo 1,1 hier", "1Mo 1,1")]
    [InlineData("Siehe Römer 8,28 hier", "Römer 8,28")]
    [InlineData("Siehe Joh 3 hier", "Joh 3")]
    [InlineData("Siehe Am 3,7 hier", "Am 3,7")]
    [InlineData("Siehe Joh. 3,16 hier", "Joh. 3,16")]
    public void FindsGermanReference(string text, string expected) => Labels(text).ShouldBe([expected]);

    [Fact]
    public void ListWithSemicolonIsOneReference() => Labels("Röm 8,28; 12,1-2 gilt").ShouldBe(["Röm 8,28; 12,1-2"]);

    [Theory]
    [InlineData("am 3. Tag")]
    [InlineData("Am 3. Tag")]
    [InlineData("Am 3")]
    [InlineData("ob 3 Männer kamen")]
    [InlineData("Ob 3")]
    [InlineData("Mi 3 Uhr")]
    [InlineData("Mal 3")]
    [InlineData("Joh 3. Danach")]
    [InlineData("xRöm 8,28 und Röm")]
    public void ProseWordsAreNoReference(string text) => Labels(text).Where(l => !l.StartsWith("Röm")).ShouldBeEmpty();

    [Fact]
    public void LowerCaseBookNameDoesNotMatch() => Labels("röm 8,28").ShouldBeEmpty();

    [Fact]
    public void ChapterBeforeSentenceEndStillCounts() => Labels("Siehe Joh 3.").ShouldBe(["Joh 3"]);

    [Fact]
    public void OsisScanIsUnchanged() => ScriptureScanner.Scan("see Gen.4.2-10 here").Count.ShouldBe(1);

    [Theory]
    [InlineData("Am")]
    [InlineData("Ri")]
    [InlineData("Hi")]
    [InlineData("Ob")]
    [InlineData("Mi")]
    [InlineData("Mal")]
    [InlineData("Hab")]
    [InlineData("Hag")]
    [InlineData("Nah")]
    [InlineData("Ex")]
    [InlineData("Hl")]
    [InlineData("Rt")]
    [InlineData("Gen")]
    [InlineData("Dan")]
    [InlineData("Sach")]
    public void CommonWordAliasesRequireVerse(string alias)
    {
        BookAliasCatalog.RequiresVerseInProse(alias).ShouldBeTrue();
        BookAliasCatalog.RequiresVerseInProse(alias + ".").ShouldBeTrue();
    }

    [Fact]
    public void NormalAliasesDoNotRequireVerse() => BookAliasCatalog.RequiresVerseInProse("Joh").ShouldBeFalse();
}
