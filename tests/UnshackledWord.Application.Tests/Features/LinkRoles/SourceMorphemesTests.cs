using UnshackledWord.Application.Features.LinkRoles;

namespace UnshackledWord.Application.Tests.Features.LinkRoles;

public class SourceMorphemesTests
{
    // Real StepHebrewWords rows (ExpandedStrongTags | Grammar); expected: Strong's[*=main][:grammar segment], space separated.
    [Theory]
    [InlineData("H9001=ו=&/{H0559=אָמַר=to say}", "Hc/Vqw3ms", "H9001:c H0559*:Vqw3ms")]                                     // Gen 1:3
    [InlineData("H9001=ו=&/{H1961=הָיָה=to be}\\H9014=־=link", "Hc/Vqw3ms", "H9001:c H1961*:Vqw3ms")]                        // maqqef dropped
    [InlineData("{H0430G=אֱלֹהִים=God»LORD@Gen.1.1-Heb}", "HNcmpa", "H0430G*:Ncmpa")]
    [InlineData("H9002=ו=and/H9006=מ=from/{H0376H=אִישׁ=: husband»man:2_husband}/H9024=Ps3f=her\\H9016=׃=verseEnd", "HC/R/Ncmsc/Sp3fs",
        "H9002:C H9006:R H0376H*:Ncmsc H9024:Sp3fs")]                                                                         // Rt 1:5
    [InlineData("{H6213A=עָשָׂה=to make}/H9013=Sn=emph.\\H9016=׃=verseEnd", "HVqi2fs/Sn", "H6213A*:Vqi2fs H9013:Sn")]        // Rt 3:4
    [InlineData("H9005=ל=to/{H9032=Os2f=you}", "HRd/Sp2fs", "H9005:Rd H9032*:Sp2fs")]                                         // suffix is the main morpheme
    [InlineData("{H0935G=בּוֹא=: come»to come (in):1_come;_go_in}//{H1409=גָּד=fortune}", "HVqp3ms//Ncmsa", "H0935G*:Vqp3ms H1409*:Ncmsa")]
    [InlineData("{H6301=פְּדָהצוּר=Pedahzur»Pedahzur@Num.1.10-}+/ /{H6301=פְּדָהצוּר=Pedahzur»Pedahzur@Num.1.10-}\\H9016=׃=verseEnd\\ \\H9017=פ=para",
        "HNpm//Npm", "H6301*:Npm")]                                                                                           // same Strong's twice, blanks
    [InlineData("H9002=ו=and/{H3706=כְּעֶ֫נֶת=now}\\H9016=׃=verseEnd\\ \\H9017=פ=para", "AC/D", "H9002:C H3706*:D")]            // Aramaic
    [InlineData("{H9033#6=Os3m=him}", null, "H9033*")]                                                                         // instance suffix
    [InlineData("H9003=ב=in/{H6450=פַּס דַּמִּים=Pas-dammim}+", "HR/Np", "H9003:R H6450*:Np")]
    [InlineData("H9002=ו=and/{H3706=now}\\H9019=׆=section", "HC/D", "H9002:C H3706*:D")]                                        // H9019 is a section mark
    [InlineData("H9001=ו=&/{H0559=אָמַר=to say}", "HVqw3ms", "H9001 H0559*")]                                                   // segment count differs: no grammar
    [InlineData("", "HNcmpa", "")]
    public void Parses_hebrew_morphemes(string tags, string? grammar, string expected)
    {
        var morphemes = SourceMorphemes.ParseHebrew(tags, grammar);

        string.Join(" ", morphemes.Select(m => m.Strongs + (m.IsMain ? "*" : "") + (m.Grammar is null ? "" : ":" + m.Grammar))).ShouldBe(expected);
    }

    [Theory]
    [InlineData("H9020", 1, 's', null)]
    [InlineData("H9024", 3, 's', 'f')]
    [InlineData("H9028", 3, 'p', 'm')]
    [InlineData("H9032", 2, 's', 'f')]
    [InlineData("H9033", 3, 's', 'm')]
    [InlineData("H9043", 3, 's', 'm')]
    [InlineData("H9045", 1, 'p', null)]
    [InlineData("H9049", 3, 'p', 'f')]
    public void Suffix_person_number_gender(string strongs, int person, char number, char? gender)
    {
        SourceMorphemes.SuffixPgn(strongs).ShouldBe(new Pgn(person, number, gender));
    }

    [Theory]
    [InlineData("H9001")]
    [InlineData("H9019")]
    [InlineData("H9050")]
    [InlineData("H0559")]
    public void Not_a_pronoun_suffix(string strongs)
    {
        SourceMorphemes.SuffixPgn(strongs).ShouldBeNull();
    }

    [Theory]
    [InlineData("Vqw3ms", true, false, 3, 's')]
    [InlineData("Vqj3ms", true, false, 3, 's')]
    [InlineData("Vqi2fs", true, false, 2, 's')]
    [InlineData("Vqp3cp", true, false, 3, 'p')]
    [InlineData("Vqv2ms", true, false, 2, 's')]
    [InlineData("Vqrmsa", false, true, null, 's')]
    [InlineData("Vqcc", false, false, null, null)]
    public void Hebrew_verb(string segment, bool finite, bool participle, int? person, char? number)
    {
        SourceMorphemes.HebrewVerb(segment).ShouldBe(new SourceVerb(finite, participle, person, number));
    }

    [Theory]
    [InlineData("Ncmsa")]
    [InlineData("Pp3mp")]
    [InlineData(null)]
    public void Hebrew_non_verb(string? segment)
    {
        SourceMorphemes.HebrewVerb(segment).ShouldBeNull();
    }

    [Theory]
    [InlineData("V-AAI-1S", true, false, 1, 's')]
    [InlineData("V-FAI-3S", true, false, 3, 's')]
    [InlineData("V-2AAI-3P", true, false, 3, 'p')]
    [InlineData("V-AAM-2S", true, false, 2, 's')]
    [InlineData("V-2AAP-NPM", false, true, null, 'p')]
    [InlineData("V-2AAP-NSM", false, true, null, 's')]
    [InlineData("V-PAN", false, false, null, null)]
    public void Greek_verb(string morphology, bool finite, bool participle, int? person, char? number)
    {
        SourceMorphemes.GreekVerb(morphology).ShouldBe(new SourceVerb(finite, participle, person, number));
    }

    [Theory]
    [InlineData("P-1NS")]
    [InlineData("N-DSN")]
    [InlineData("CONJ")]
    public void Greek_non_verb(string morphology)
    {
        SourceMorphemes.GreekVerb(morphology).ShouldBeNull();
    }
}
