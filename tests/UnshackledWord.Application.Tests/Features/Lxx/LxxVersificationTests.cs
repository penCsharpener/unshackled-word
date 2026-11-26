using UnshackledWord.Application.Features.Lxx;

namespace UnshackledWord.Application.Tests.Features.Lxx;

/// <summary>
/// Roadmap 0028.2: Swete → Rahlfs → LxxRefId/HebRefId on TVTMS snippets copied from the real file and short Swete verses (the five
/// examples of 03-versification.md plus Jos 9, Gen 2, Daniel OG/Theodotion and the Malachi 4 fix).
/// </summary>
public class LxxVersificationTests
{
    private const string Tvtms = """
        $Psa.3:1-3:8
        BIBLES | English KJV | Hebrew | Latin | Greek | EngTitleMerged
        OneToOne | Psa.3:Title | Psa.3:1 | Psa.3:1 | Psa.3:1 | Absent [=Psa.3:1]
        OneToOne | Psa.3:1 | Psa.3:2 | Psa.3:2 | Psa.3:2 | Psa.3:1
        OneToOne | Psa.3:2-8 | Psa.3:3-9 | Psa.3:3-9 | Psa.3:3-9 | Psa.3:2-8
        $Psa.9:19--10:18
        BIBLES | English KJV | Hebrew | Latin | Greek | EngTitleMerged
        OneToOne | Psa.10:1-18 | Psa.10:1-18 | Psa.9:22-39 | Psa.9:22-39 | Psa.10:1-18
        $Jer.25:15-38 | English KJV | Hebrew | Latin | Greek* | Greek2 (eg Brenton)*
        OneToOne | Jer.25:14 | Jer.25:14 | Jer.25:14 | Absent | Absent
        OneToOne | Jer.25:15-38 | Jer.25:15-38 | Jer.25:15-38 | Jer.32:1-24 | Jer.32:15-38
        $1Ki.2:8--7:23 | English KJV | Hebrew | Latin | Greek* | GreekUndivided*
        LongVerse/LVDuplicated | 1Ki.2:35 | 1Ki.2:35 | 1Ki.2:35 | 1Ki.2:35.0-14 [=1Ki.4:29-30] | 1Ki.2:35 [=+1Ki.4:29-30]
        $Est.1:1-22 | English KJV | Hebrew | Latin* | Greek | GreekUndivided
        OneToOne | Est.1:1.0 | Est.1:1 | Est.1:1 | Est.1:1.0 | Est.1:1 [=+Est.11:2-12; 12:1-6]
        LongVerse/LVExtra | Est.1:1.1-17 | NoVerse | Est.A:1-17 | Est.1:1.1-17 | NoVerse
        OneToOne | Est.1:2-22 | Est.1:2-22 | Est.1:2-22 | Est.1:2-22 | Est.1:2-22
        $Jos.8:30--9:27 | English KJV | Hebrew | Latin | Greek | Greek2
        LongVerseElsewhere | Jos.8:30-35 | Jos.8:30-35 | Jos.8:30-35 | Jos.9:2.1-6 | Absent [=Jos.9:2.1-6]
        LongVerse/LVElsewhere | Jos.9:2 | Jos.9:2 | Jos.9:2 | Jos.9:2.0 | Jos.9:2 [=+Jos.8:30-35]
        $Gen.2:24--3:1 | English KJV | Hebrew | Latin | Greek | Greek2 (eg NETS)
        MergedPrevVerse | Gen.2:25 | Gen.2:25 | Gen.2:25 | Gen.2:25 | Absent [=Gen.3:1]
        $Dan.3:23--4:3 | English KJV | Hebrew | Latin* | Greek*
        OneToOne | S3Y.1:1-28 | NoVerse | Dan.3:24-51 | Dan.3:24-51
        #OneToOne | S3Y.1:29 | NoVerse | Dan.3:52 | Dan.3:52
        OneToOne | Dan.3:24-30 | Dan.3:24-30 | Dan.3:91-97 | Dan.3:91-97
        OneToOne | Dan.4:1-3 | Dan.3:31-33 | Dan.3:98-100 | Absent
        $Dan.5:31--6:28 | English KJV | Hebrew | Latin | Greek
        OneToOne | Dan.5:31 | Dan.6:1 | Dan.5:31 | Dan.6:1
        OneToOne | Dan.6:1-28 | Dan.6:2-29 | Dan.6:1-28 | Dan.6:2-29
        $Mal.4:1-4:6 | English KJV | Hebrew | Latin | Greek* | Greek2
        OneToOne | Mal.4:1-3 | Mal.3:19-21 | Mal.4:1-3 | Mal.4:1-3 | Mal.3:19-21
        OneToOne | Mal.4:1-3 | Mal.3:19-21 | Mal.4:1-3 | Mal.4:1-35 | Mal.3:19-21
        OneToOne | Mal.4:4 | Mal.3:22 | Mal.4:4 | Mal.4:5 | Mal.3:24
        OneToOne | Mal.4:5 | Mal.3:23 | Mal.4:5 | Mal.4:6 | Mal.3:22
        OneToOne | Mal.4:6 | Mal.3:24 | Mal.4:6 | Mal.4:4 | Mal.3:23
        """;

