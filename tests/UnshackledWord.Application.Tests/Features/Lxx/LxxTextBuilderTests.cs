using UnshackledWord.Application.Features.Lxx;
using UnshackledWord.Application.Features.Seed.Anchoring;

namespace UnshackledWord.Application.Tests.Features.Lxx;

/// <summary>Roadmap 0028.3: the word filter of the Swete stream and the mapping of versification units to <c>LxxVerses</c>/<c>LxxWords</c> rows.</summary>
public class LxxTextBuilderTests
{
    private static List<string> Filter(string tokens, string book = "Gen", Dictionary<LxxDroppedToken, int>? dropped = null) =>
        LxxWordFilter.Filter(tokens.Split(' '), book, dropped);

    [Fact]
    public void Filter_Drops_Marker_Tokens()
    {
        var dropped = new Dictionary<LxxDroppedToken, int>();

        Filter("καὶ ἔδωκεν [1] Καὶ ⸂[2] ἐπληθύνθη", dropped: dropped).ShouldBe(["καὶ", "ἔδωκεν", "Καὶ", "ἐπληθύνθη"]);
        dropped[LxxDroppedToken.Marker].ShouldBe(2);
    }

    [Fact]
    public void Filter_Removes_Editorial_Marks_Alone_And_Attached()
    {
        var dropped = new Dictionary<LxxDroppedToken, int>();

        Filter("⸂αἰρόμενα ⸂⸆⸃ ⸆ χρονιεῖ,⸃ κατέλυσαν.⸆ ⸂⸆⸆⸃", dropped: dropped).ShouldBe(["αἰρόμενα", "χρονιεῖ,", "κατέλυσαν."]);
        dropped[LxxDroppedToken.EditorialMark].ShouldBe(3);
    }

    [Fact]
    public void Filter_Appends_Standalone_Punctuation_To_The_Previous_Word()
    {
        var dropped = new Dictionary<LxxDroppedToken, int>();

        Filter("ἔθνεσιν αὐτῶν · ἡγεμὼν", dropped: dropped).ShouldBe(["ἔθνεσιν", "αὐτῶν·", "ἡγεμὼν"]);
        Filter("· ἡγεμὼν", dropped: dropped).ShouldBe(["ἡγεμὼν"]);
        dropped[LxxDroppedToken.Punctuation].ShouldBe(2);
    }

    [Fact]
    public void Filter_Drops_Acrostic_Letters_Only_In_Lamentations()
    {
        Filter("καὶ εἶπεν Ἄλεφ. Πῶς ἐκάθισεν", "Lam").ShouldBe(["καὶ", "εἶπεν", "Πῶς", "ἐκάθισεν"]);
        Filter("Σάμεχ. Ἀπόστητε", "Lam").ShouldBe(["Ἀπόστητε"]);
        Filter("ὅπως ᾖ. καὶ", "Gen").ShouldBe(["ὅπως", "ᾖ.", "καὶ"]);
        Filter("Ἥ. Ἐγένοντο", "Gen").ShouldBe(["Ἥ.", "Ἐγένοντο"]);
    }

    [Fact]
    public void Filter_Keeps_The_Capitals_At_The_Start_Of_A_Book()
    {
        Filter("ΕΝ ΑΡΧΗ ἐποίησεν ὁ θεὸς").ShouldBe(["ΕΝ", "ΑΡΧΗ", "ἐποίησεν", "ὁ", "θεὸς"]);
    }

    private static LxxVerseMapping Mapping(int order, string sweteRef, string text, long rahlfsRefId = 1_001_001_00L, string kind = LxxMapKinds.One,
        int? lxx = 1_001_001, int? heb = 1_001_001, int? after = null, string book = "Gen", int bookId = 1, string form = "", int chapter = 1, int verse = 1,
        int sub = 0)
    {
        var words = text.Length == 0
            ? new List<SweteWord> { new(1, 1, "") }
            : text.Split(' ').Select((w, i) => new SweteWord(i + 1, i + 1, w)).ToList();

        return new LxxVerseMapping(order, sweteRef, book, 7, 8, words, bookId, book, form, chapter, verse, sub, rahlfsRefId, lxx, heb, after, kind,
            LxxMapOrigins.Identity, false, null, null);
    }

