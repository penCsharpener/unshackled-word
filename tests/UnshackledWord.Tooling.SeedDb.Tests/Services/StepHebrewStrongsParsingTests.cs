using UnshackledWord.Domain.Models.Dbo.Step;
using UnshackledWord.Seeding.StepBible;

namespace UnshackledWord.Tooling.SeedDb.Tests.Services;

/// <summary>Roadmap 0022.1b: TBESH lines whose eStrong column ends in a lower-case letter (H5921a) must be imported.</summary>
public class StepHebrewStrongsParsingTests
{
    // real lines of "TBESH - Translators Brief lexicon of Extended Strongs for Hebrew" (Meaning column shortened)
    private static readonly string[] Lines =
    [
        "H5920\tH5920H =\tH5920H\tעַל\tal\tH:N-M / H:Adv\theight\tsubst<br>1) height",
        "H5921a\tH5921A =\tH5921A\tעַל\tal\tH:Prep\tupon\tprep<br>1) upon, on the ground of",
        "H5921b\tH5921B = a Meaning of\tH3588A\t[כִּי] עַל [כֵּן]\t[ki] al [ken]\tH:Prep\tas\tconj<br> forasmuch as",
        "H5922\tH5922 = in Aramaic of\tH5921A\tעַל\tal\tA:Prep\tsince\t1) upon, over",
        "H3588c\tH3588C = combination of\tH3588A (H3588A+H5921B+H3651C)\tכִּי [עַל כֵּן]\tki [al ken]\tH:Conj\tfor as that: since\t1) that, for",
        "eStrong#\tdStrong\tuStrong\tHebrew\tTransliteration\tMorph\tGloss\tMeaning",
        "",
    ];

    [Fact]
    public void LinesWithLowerCaseExtendedSuffixAreParsed()
    {
        var entries = StepHebrewStrongsStrategy.ParseLines(Lines);

        entries.Count.ShouldBe(5);
        entries.Select(x => $"{x.Number}{x.Extra}").ToArray().ShouldBe(["5920H", "5921A", "5921B", "5922", "3588C"]);
    }

    [Fact]
    public void DisambiguatedKeyAndLanguageComeFromTheRightColumns()
    {
        var entries = StepHebrewStrongsStrategy.ParseLines(Lines);

        var upon = entries.Single(x => x is { Number: 5921, Extra: "A" });
        upon.LanguageId.ShouldBe(StrongsLanguage.Hebrew);
        upon.OriginalWord.ShouldBe("עַל");
        upon.Gloss.ShouldBe("upon");

        entries.Single(x => x.Number == 5922).LanguageId.ShouldBe(StrongsLanguage.Aramaic);
        entries.Single(x => x is { Number: 3588, Extra: "C" }).UnifiedEntries.Count.ShouldBe(3);
    }
}
