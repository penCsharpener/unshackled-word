using UnshackledWord.Domain.Models.Grammar;
using UnshackledWord.Domain.WebApi.Elberfelder.GetWordDetail;

namespace UnshackledWord.Application.Features.VerseMapping;

public static class MorphologyResponses
{
    /// <summary>Decodes a STEP morphology code into translation keys (Greek: whole word, Hebrew: root part); null for an empty code.</summary>
    public static MorphologyResponse? Decode(bool hebrew, string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var keys = hebrew ? HebrewMorphologyDecoder.Decode(code).Root : GreekMorphologyDecoder.Decode(code);
        return new MorphologyResponse
        {
            Code = code.Trim(),
            PartOfSpeech = keys.PartOfSpeech,
            Tense = keys.Tense,
            Voice = keys.Voice,
            Mood = keys.Mood,
            Person = keys.Person,
            Case = keys.Case,
            Number = keys.Number,
            Gender = keys.Gender,
            Degree = keys.Degree,
            Stem = keys.Stem,
            HebrewForm = keys.HebrewForm,
            State = keys.State,
        };
    }
}
