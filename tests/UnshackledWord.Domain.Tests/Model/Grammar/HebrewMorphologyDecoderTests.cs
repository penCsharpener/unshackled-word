using UnshackledWord.Domain.Models.Grammar;

namespace UnshackledWord.Domain.Tests.Model.Grammar;

public class HebrewMorphologyDecoderTests
{
    [Fact]
    public void Decode_PrepositionPlusNoun_RootIsTheNoun()
    {
        var result = HebrewMorphologyDecoder.Decode("HR/Ncfsa");

        result.Segments.Count.ShouldBe(2);
        result.SegmentCodes.ShouldBe(["R", "Ncfsa"]);
        result.Segments[0].PartOfSpeech.ShouldBe("preposition");
        result.RootIndex.ShouldBe(1);
        result.Root.PartOfSpeech.ShouldBe("noun");
        result.Root.Gender.ShouldBe("feminine");
        result.Root.Number.ShouldBe("singular");
        result.Root.State.ShouldBe("absolute");
        result.IsAramaic.ShouldBeFalse();
    }

    [Fact]
    public void Decode_PlainNoun()
    {
        var root = HebrewMorphologyDecoder.Decode("HNcmpa").Root;

        root.PartOfSpeech.ShouldBe("noun");
        root.Gender.ShouldBe("masculine");
        root.Number.ShouldBe("plural");
        root.State.ShouldBe("absolute");
    }

    [Fact]
    public void Decode_SequentialImperfectVerb()
    {
        var result = HebrewMorphologyDecoder.Decode("HC/Vqw3ms");

        result.Segments[0].PartOfSpeech.ShouldBe("conjunction");
        result.Root.PartOfSpeech.ShouldBe("verb");
        result.Root.Stem.ShouldBe("qal");
        result.Root.HebrewForm.ShouldBe("sequentialImperfect");
        result.Root.Person.ShouldBe("third");
        result.Root.Gender.ShouldBe("masculine");
        result.Root.Number.ShouldBe("singular");
    }

    [Fact]
    public void Decode_PerfectVerb()
    {
        var root = HebrewMorphologyDecoder.Decode("HVqp3ms").Root;

        root.HebrewForm.ShouldBe("perfect");
        root.Stem.ShouldBe("qal");
    }

    [Fact]
    public void Decode_HiphilImperfect()
    {
        var root = HebrewMorphologyDecoder.Decode("HVhi3ms").Root;

        root.Stem.ShouldBe("hiphil");
        root.HebrewForm.ShouldBe("imperfect");
    }

    [Fact]
    public void Decode_ObjectMarkerIsParticle()
    {
        HebrewMorphologyDecoder.Decode("HTo").Root.PartOfSpeech.ShouldBe("particle");
    }

    [Fact]
    public void Decode_SuffixIsNotTheRoot()
    {
        var result = HebrewMorphologyDecoder.Decode("HNcmsc/Sp3ms");

        result.RootIndex.ShouldBe(0);
        result.Root.PartOfSpeech.ShouldBe("noun");
        result.Root.State.ShouldBe("construct");
        result.Segments[1].PartOfSpeech.ShouldBe("suffix");
        result.Segments[1].Person.ShouldBe("third");
        result.Segments[1].Number.ShouldBe("singular");
    }

    [Fact]
    public void Decode_ArticleSegment()
    {
        var result = HebrewMorphologyDecoder.Decode("HTd/Ncmpa");

        result.Segments[0].PartOfSpeech.ShouldBe("article");
        result.Root.PartOfSpeech.ShouldBe("noun");
    }

    [Fact]
    public void Decode_AramaicNoun()
    {
        var result = HebrewMorphologyDecoder.Decode("ANcmsd");

        result.IsAramaic.ShouldBeTrue();
        result.Root.PartOfSpeech.ShouldBe("noun");
        result.Root.State.ShouldBe("determined");
    }

    [Fact]
    public void Decode_ProperNoun()
    {
        HebrewMorphologyDecoder.Decode("HNp").Root.PartOfSpeech.ShouldBe("nounProper");
    }

    [Theory]
    [InlineData("XYZ")]
    [InlineData("")]
    [InlineData(null)]
    public void Decode_UnknownCode_ReturnsEmpty(string? code)
    {
        var result = HebrewMorphologyDecoder.Decode(code);

        result.Root.IsEmpty.ShouldBeTrue();
        result.Segments.ShouldBeEmpty();
        result.RootIndex.ShouldBe(-1);
    }
}
