using UnshackledWord.Application.Features.Seed.Anchoring;

namespace UnshackledWord.Application.Features.Seed.Morphology;

/// <summary>
/// One line of <c>assets/seed/morphology/NN-Book.csv</c>: a German word (<see cref="Ref"/>, <see cref="Pos"/>, <see cref="Form"/>) with
/// its lemma and grammar. A word without a morphology row in the exporting database has an empty <see cref="Lemma"/>; such lines
/// only complete the token list the anchoring aligns against.
/// </summary>
public sealed class MorphologySeedLine
{
    /// <summary><c>HebRefId</c> (<c>BBCCCVVV</c> as a number).</summary>
    public int Ref { get; set; }

    /// <summary><c>PositionInVerse</c>.</summary>
    public int Pos { get; set; }

    /// <summary><c>PlainWord</c> of the word (public domain Elb1871 text).</summary>
    public string Form { get; set; } = string.Empty;

    public string Lemma { get; set; } = string.Empty;

    /// <summary>Universal Dependencies part of speech (<c>PartOfSpeech</c>).</summary>
    public string Upos { get; set; } = string.Empty;

    public string Stts { get; set; } = string.Empty;

    /// <summary>UD style features, e.g. <c>Case=Nom|Gender=Masc|Number=Sing</c>; see <see cref="MorphologyFeatures"/>.</summary>
    public string Feats { get; set; } = string.Empty;

    /// <summary>True when the line carries morphology data, i.e. the exporting database had a row for this word.</summary>
    public bool HasMorphology => Lemma.Length > 0;

    public AnchorToken ToToken() => AnchorToken.ForWord(Ref, Pos, Form);
}
