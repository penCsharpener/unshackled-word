namespace UnshackledWord.Application.Features.Lxx;

/// <summary>
/// Documented corrections of the TVTMS Greek column (roadmap 0028.2) for places where TVTMS, not Swete, is wrong or incomplete. Applied by
/// <see cref="LxxVersification"/> before any mapping; fixed rows carry a <c>Section</c> starting with <see cref="SectionPrefix"/>.
/// <list type="number">
/// <item><b>Mal 4:</b> the record has a stray row <c>Mal.4:1-35</c> (typo for 4:1-3), and the three rows for Eng 4:4-6 rotate the Greek
/// verses the wrong way. Swete and Rahlfs end with Elijah (Greek 4:4 = Eng 4:5 = Heb 3:23), the turning of hearts (4:5 = Eng 4:6 = Heb 3:24)
/// and Moses (4:6 = Eng 4:4 = Heb 3:22). The stray row is dropped and the three rows get the Greek 4:6 / 4:4 / 4:5.</item>
/// <item><b>Dan 3:52-90:</b> the Song of the Three (S3Y 1:29-68) is commented out with <c>#</c> in TVTMS. Like Dan 3:24-51 (S3Y 1:1-28) it
/// has no Hebrew text: a row with Greek <c>Dan.3:52-90</c> and no English/Hebrew is added, so these verses become additions.</item>
/// <item><b>Dan 3:98-100:</b> TVTMS marks the Greek of Eng 4:1-3 (Heb 3:31-33) as absent, but Swete prints it as 3:98-100 in both the Old
/// Greek and Theodotion (the Latin column has the same numbers). The Greek cell becomes <c>Dan.3:98-100</c>.</item>
/// <item><b>Exo 38:19:</b> the Greek column has no 38:19 (TVTMS files Eng 36:13 under the subverse 37:2.6 and names 38:19 only in its
/// GreekUndivided note). Swete's 38:19 (the gold rings of the tent) is Eng/Heb 36:13; a <c>SubdividedVerse</c> row is added.</item>
/// </list>
/// <see cref="PhantomGreekVerses"/> lists verses that <see cref="TvtmsGreekTable.GreekVerses"/> derives from the English verse-count table
/// but that no Greek Bible has (Deu 28:69: the count table gives Deu 28 69 verses, English has 68, Heb 28:69 is Greek 29:1).
/// </summary>
public static class TvtmsGreekFixes
{
    public const string SectionPrefix = "fix:";

    /// <summary>Greek verses to drop from <see cref="TvtmsGreekTable.GreekVerses"/> (count table artefacts), with the reason.</summary>
    public static readonly IReadOnlyDictionary<TvtmsRef, string> PhantomGreekVerses = new Dictionary<TvtmsRef, string>
    {
        [new TvtmsRef("Deu", "28", 69, 0)] = "the English count table gives Deu 28 69 verses; Heb 28:69 is Eng/Greek 29:1",
    };

    /// <summary>The (TVTMS book, chapter) pairs whose Greek mapping a fix changes.</summary>
    public static readonly IReadOnlySet<(string Book, string Chapter)> TouchedChapters = new HashSet<(string, string)>
    {
        ("Mal", "4"), ("Dan", "3"), ("Exo", "38"), ("Deu", "28"),
    };

    public static TvtmsGreekTable Apply(TvtmsGreekTable table)
    {
        var rows = new List<TvtmsGreekRow>(table.Rows.Count + 2);
        foreach (var row in table.Rows)
        {
            if (IsMalachiTypo(row))
            {
                continue;
            }

            rows.Add(FixMalachiOrder(row) ?? FixDaniel398(row) ?? row);
        }

        rows.Add(new TvtmsGreekRow($"{SectionPrefix} Dan.3:52-90 (S3Y 1:29-68, commented out in TVTMS)", "LongVerse/LVExtra", [], [],
            [Span("Dan", 3, 52, 90)], false));
        rows.Add(new TvtmsGreekRow($"{SectionPrefix} Exo.38:19 (= Eng/Heb 36:13, missing in the Greek column)", "SubdividedVerse",
            [Span("Exo", 36, 13, 13)], [Span("Exo", 36, 13, 13)], [Span("Exo", 38, 19, 19)], false));

        return new TvtmsGreekTable(rows, table.UnparsedCells);
    }

    private static bool IsMalachiTypo(TvtmsGreekRow row) =>
        row.Greek is [{ Start: { Book: "Mal", Chapter: "4", Verse: 1 }, End: { Verse: 35 } }];

    private static TvtmsGreekRow? FixMalachiOrder(TvtmsGreekRow row)
    {
        if (row.English is not [{ Start: { Book: "Mal", Chapter: "4" } start } span] || span.Start != span.End
            || row.Greek is not [{ Start.Chapter: "4" }])
        {
            return null;
        }

        int? greek = start.Verse switch { 4 => 6, 5 => 4, 6 => 5, _ => null };

        return greek is null
            ? null
            : row with { Section = $"{SectionPrefix} Mal.4:{greek} = Eng Mal.4:{start.Verse} (TVTMS rotates 4:4-6)", Greek = [Span("Mal", 4, greek.Value, greek.Value)] };
    }

    private static TvtmsGreekRow? FixDaniel398(TvtmsGreekRow row)
    {
        if (!row.GreekAbsent || row.English is not [{ Start: { Book: "Dan", Chapter: "4", Verse: 1 }, End: { Chapter: "4", Verse: 3 } }])
        {
            return null;
        }

        return row with { Section = $"{SectionPrefix} Dan.3:98-100 = Eng Dan.4:1-3 (TVTMS: Absent)", Greek = [Span("Dan", 3, 98, 100)], GreekAbsent = false };
    }

    private static TvtmsSpan Span(string book, int chapter, int from, int to) =>
        new(new TvtmsRef(book, chapter.ToString(System.Globalization.CultureInfo.InvariantCulture), from, 0),
            new TvtmsRef(book, chapter.ToString(System.Globalization.CultureInfo.InvariantCulture), to, 0));
}