    private const string Exceptions = """
        SweteRef;RahlfsRef;TextForm;Note
        Gen.3:1#1-4;Gen.2:25;;Swete prints 2:25 as the start of 3:1
        Gen.3:1#5-;Gen.3:1;;rest of 3:1
        Jos.9:3-8;Jos.9:2.1-6;;Rahlfs 9:2a-f
        Jos.9:9;Jos.9:3;;shifted
        Est.1:1[18];Est.1:1;;canonical 1:1
        Dan.5:31;Dan.6:1;og;English numbering
        Dat.5:31;Dan.6:1;th;English numbering
        """;

    private static TvtmsGreekTable Table() =>
        TvtmsGreekParser.Parse(("#DataStart(Condensed)\n" + Tvtms.Replace(" | ", "\t") + "\n#DataEnd(Condensed)").Split('\n'));

    private static SweteVerse Verse(string reference, string text)
    {
        var dot = reference.IndexOf('.');
        var colon = reference.IndexOf(':');
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select((w, i) => new SweteWord(i + 1, i + 1, w)).ToList();

        return new SweteVerse(1, reference[..dot], int.Parse(reference[(dot + 1)..colon]), int.Parse(reference[(colon + 1)..]),
            words.Count == 0 ? [new SweteWord(1, 1, "")] : words);
    }

    private static LxxVersificationResult Map(params SweteVerse[] verses)
    {
        var errors = new List<string>();
        var exceptions = SweteRahlfsExceptionList.Parse(Exceptions.Split('\n'), errors)
            .Where(e => verses.Any(v => e.Swete!.Covers(v.BookCode, v.Chapter, v.Verse)))
            .ToList();
        errors.ShouldBeEmpty();
        var result = LxxVersification.Map(verses, exceptions, Table());
        result.Errors.ShouldBeEmpty();

        return result;
    }

    private static int RefId(int book, int chapter, int verse) => LxxVersification.RefId(book, chapter, verse);

    [Fact]
    public void Jer_32_1_Is_Hebrew_And_English_Jer_25_15()
    {
        var m = Map(Verse("Jer.32:1", "Οὕτως εἶπεν Κύριος")).Verses.Single();

        m.RahlfsRef.ShouldBe("Jer.32:1");
        m.RahlfsRefId.ShouldBe(24_032_001_00L);
        m.LxxRefId.ShouldBe(RefId(24, 25, 15));
        m.HebRefId.ShouldBe(RefId(24, 25, 15));
        m.MapKind.ShouldBe(LxxMapKinds.One);
    }

    [Fact]
    public void Ps_9_22_Is_Ps_10_1()
    {
        var m = Map(Verse("Psa.9:22", "ἵνα τί, Κύριε")).Verses.Single();

        m.LxxRefId.ShouldBe(RefId(19, 10, 1));
        m.HebRefId.ShouldBe(RefId(19, 10, 1));
    }

    [Fact]
    public void Psalm_Title_Is_English_Verse_0_And_Hebrew_Verse_1()
    {
        var result = Map(Verse("Psa.3:1", "ψαλμὸς τῷ Δαυείδ"), Verse("Psa.3:2", "Κύριε, τί ἐπληθύνθησαν"));

        result.Verses[0].LxxRefId.ShouldBe(RefId(19, 3, 0));
        result.Verses[0].HebRefId.ShouldBe(RefId(19, 3, 1));
        result.Verses[1].LxxRefId.ShouldBe(RefId(19, 3, 1));
        result.Verses[1].HebRefId.ShouldBe(RefId(19, 3, 2));
    }

