using UnshackledWord.Application.Features.VerseMapping;
using UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping;

namespace UnshackledWord.Application.Tests.Features;

/// <summary>
/// Invariants of roadmap 0018 (04-roles-and-counting.md) in <see cref="SaveVerseMappingValidator"/>. Ids are the real ones of the dev data
/// (Mk 1:8, Mk 1:35, Gen 1:3); the <c>zum</c> verse is made up (εἰς τὸν …).
/// </summary>
public class LinkRoleValidationTests
{
    private static VerseMappingLink Link(int word, int step, string? role = null, string? strongs = null, bool? multiHead = null) =>
        new() { ElbWordId = word, StepWordId = step, Role = role, StrongsNumber = strongs, AllowMultiHead = multiHead };

    private static string[] Greek(bool enforce, Dictionary<int, string?> stts, Dictionary<int, string?> strongs, params VerseMappingLink[] links)
    {
        var result = new SaveVerseMappingValidator().Validate(new VerseMappingSaveInput
        {
            Links = links,
            GermanWordIds = stts.Keys.ToHashSet(),
            OriginalWordIds = strongs.Keys.ToHashSet(),
            GermanWordTexts = stts.Keys.ToDictionary(k => k, _ => "x"),
            OriginalWordStrongs = strongs,
            GermanWordStts = stts,
            EnforceSingleHead = enforce,
        });
        return result.Errors.Select(e => e.ErrorMessage).ToArray();
    }

    // ---- Mk 1:8 "habe … getauft" = ἐβάπτισα (19025, G0907) -------------------------------------------------------------------

    private static string[] Mk18(bool enforce, params VerseMappingLink[] links) => Greek(
        enforce,
        new Dictionary<int, string?> { [574860] = "VAFIN", [574864] = "VVPP" },
        new Dictionary<int, string?> { [19025] = "G0907" },
        links);

    [Fact]
    public void Mk1_8_AuxiliaryUnitWithHead_IsValid()
    {
        Mk18(true, Link(574860, 19025, MappingRoles.Unit), Link(574864, 19025, MappingRoles.Head)).ShouldBeEmpty();
    }

    [Fact]
    public void Mk1_8_TwoHeads_AreRejected_WhenRolesAreKnown()
    {
        Mk18(true, Link(574860, 19025, MappingRoles.Head), Link(574864, 19025, MappingRoles.Head))
            .ShouldContain(e => e.Contains("2 head links") && e.Contains("allowMultiHead"));
    }

    [Fact]
    public void Mk1_8_TwoHeads_AreAccepted_WithTheMultiHeadFlag()
    {
        Mk18(true, Link(574860, 19025, MappingRoles.Head, multiHead: true), Link(574864, 19025, MappingRoles.Head)).ShouldBeEmpty();
    }

    [Fact]
    public void Mk1_8_TwoHeads_AreNotChecked_ForLegacySaves()
    {
        Mk18(false, Link(574860, 19025), Link(574864, 19025)).ShouldBeEmpty();
    }

    [Fact]
    public void Mk1_8_UnitWithoutHead_IsRejected()
    {
        Mk18(true, Link(574860, 19025, MappingRoles.Unit))
            .ShouldContain(e => e.Contains("word 574860 is a 'unit'") && e.Contains("no head link"));
    }

    [Fact]
    public void UnknownRole_IsRejected()
    {
        Mk18(true, Link(574864, 19025, "main")).ShouldContain(e => e.Contains("role must be 'head' or 'unit'"));
    }

    [Fact]
    public void MultiHeadFlagOnAUnit_IsRejected()
    {
        Mk18(true, Link(574860, 19025, MappingRoles.Unit, multiHead: true), Link(574864, 19025))
            .ShouldContain(e => e.Contains("allowMultiHead is only for head links"));
    }

    [Fact]
    public void GreekStrongsNumber_MayEchoTheWordButNotChangeIt()
    {
        Mk18(true, Link(574864, 19025, strongs: "G0907")).ShouldBeEmpty();
        Mk18(true, Link(574864, 19025, strongs: "G1234")).ShouldContain(e => e.Contains("only for Hebrew morphemes"));
    }

    // ---- Mk 1:35 "ging hinaus" = ἐξῆλθεν (19476, G1831) --------------------------------------------------------------------

    private static string[] Mk135(params VerseMappingLink[] links) => Greek(
        true,
        new Dictionary<int, string?> { [575392] = "VVFIN", [575393] = "APZR", [575395] = "VVFIN" },
        new Dictionary<int, string?> { [19476] = "G1831", [19478] = "G0565" },
        links);

    [Fact]
    public void Mk1_35_ParticleUnitWithHead_IsValid()
    {
        Mk135(Link(575392, 19476), Link(575393, 19476, MappingRoles.Unit), Link(575395, 19478)).ShouldBeEmpty();
    }

