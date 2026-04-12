using UnshackledWord.Domain.Models.Dbo.Step;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible.StrongsToText;

public static class StrongsTransformationExtensions
{
    public static IEnumerable<StepStrongsToTextDbo> ToDbo(this IEnumerable<StrongsIdLangDto> strongs)
    {
        foreach (var strong in strongs)
        {

            var hebId = strong.Language is StrongsLanguage.Hebrew ? strong.Id : default(int?);
            var gkId = strong.Language is StrongsLanguage.Greek ? strong.Id : default(int?);

            var internalStrongs = StrongsRegexParser.Parse(strong.Strongs);

            foreach (var result in internalStrongs.ToDbo(hebId, gkId))
            {
                yield return result;
            }
        }
    }
}