    [Fact]
    public void Kingdoms_2_35_Markers_Become_Additions_After_The_Verse()
    {
        var result = Map(Verse("1Ki.2:35", "καὶ ἔδωκεν ὁ βασιλεὺς [1] Καὶ ἔδωκεν Κύριος φρόνησιν [2] καὶ ἐπληθύνθη"));

        result.Verses.Count.ShouldBe(3);
        result.Verses[0].RahlfsRef.ShouldBe("1Ki.2:35");
        result.Verses[0].MapKind.ShouldBe(LxxMapKinds.One);
        result.Verses[0].HebRefId.ShouldBe(RefId(11, 2, 35));
        result.Verses[1].RahlfsRef.ShouldBe("1Ki.2:35.1");
        result.Verses[1].RahlfsRefId.ShouldBe(11_002_035_01L);
        result.Verses[1].MapKind.ShouldBe(LxxMapKinds.Addition);
        result.Verses[1].HebRefId.ShouldBeNull();
        result.Verses[1].LxxRefId.ShouldBeNull();
        result.Verses[1].AfterHebRefId.ShouldBe(RefId(11, 2, 35));
        result.Verses[1].Words[0].Text.ShouldBe("[1]");
        result.Verses[2].RahlfsRef.ShouldBe("1Ki.2:35.2");
    }

    [Fact]
    public void Esther_Addition_A_Precedes_The_Canonical_1_1_And_Is_Shown_After_It()
    {
        var result = Map(Verse("Est.1:1", "⸂[1] ΕΤΟΥΣ δευτέρου [2] ἄνθρωπος Ἰουδαῖος⸃ [18] Καὶ ἐγένετο"), Verse("Est.1:2", "ἐν αὐταῖς"));

        result.Verses.Select(v => v.RahlfsRef).ShouldBe(["Est.1:1.1", "Est.1:1.2", "Est.1:1", "Est.1:2"]);
        result.Verses[0].MapKind.ShouldBe(LxxMapKinds.Addition);
        result.Verses[0].AfterHebRefId.ShouldBe(RefId(17, 1, 1));
        result.Verses[2].MapKind.ShouldBe(LxxMapKinds.One);
        result.Verses[2].HebRefId.ShouldBe(RefId(17, 1, 1));
        result.Verses[2].Origin.ShouldBe(LxxMapOrigins.Exception);
    }

    [Fact]
    public void Joshua_9_3_In_Swete_Is_Rahlfs_9_2a_And_Hebrew_8_30()
    {
        var result = Map(Verse("Jos.9:2", "συνήλθοσαν"), Verse("Jos.9:3", "Τότε οἰκοδόμησεν"), Verse("Jos.9:4", "καθότι"), Verse("Jos.9:5", "καὶ"),
            Verse("Jos.9:6", "καὶ"), Verse("Jos.9:7", "καὶ"), Verse("Jos.9:8", "οὐκ"), Verse("Jos.9:9", "Καὶ οἱ κατοικοῦντες Γαβαὼν"));

        var a = result.Verses.Single(v => v.SweteRef == "Jos.9:3");
        a.RahlfsRef.ShouldBe("Jos.9:2.1");
        a.HebRefId.ShouldBe(RefId(6, 8, 30));
        a.MapKind.ShouldBe(LxxMapKinds.One);
        result.Verses.Single(v => v.SweteRef == "Jos.9:8").HebRefId.ShouldBe(RefId(6, 8, 35));
        var gibeon = result.Verses.Single(v => v.SweteRef == "Jos.9:9");
        gibeon.RahlfsRef.ShouldBe("Jos.9:3");
        gibeon.HebRefId.ShouldBe(RefId(6, 9, 3));
    }

    [Fact]
    public void Genesis_3_1_Is_Split_Back_Into_2_25_And_3_1()
    {
        var result = Map(Verse("Gen.3:1", "καὶ ἦσαν οἱ γυμνοί Ὁ δὲ ὄφις"));

        result.Verses.Select(v => v.RahlfsRef).ShouldBe(["Gen.2:25", "Gen.3:1"]);
        result.Verses[0].SweteRef.ShouldBe("Gen.3:1#1-4");
        result.Verses[0].HebRefId.ShouldBe(RefId(1, 2, 25));
        result.Verses[0].Words.Select(w => w.Position).ShouldBe([1, 2, 3, 4]);
        result.Verses[1].Words[0].Text.ShouldBe("Ὁ");
        result.Verses[1].HebRefId.ShouldBe(RefId(1, 3, 1));
    }