    [Fact]
    public void Mk1_35_UnitOnlyCountsAHeadOfTheSameSourceWord()
    {
        // "hinaus" is a unit of ἐξῆλθεν, but only ἀπῆλθεν (19478) has a head.
        Mk135(Link(575393, 19476, MappingRoles.Unit), Link(575395, 19478))
            .ShouldContain(e => e.Contains("Original word 19476") && e.Contains("no head link"));
    }

    [Fact]
    public void Role_OnlyOnRealLinks()
    {
        Mk135(
                Link(575392, 19476),
                new VerseMappingLink { ElbWordId = 575393, IsAddedWord = true, ParentElbWordId = 575392, Role = MappingRoles.Unit, StrongsNumber = "G1831", AllowMultiHead = true })
            .ShouldBe(
            [
                "Link 2 (word 575393): an added word has no role; only links to an original word can be 'unit'.",
                "Link 2 (word 575393): an added word has no strongsNumber.",
                "Link 2 (word 575393): allowMultiHead is only for head links.",
            ]);
    }

    // ---- zum = εἰς (head) + τόν (unit, article) --------------------------------------------------------------------------------

    private static string[] Zum(string? stts, params VerseMappingLink[] links) => Greek(
        true,
        new Dictionary<int, string?> { [1] = stts, [2] = "NN" },
        new Dictionary<int, string?> { [100] = "G1519", [101] = "G3588", [102] = "G0290" },
        links);

    [Fact]
    public void Zum_ContractionIsUnitOfTheArticleWithoutAHead()
    {
        Zum("APPRART", Link(1, 100), Link(1, 101, MappingRoles.Unit), Link(2, 102)).ShouldBeEmpty();
    }

    [Fact]
    public void Zum_ExceptionNeedsTheApprartTag()
    {
        Zum("APPR", Link(1, 100), Link(1, 101, MappingRoles.Unit), Link(2, 102)).ShouldContain(e => e.Contains("Original word 101"));
    }

    [Fact]
    public void Zum_ExceptionNeedsTheContractionToHeadThePreposition()
    {
        Zum("APPRART", Link(1, 101, MappingRoles.Unit), Link(2, 102)).ShouldContain(e => e.Contains("Original word 101"));
    }

    [Fact]
    public void Zum_ExceptionOnlyForTheArticle()
    {
        Zum("APPRART", Link(1, 101), Link(1, 100, MappingRoles.Unit), Link(2, 102)).ShouldContain(e => e.Contains("Original word 100"));
    }

    // ---- Gen 1:3 וַיֹּאמֶר (22) = H9001 + H0559, אֱלֹהִים (23), יְהִי (24), א֑וֹר (25) ------------------------------------------

    private static readonly Dictionary<int, IReadOnlyList<VerseMappingMorpheme>> Gen13Morphemes = new()
    {
        [22] = HebrewMorphemes.Parse("H9001=ו=&/{H0559=אָמַר=to say}"),
        [23] = HebrewMorphemes.Parse("{H0430G=אֱלֹהִים=God»LORD@Gen.1.1-Heb}"),
        [24] = HebrewMorphemes.Parse("{H1961=הָיָה=to be}"),
        [25] = HebrewMorphemes.Parse("{H0216=אוֹר=light}\\H9016=׃=verseEnd"),
    };

    private static VerseMappingSaveInput Gen13Input(params VerseMappingLink[] links) => new()
    {
        Links = links,
        GermanWordIds = new HashSet<int> { 31, 32, 33, 34, 35, 36 },
        OriginalWordIds = Gen13Morphemes.Keys.ToHashSet(),
        GermanWordTexts = new Dictionary<int, string> { [31] = "Und", [32] = "Gott", [33] = "sprach:", [34] = "Es", [35] = "werde", [36] = "Licht!" },
        HebrewMorphemes = Gen13Morphemes,
        GermanWordStts = new Dictionary<int, string?> { [31] = "KON", [32] = "NN", [33] = "VVFIN", [34] = "PPER", [35] = "VAFIN", [36] = "NN" },
        EnforceSingleHead = true,
    };

    private static string[] Gen13(params VerseMappingLink[] links) =>
        new SaveVerseMappingValidator().Validate(Gen13Input(links)).Errors.Select(e => e.ErrorMessage).ToArray();

    private static VerseMappingLink EsAdded() => new() { ElbWordId = 34, IsAddedWord = true, ParentElbWordId = 35 };

    [Fact]
    public void Gen1_3_UndAndSprachHeadTheirOwnMorphemes()
    {
        Gen13(Link(31, 22, strongs: "H9001"), Link(32, 23), Link(33, 22), EsAdded(), Link(35, 24), Link(36, 25)).ShouldBeEmpty();
    }

