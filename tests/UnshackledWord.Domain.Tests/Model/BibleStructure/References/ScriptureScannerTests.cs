using UnshackledWord.Domain.Models.BibleStructure.References;

namespace UnshackledWord.Domain.Tests.Model.BibleStructure.References;

public class ScriptureScannerTests
{
    private static string[] Labels(string text) => ScriptureScanner.Scan(text).Select(r => text.Substring(r.Start, r.Length)).ToArray();

    [Fact]
    public void FindsUntaggedReferenceWithPositions()
    {
        const string text = "first mentioned at 1Ch.8.39; son of";
        var found = ScriptureScanner.Scan(text);
        found.Count.ShouldBe(1);
        found[0].BookId.ShouldBe(13);
        found[0].Spans.ShouldBe([new VerseSpan(8, 39, 8, 39)]);
        text.Substring(found[0].Start, found[0].Length).ShouldBe("1Ch.8.39");
    }

    [Fact]
    public void WordJobWithoutDigitStaysText() => Labels("the Job.1.1 and Job is").ShouldBe(["Job.1.1"]);

    [Fact]
    public void ConsumesListOfVerses() => Labels("see Gen.4.2-10; 5.1 here").ShouldBe(["Gen.4.2-10; 5.1"]);

    [Fact]
    public void SpaceBeforeCommaEndsReference() => Labels("see 2Co.6.7 , al.").ShouldBe(["2Co.6.7"]);

    [Fact]
    public void UnknownBookAndEmbeddedWordsAreIgnored() => Labels("Xyz.1.1 and aGen.1.1 and ch.5").ShouldBeEmpty();

    [Fact]
    public void NonCanonicalIsReturnedWithoutBookId()
    {
        var found = ScriptureScanner.Scan("see Sir.3.1 now");
        found.Count.ShouldBe(1);
        found[0].BookId.ShouldBeNull();
    }

    [Fact]
    public void TwoReferencesInOneText() => Labels("Gen.1.1 and Mat.5.29.").ShouldBe(["Gen.1.1", "Mat.5.29"]);

    [Fact]
    public void EmptyAndNull()
    {
        ScriptureScanner.Scan(null).ShouldBeEmpty();
        ScriptureScanner.Scan("").ShouldBeEmpty();
    }
}