    [Fact]
    public void Build_Maps_A_Unit_To_A_Row_With_Contiguous_Positions()
    {
        var text = LxxTextBuilder.Build([Mapping(5, "Jer.32:1", "⸂⸆⸃ Οὕτως [1] εἶπεν Κύριος,", 2_403_200_100L, lxx: 24_025_015, heb: 24_025_015,
            book: "Jer", bookId: 24, chapter: 32)]);

        var row = text.Verses.ShouldHaveSingleItem();
        row.Verse.BibleBookId.ShouldBe(24);
        row.Verse.Chapter.ShouldBe(32);
        row.Verse.Verse.ShouldBe(1);
        row.Verse.RahlfsRefId.ShouldBe(2_403_200_100L);
        row.Verse.SortOrder.ShouldBe(5);
        row.Verse.SweteRef.ShouldBe("Jer.32:1");
        row.Verse.LxxRefId.ShouldBe(24_025_015);
        row.Verse.HebRefId.ShouldBe(24_025_015);
        row.Verse.MapKind.ShouldBe(LxxMapKinds.One);
        row.Words.Select(w => w.Position).ShouldBe([1, 2, 3]);
        row.Words.Select(w => w.Greek).ShouldBe(["Οὕτως", "εἶπεν", "Κύριος,"]);
        row.Words[2].GreekNoDiacritics.ShouldBe("Κυριος");
        row.Words[2].SrcHash.ShouldBe(SourceKey.Hash("Κύριος,"));
        row.Words[2].SrcHash.ShouldBe(SourceKey.Hash("Κύριος"));
        text.Dropped[LxxDroppedToken.Marker].ShouldBe(1);
        text.Dropped[LxxDroppedToken.EditorialMark].ShouldBe(1);
    }

    [Fact]
    public void PlainForm_Drops_Diacritics_Punctuation_And_Brackets()
    {
        LxxTextBuilder.PlainForm("[τοῖ]ς").ShouldBe("τοις");
        LxxTextBuilder.PlainForm("ἀρχῇ.").ShouldBe("αρχη");
    }

    [Fact]
    public void Build_Skips_Empty_Verses_And_Units_Without_Words()
    {
        var text = LxxTextBuilder.Build([
            Mapping(0, "1Ki.14:1", "", kind: LxxMapKinds.Absent, lxx: null, heb: null),
            Mapping(1, "1Ki.14:2", "⸂⸆⸃ ⸆"),
            Mapping(2, "1Ki.14:21", "καὶ Ῥοβοάμ"),
        ]);

        text.Verses.ShouldHaveSingleItem().Verse.SortOrder.ShouldBe(2);
        text.SkippedEmpty.ShouldBe(1);
        text.SkippedNoWords.ShouldBe(1);
        text.WordCount.ShouldBe(2);
    }

    [Fact]
    public void Build_Keeps_Swete_Order_For_Esther_Addition_A()
    {
        var text = LxxTextBuilder.Build([
            Mapping(10, "Est.1:1[1]", "ΕΤΟΥΣ δευτέρου", 1_700_100_101L, LxxMapKinds.Addition, null, null, 17_001_001, "Est", 17, sub: 1),
            Mapping(11, "Est.1:1[18]", "Καὶ ἐγένετο", 1_700_100_100L, lxx: 17_001_001, heb: 17_001_001, book: "Est", bookId: 17),
        ]);

        text.Verses.Select(v => v.Verse.SortOrder).ShouldBe([10, 11]);
        text.Verses[0].Verse.RahlfsRefId!.Value.ShouldBeGreaterThan(text.Verses[1].Verse.RahlfsRefId!.Value);
        text.Verses[0].Verse.Subverse.ShouldBe(1);
        text.Verses[0].Verse.HebRefId.ShouldBeNull();
        text.Verses[0].Verse.AfterHebRefId.ShouldBe(17_001_001);
    }

    [Fact]
    public void Build_Keeps_Absent_Text_With_Swete_Numbers_And_No_Rahlfs_Reference()
    {
        var text = LxxTextBuilder.Build([Mapping(3, "Pro.24:23", "Ταῦτα δὲ", 0, LxxMapKinds.Absent, null, null, 20_024_022, "Pro", 20, chapter: 0, verse: 0)]);

        var row = text.Verses.ShouldHaveSingleItem().Verse;
        row.RahlfsRefId.ShouldBeNull();
        row.Chapter.ShouldBe(7);
        row.Verse.ShouldBe(8);
        row.MapKind.ShouldBe(LxxMapKinds.Absent);
        row.AfterHebRefId.ShouldBe(20_024_022);
    }

    [Fact]
    public void Build_Keeps_The_Daniel_Text_Form()
    {
        var text = LxxTextBuilder.Build([Mapping(0, "Dat.1:1", "ΕΝ ἔτει", 2_700_100_100L, book: "Dan", bookId: 27, form: "th")]);

        text.Verses.ShouldHaveSingleItem().Verse.TextForm.ShouldBe("th");
    }
}
