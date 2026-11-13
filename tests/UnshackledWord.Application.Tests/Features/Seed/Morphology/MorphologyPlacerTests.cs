using UnshackledWord.Application.Features.Seed.Anchoring;
using UnshackledWord.Application.Features.Seed.Morphology;
using UnshackledWord.Application.Tests.Features.Seed.Anchoring;

namespace UnshackledWord.Application.Tests.Features.Seed.Morphology;

public class MorphologyPlacerTests
{
    private static List<MorphologySeedLine> Lines(params string[] verses)
    {
        var tokens = AnchorTestData.Chapter1(verses);

        return tokens.Select(t => new MorphologySeedLine { Ref = t.Ref, Pos = t.Pos, Form = t.Text, Lemma = t.Text.ToLowerInvariant(), Upos = "NOUN" }).ToList();
    }

    private static Dictionary<int, string> Fingerprints(params string[] verses)
    {
        return new Dictionary<int, string> { [AnchorTestData.Chapter] = ChapterFingerprint.Compute(AnchorTestData.Chapter1(verses)) };
    }

    [Fact]
    public void Place_ShouldTakeLinesAsTheyAre_WhenFingerprintEqualsTheNewWords()
    {
        string[] verses = ["Im Anfang schuf Gott", "Und die Erde"];

        var placement = MorphologyPlacer.Place(Lines(verses), AnchorTestData.Chapter1(verses), Fingerprints(verses));

        placement.Exact.ShouldBe(7);
        placement.Orphaned.ShouldBe(0);
        placement.ChaptersAligned.ShouldBeEmpty();
        placement.Placed.Select(p => p.New!.Value.Pos).ShouldBe([1, 2, 3, 4, 1, 2, 3]);
    }

    [Fact]
    public void Place_ShouldNotListWordsWithoutMorphology_WhenSeedLinesAreOnlyTokenManifest()
    {
        string[] verses = ["Im Anfang schuf Gott"];
        var lines = Lines(verses);
        lines[1].Lemma = string.Empty;
        lines[1].Upos = string.Empty;

        var placement = MorphologyPlacer.Place(lines, AnchorTestData.Chapter1(verses), Fingerprints(verses));

        placement.Lines.Count.ShouldBe(3);
        placement.Exact.ShouldBe(3);
    }

    [Fact]
    public void Place_ShouldAlignTheChapter_WhenAWordWasInsertedAndLaterWordsShifted()
    {
        var lines = Lines("Im Anfang schuf Gott", "Und die Erde");
        string[] newVerses = ["Im Anfang schuf dann Gott", "Und die Erde"];

        var placement = MorphologyPlacer.Place(lines, AnchorTestData.Chapter1(newVerses), Fingerprints("Im Anfang schuf Gott", "Und die Erde"));

        placement.ChaptersAligned.ShouldBe([AnchorTestData.Chapter]);
        placement.Shifted.ShouldBe(1);
        placement.Exact.ShouldBe(6);
        var gott = placement.Lines.Single(l => l.Line.Form == "Gott");
        gott.New.ShouldBe(new AnchorToken(1001001, 5, "Gott"));
        gott.Class.ShouldBe(AnchorClass.Shifted);
        placement.Reportable.Select(l => l.Line.Form).ShouldBe(["Gott"]);
    }

    [Fact]
    public void Place_ShouldOrphanAndReport_WhenTheWordIsGoneFromTheNewText()
    {
        var lines = Lines("Im Anfang schuf dann Gott");

        var placement = MorphologyPlacer.Place(lines, AnchorTestData.Chapter1("Im Anfang schuf Gott"), Fingerprints("Im Anfang schuf dann Gott"));

        placement.Orphaned.ShouldBe(1);
        placement.Placed.Select(p => p.Line.Form).ShouldBe(["Im", "Anfang", "schuf", "Gott"]);
        placement.Reportable.Select(l => (l.Line.Form, l.Class)).ShouldBe([("dann", AnchorClass.Orphaned), ("Gott", AnchorClass.Shifted)]);
    }

    [Fact]
    public void Place_ShouldKeepTheLemmaOnTheFixedWord_WhenATypoWasCorrected()
    {
        var lines = Lines("Im Anfang schuff Gott die Himmel");

        var placement = MorphologyPlacer.Place(lines, AnchorTestData.Chapter1("Im Anfang schuf Gott die Himmel"), Fingerprints("Im Anfang schuff Gott die Himmel"));

        var schuf = placement.Lines.Single(l => l.Line.Form == "schuff");
        schuf.Class.ShouldBe(AnchorClass.Modified);
        schuf.New!.Value.Text.ShouldBe("schuf");
        placement.Modified.ShouldBe(1);
    }

    [Fact]
    public void Place_ShouldAlignAnyway_WhenFingerprintMatchesButAFormDiffers()
    {
        string[] verses = ["Im Anfang schuf Gott"];
        var lines = Lines(verses);
        lines[2].Form = "schuff"; // seed file was edited by hand and no longer matches its fingerprint

        var placement = MorphologyPlacer.Place(lines, AnchorTestData.Chapter1(verses), Fingerprints(verses));

        placement.ChaptersAligned.ShouldBe([AnchorTestData.Chapter]);
        placement.Modified.ShouldBe(1);
    }

    [Fact]
    public void Place_ShouldAlign_WhenTheChapterHasNoStoredFingerprint()
    {
        string[] verses = ["Im Anfang schuf Gott"];

        var placement = MorphologyPlacer.Place(Lines(verses), AnchorTestData.Chapter1(verses), new Dictionary<int, string>());

        placement.ChaptersAligned.ShouldBe([AnchorTestData.Chapter]);
        placement.Exact.ShouldBe(4);
    }

    [Fact]
    public void Place_ShouldOrphanEverything_WhenTheNewInstallationHasNoWordsForTheChapter()
    {
        var placement = MorphologyPlacer.Place(Lines("Im Anfang"), [], Fingerprints("Im Anfang"));

        placement.Orphaned.ShouldBe(2);
        placement.Placed.ShouldBeEmpty();
    }

    [Fact]
    public void Place_ShouldNeverPlaceTwoLinesOnTheSameWord_WhenWordsRepeat()
    {
        var lines = Lines("und Gott und die Erde und Licht");

        var placement = MorphologyPlacer.Place(lines, AnchorTestData.Chapter1("Dann und Gott und die Erde und Licht"), new Dictionary<int, string>());

        var targets = placement.Placed.Select(p => (p.New!.Value.Ref, p.New!.Value.Pos)).ToList();
        targets.Distinct().Count().ShouldBe(targets.Count);
        targets.Select(t => t.Pos).ShouldBe([2, 3, 4, 5, 6, 7, 8]);
    }
}
