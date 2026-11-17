using UnshackledWord.Application.Features.LinkRoles;

namespace UnshackledWord.Application.Tests.Features.LinkRoles;

public class HebrewMorphemeHintsTests
{
    // Tags and grammar are real StepHebrewWords rows (Gen 1:3, Rt 1:5, Rt 1:22, Rt 3:4) unless noted.
    private const string Wayyomer = "H9001=ו=&/{H0559=אָמַר=to say}";
    private const string AndChilion = "H9002=ו=and/{H3630=כִּלְיוֹן=Chilion}";
    private const string TheMoabite = "H9009=ה=the/{H4125=מוֹאָבִי=Moab}";
    private const string FromTwo = "H9006=מ=from/{H8147=שְׁנַ֫יִם=two}";
    private const string AndFromHerHusband = "H9002=ו=and/H9006=מ=from/{H0376H=אִישׁ=husband}/H9024=Ps3f=her\\H9016=׃=verseEnd";
    private const string InBeginning = "H9003=ב=in/{H8462=תְּחִלָּה=beginning}";
    private const string HerChildren = "{H3206=יֶ֫לֶד=youth}/H9024=Ps3f=her";
    private const string WithHer = "{H5973A=עִם=with}/H9034=Os3f=her";
    private const string HisFeet = "{H4772=מַרְגְּלוֹת=feet}/H9023=Ps3m=his";
    private const string ToYou = "H9005=ל=to/{H9032=Os2f=you}";
    private const string WhenHeLiesDown = "H9003=ב=in/{H7901G=שָׁכַב=to lie down}/H9043=Ss3m=he";
    private const string LikeDust = "H9004=כ=like/{H6083=עָפָר=dust}";          // Gen 13:16 style, prefix row from the inventory
    private const string ToBethlehem = "{H1035G=בֵּית לֶ֫חֶם=Bethlehem}/H9011=Sd=to";  // directional he

    [Theory]
    [InlineData("KON", "Und", "und", null, null, Wayyomer, "H9001")]
    [InlineData("KON", "und", "und", null, null, AndChilion, "H9002")]
    [InlineData("KON", "aber", "aber", null, null, AndChilion, "H9002")]
    [InlineData("KOUS", "Da", "da", null, null, Wayyomer, null)]
    [InlineData("ART", "die", "der", null, null, TheMoabite, "H9009")]
    [InlineData("ART", "den", "der", null, null, FromTwo, null)]
    [InlineData("APPR", "von", "von", null, null, FromTwo, "H9006")]
    [InlineData("PTKVZ", "aus", "aus", null, null, FromTwo, "H9006")]
    [InlineData("APPR", "von", "von", null, null, AndFromHerHusband, "H9006")]
    [InlineData("KON", "und", "und", null, null, AndFromHerHusband, "H9002")]
    [InlineData("APPRART", "beim", "bei", null, null, InBeginning, "H9003")]
    [InlineData("APPRART", "im", null, null, null, InBeginning, "H9003")]
    [InlineData("APPR", "in", "in", null, null, InBeginning, "H9003")]
    [InlineData("APPR", "mit", "mit", null, null, WithHer, null)]
    [InlineData("KOKOM", "wie", "wie", null, null, LikeDust, "H9004")]
    [InlineData("APPR", "nach", "nach", null, null, ToBethlehem, "H9011")]
    [InlineData("APPR", "zu", "zu", null, null, ToYou, "H9005")]
    [InlineData("PPOSAT", "ihren", "ihr", null, null, HerChildren, "H9024")]
    [InlineData("PPOSAT", "ihrem", "ihr", null, null, AndFromHerHusband, "H9024")]
    [InlineData("PPOSAT", "seinen", "sein", null, null, HisFeet, "H9023")]
    [InlineData("PPOSAT", "seinen", "sein", null, null, HerChildren, "H9024")]   // sein of a neuter possessor (das Weib) → her
    [InlineData("PPOSAT", "meinen", "mein", null, null, HisFeet, null)]
    [InlineData("PPOSAT", "ihr", "ihr", "S", "Dat", WithHer, "H9034")]          // Rt 1:22 "mit ihr", tagged PPOSAT
    [InlineData("PPER", "dir", "dir", "S", "Dat", ToYou, "H9032")]
    [InlineData("PPER", "er", "er", "S", "Nom", WhenHeLiesDown, "H9043")]
    [InlineData("PRF", "sich", "sich", "S", "Acc", WhenHeLiesDown, "H9043")]
    [InlineData("PPER", "sie", "sie", "P", "Nom", WhenHeLiesDown, null)]
    [InlineData("PPER", "ich", "ich", "S", "Nom", HisFeet, null)]
    [InlineData("PPER", "Jesus", "Jesus", null, null, HisFeet, null)]            // mis-tagged name: no pronoun form
    [InlineData("NN", "Söhnen", "Sohn", null, null, HerChildren, null)]
    public void Finds_the_morpheme_a_German_word_renders(string stts, string text, string? lemma, string? number, string? @case, string tags, string? expected)
    {
        var word = new LinkRoleGermanWord(1, 1, text, lemma, stts, null, number, null, @case);

        var hint = HebrewMorphemeHints.Find(word, SourceMorphemes.ParseHebrew(tags));

        hint.Strongs.ShouldBe(expected);
        hint.Kind.ShouldBe(expected is null ? MorphemeHintKind.None : MorphemeHintKind.Match);
    }

    [Fact]
    public void Zu_before_an_infinitive_hints_the_lamed()
    {
        var word = new LinkRoleGermanWord(1, 1, "zu", "zu", "PTKVZ");

        HebrewMorphemeHints.Find(word, SourceMorphemes.ParseHebrew("H9005=ל=to/{H0398=אָכַל=to eat}"), zuInfinitive: true).Strongs.ShouldBe("H9005");
    }

    [Fact]
    public void Two_fitting_suffixes_are_ambiguous()
    {
        var word = new LinkRoleGermanWord(1, 1, "ihrem", "ihr", "PPOSAT");

        var hint = HebrewMorphemeHints.Find(word, SourceMorphemes.ParseHebrew(HerChildren + "/H9028=Pp3m=their"));

        hint.Kind.ShouldBe(MorphemeHintKind.Ambiguous);
        hint.Candidates.ShouldBe(["H9024", "H9028"]);
    }

    [Fact]
    public void Possessives_prefer_possessive_suffixes()
    {
        // Synthetic: both a possessive (H9023 his) and an object suffix (H9033 him) of 3ms.
        var word = new LinkRoleGermanWord(1, 1, "sein", "sein", "PPOSAT");

        HebrewMorphemeHints.Find(word, SourceMorphemes.ParseHebrew("{H4772=x=feet}/H9023=Ps3m=his/H9033=Os3m=him")).Strongs.ShouldBe("H9023");
    }
}