    [Fact]
    public void Gen1_3_ExplicitMainMorphemeEqualsNull()
    {
        // "sprach" with H0559 spelled out is the same morpheme as without strongsNumber, so a second head on it is caught.
        Gen13(Link(31, 22), Link(33, 22, strongs: "h0559")).ShouldContain(e => e.Contains("morpheme H0559") && e.Contains("2 head links"));
    }

    [Fact]
    public void Gen1_3_WithoutMorphemes_UndAndSprachAreTwoHeadsOfTheMainMorpheme()
    {
        Gen13(Link(31, 22), Link(33, 22)).ShouldContain(e => e.Contains("Original word 22 morpheme H0559"));
    }

    [Fact]
    public void Gen1_3_StrongsNumberMustBeAMorphemeOfTheLinkedWord()
    {
        Gen13(Link(31, 23, strongs: "H9001")).ShouldContain(e => e.Contains("H9001 is not a morpheme of original word 23 (H0430G)"));
    }

    [Fact]
    public void Gen1_3_PunctuationIsNeverAMorpheme()
    {
        Gen13(Link(36, 25, strongs: "H9016")).ShouldContain(e => e.Contains("H9016 is punctuation"));
    }

    [Fact]
    public void Gen1_3_UnitOfThePrefixNeedsAHeadOnThePrefix()
    {
        // "Und" as unit of H9001 while only "sprach" (H0559) is a head of וַיֹּאמֶר.
        Gen13(Link(31, 22, MappingRoles.Unit, "H9001"), Link(33, 22)).ShouldContain(e => e.Contains("morpheme H9001") && e.Contains("no head link"));
    }

    [Fact]
    public void Gen1_3_MorphemeKey_UsesTheMorphemeSpellingOfTheWord()
    {
        var input = Gen13Input();
        input.MorphemeKey(Link(31, 22, strongs: "h9001")).ShouldBe((22, "H9001"));
        input.MorphemeKey(Link(33, 22)).ShouldBe((22, "H0559"));
    }

    // ---- part lemma ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void PartLemma_OnlyWithGermanWordPart()
    {
        var withoutPart = new VerseMappingLink { ElbWordId = 36, StepWordId = 25, PartLemma = "Licht" };
        Gen13(withoutPart).ShouldContain(e => e.Contains("partLemma needs a germanWordPart"));
    }

    [Fact]
    public void PartLemma_WithParts_IsValid_AndLengthIsChecked()
    {
        var result = new SaveVerseMappingValidator().Validate(new VerseMappingSaveInput
        {
            Links =
            [
                new VerseMappingLink { ElbWordId = 1, StepWordId = 12, PartOrder = 1, GermanWordPart = "Gersten", PartLemma = "Gerste" },
                new VerseMappingLink { ElbWordId = 1, StepWordId = 13, PartOrder = 2, GermanWordPart = "ernte", PartLemma = new string('x', 41) },
            ],
            GermanWordIds = new HashSet<int> { 1 },
            OriginalWordIds = new HashSet<int> { 12, 13 },
            GermanWordTexts = new Dictionary<int, string> { [1] = "Gerstenernte." },
        });

        result.Errors.Select(e => e.ErrorMessage).ShouldBe(["Link 2 (word 1): partLemma is longer than 40 characters."]);
    }

    // ---- morpheme parsing ------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("H9001=ו=&/{H0559=אָמַר=to say}", "H9001,H0559", "H0559")]
    [InlineData("H9001/{H1961}\\H9014", "H9001,H1961", "H1961")]
    [InlineData("H9006/{H5921A}/H9035\\H9016\\ \\H9018", "H9006,H5921A,H9035", "H5921A")]
    [InlineData("{H6495=פְּקַח־קוֹחַ=opening}+\\H9014=־=link", "H6495", "H6495")]
    [InlineData("H9003/H9009", "H9003,H9009", "H9003")]
    [InlineData("{H9033#6=Os3m=him}", "H9033", "H9033")]                       // instance suffix stripped, like SourceMorphemes
    [InlineData("H9002=ו=and/{H3706=now}\\H9019=׆=section", "H9002,H3706", "H3706")] // H9019 is a section mark
    [InlineData(" ", "", null)]
    public void HebrewMorphemes_ParseSplitsStripsAndDropsPunctuation(string tags, string expected, string? main)
    {
        var morphemes = HebrewMorphemes.Parse(tags);

        string.Join(",", morphemes.Select(m => m.Strongs)).ShouldBe(expected);
        HebrewMorphemes.Main(morphemes).ShouldBe(main);
    }

    [Fact]
    public void HebrewMorphemes_ParseKeepsOriginalAndGloss()
    {
        var und = HebrewMorphemes.Parse("H9001=ו=&/{H0559=אָמַר=to say}")[0];

        (und.Strongs, und.IsMain, und.Original, und.Gloss).ShouldBe(("H9001", false, "ו", "&"));
    }
}