    [Fact]
    public void Daniel_Keeps_Old_Greek_And_Theodotion_Apart()
    {
        var result = Map(Verse("Dan.5:31", "καὶ Ἀρταξέρξης"), Verse("Dan.3:52", "Εὐλογητὸς εἶ"), Verse("Dan.3:98", "Ναβουχοδονοσὸρ"),
            Verse("Dat.5:31", "καὶ Δαρεῖος"), Verse("Dat.3:23", "καὶ οἱ τρεῖς"), Verse("Dat.3:24", "καὶ περιεπάτουν"));

        var og = result.Verses.Single(v => v.SweteRef == "Dan.5:31");
        var th = result.Verses.Single(v => v.SweteRef == "Dat.5:31");
        (og.TextForm, th.TextForm).ShouldBe(("og", "th"));
        og.RahlfsRef.ShouldBe("Dan.6:1");
        th.RahlfsRef.ShouldBe("Dan.6:1");
        og.HebRefId.ShouldBe(RefId(27, 6, 1));
        og.LxxRefId.ShouldBe(RefId(27, 5, 31));
        og.RahlfsRefId.ShouldBe(th.RahlfsRefId);

        // TVTMS comments Dan 3:52-90 out (fix: addition) and marks 3:98-100 absent (fix: Heb 3:31-33).
        var song = result.Verses.Single(v => v.SweteRef == "Dan.3:52");
        song.MapKind.ShouldBe(LxxMapKinds.Addition);
        song.TvtmsFix.ShouldBeTrue();
        var decree = result.Verses.Single(v => v.SweteRef == "Dan.3:98");
        decree.HebRefId.ShouldBe(RefId(27, 3, 31));
        decree.LxxRefId.ShouldBe(RefId(27, 4, 1));
        result.Verses.Single(v => v.SweteRef == "Dat.3:24").AfterHebRefId.ShouldBe(RefId(27, 3, 23));
    }

    [Fact]
    public void Malachi_4_Fix_Drops_The_Typo_And_Restores_The_Greek_Order()
    {
        var result = Map(Verse("Mal.4:3", "καὶ καταπατήσετε"), Verse("Mal.4:4", "Καὶ ἰδοὺ ἐγὼ ἀποστέλλω ὑμῖν Ἠλίαν"),
            Verse("Mal.4:5", "ὃς ἀποκαταστήσει καρδίαν"), Verse("Mal.4:6", "Μνήσθητε νόμου Μωσῆ"));

        result.Verses.Select(v => v.HebRefId).ShouldBe([RefId(39, 3, 21), RefId(39, 3, 23), RefId(39, 3, 24), RefId(39, 3, 22)]);
        result.Verses.Select(v => v.LxxRefId).ShouldBe([RefId(39, 4, 3), RefId(39, 4, 5), RefId(39, 4, 6), RefId(39, 4, 4)]);
        result.Verses.Skip(1).ShouldAllBe(v => v.TvtmsFix);
        LxxVersification.GreekVerses(result.Table)[("Mal", "4")].Verses.Max().ShouldBe(6);
    }

    [Fact]
    public void Empty_Swete_Verse_Is_Absent_And_Has_No_Placement()
    {
        var m = Map(Verse("Jer.25:14", "")).Verses.Single();

        m.IsEmpty.ShouldBeTrue();
        m.MapKind.ShouldBe(LxxMapKinds.Absent);
        m.HebRefId.ShouldBeNull();
        m.AfterHebRefId.ShouldBeNull();
    }

    [Fact]
    public void Exception_List_Rejects_Bad_Rows_And_Requires_A_Note()
    {
        var errors = new List<string>();
        var rows = SweteRahlfsExceptionList.Parse(
        [
            SweteRahlfsExceptionList.Header,
            "Joe.2:28-32;Jol.3:1-5;;English numbering",
            "Joe.2:28;Jol.3:1;;",
            "Joe.2:28;Jol.2:32-3:1;;crosses a chapter",
            "Dan.5:31;Dan.6:1;th;wrong form",
            ";Pro.15:31;;gap row",
            "Est.5:1[7-11];Est.5:2.1-5;;markers",
        ], errors);

        rows.Count.ShouldBe(5);
        errors.Count.ShouldBe(3);
        rows[0].Rahlfs.Count.ShouldBe(5);
        rows.Single(r => r.Swete is null).Rahlfs.Single().ShouldBe(new TvtmsRef("Pro", "15", 31, 0));
        rows[^1].Swete!.FromMarker.ShouldBe(7);
        rows[^1].Rahlfs[^1].ShouldBe(new TvtmsRef("Est", "5", 2, 5));
    }

    [Fact]
    public void A_Default_Reference_Given_Twice_Is_An_Error()
    {
        var errors = new List<string>();
        var exceptions = SweteRahlfsExceptionList.Parse([SweteRahlfsExceptionList.Header, "Jer.32:1;Jer.32:2;;wrong"], errors);

        var result = LxxVersification.Map([Verse("Jer.32:1", "Οὕτως"), Verse("Jer.32:2", "λάβε")], exceptions, Table());

        result.Errors.ShouldContain(e => e.Contains("given twice"));
    }
}
