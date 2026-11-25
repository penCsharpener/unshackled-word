using System.Text.RegularExpressions;

namespace UnshackledWord.Application.Features.Lxx;

/// <summary>
/// A verse reference as TVTMS writes it: <c>Book.Chapter:Verse[.Sub]</c> (STEP/English counting for the English column, the tradition's own
/// counting for the others). <c>Chapter</c> is text because TVTMS uses letters for the Esther additions (<c>Est.A:1</c>). A Psalm title is
/// verse 0. <c>Sub</c> 0 is the verse itself; 1.. are the subverses of a LongVerse (<c>1Ki.2:35.1</c>).
/// </summary>
public readonly record struct TvtmsRef(string Book, string Chapter, int Verse, int Sub)
{
    public override string ToString() => $"{Book}.{Chapter}:{(Verse == 0 ? "Title" : Verse.ToString())}{(Sub > 0 ? $".{Sub}" : "")}";
}

/// <summary>An inclusive span of references. Within one chapter it is a verse range, or (same verse) a subverse range.</summary>
public readonly record struct TvtmsSpan(TvtmsRef Start, TvtmsRef End)
{
    /// <summary>True when the span runs over a chapter boundary (<c>Rev.12:18-13:1</c>); such spans are expanded to their two ends only.</summary>
    public bool CrossesChapter => Start.Chapter != End.Chapter;

    /// <summary>The references of the span in order (verse range, subverse range or, for a cross-chapter span, just start and end).</summary>
    public IEnumerable<TvtmsRef> Expand()
    {
        if (CrossesChapter)
        {
            yield return Start;
            yield return End;
            yield break;
        }

        if (Start.Verse == End.Verse)
        {
            for (var sub = Start.Sub; sub <= End.Sub; sub++)
            {
                yield return Start with { Sub = sub };
            }

            yield break;
        }

        for (var verse = Start.Verse; verse <= End.Verse; verse++)
        {
            yield return Start with { Verse = verse, Sub = 0 };
        }
    }
}

/// <summary>One alignment row of a condensed record, reduced to the English, Hebrew and Greek columns.</summary>
/// <param name="Section">The record header, e.g. <c>Psa.3:1-3:8</c>.</param>
/// <param name="Alignment">TVTMS alignment type (<c>OneToOne</c>, <c>SubdividedVerse</c>, <c>LongVerse/LVExtra</c> ...).</param>
/// <param name="English">English (NRSV/KJV) spans; empty for <c>NoVerse</c>/<c>Absent</c>.</param>
/// <param name="Hebrew">Hebrew (MT) spans. Records without a Hebrew column have the Hebrew numbering of the English column.</param>
/// <param name="Greek">Greek (LXX) spans; empty when <see cref="GreekAbsent"/>.</param>
/// <param name="GreekAbsent">The Greek cell is <c>Absent</c> or <c>NoVerse</c>: the Greek tradition has no verse for this row.</param>
public sealed record TvtmsGreekRow(
    string Section,
    string Alignment,
    IReadOnlyList<TvtmsSpan> English,
    IReadOnlyList<TvtmsSpan> Hebrew,
    IReadOnlyList<TvtmsSpan> Greek,
    bool GreekAbsent);

/// <summary>One Greek reference with its English and Hebrew equivalent where the row gives one.</summary>
/// <param name="Paired">The Greek and English columns of the row have the same number of references (so the equivalents are exact);
/// otherwise the equivalents are the first reference of the English/Hebrew column (a split or merged verse).</param>
public sealed record TvtmsGreekEntry(TvtmsRef Greek, TvtmsRef? English, TvtmsRef? Hebrew, string Alignment, bool Paired);

/// <summary>Greek verses of one chapter (Greek numbering): main verses (Title is 0) and the highest subverse per verse.</summary>
public sealed record TvtmsGreekChapter(SortedSet<int> Verses, SortedDictionary<int, int> MaxSubverse);

/// <summary>The Greek column of the TVTMS condensed table.</summary>
public sealed class TvtmsGreekTable
{
    public TvtmsGreekTable(IReadOnlyList<TvtmsGreekRow> rows, IReadOnlyList<string> unparsedCells)
    {
        Rows = rows;
        UnparsedCells = unparsedCells;
    }

    public IReadOnlyList<TvtmsGreekRow> Rows { get; }

    /// <summary>Cells the grammar could not read, as <c>Section: cell</c>.</summary>
    public IReadOnlyList<string> UnparsedCells { get; }

    /// <summary>Every non-absent Greek reference of the explicit rows with its English and Hebrew equivalent.</summary>
    public IEnumerable<TvtmsGreekEntry> Entries()
    {
        foreach (var row in Rows.Where(r => !r.GreekAbsent))
        {
            var greek = row.Greek.SelectMany(s => s.Expand()).ToList();
            var english = row.English.SelectMany(s => s.Expand()).ToList();
            var hebrew = row.Hebrew.SelectMany(s => s.Expand()).ToList();
            var paired = english.Count == greek.Count;

            for (var i = 0; i < greek.Count; i++)
            {
                yield return new TvtmsGreekEntry(greek[i], Pick(english, i, greek.Count), Pick(hebrew, i, greek.Count), row.Alignment, paired);
            }
        }
    }

    /// <summary>
    /// The verses of the Greek tradition per (TVTMS book code, chapter). The condensed table only lists places where the traditions
    /// differ, so a verse that no row mentions in its English column is identical in every tradition and joins the set under its English
    /// reference. <paramref name="englishCounts"/> returns the English verse count per chapter of a book (index 0 = chapter 1) or null.
    /// </summary>
    public IReadOnlyDictionary<(string Book, string Chapter), TvtmsGreekChapter> GreekVerses(Func<string, int[]?> englishCounts)
    {
        var chapters = new Dictionary<(string, string), TvtmsGreekChapter>();

        void Add(TvtmsRef r)
        {
            if (!chapters.TryGetValue((r.Book, r.Chapter), out var chapter))
            {
                chapter = new TvtmsGreekChapter([], []);
                chapters[(r.Book, r.Chapter)] = chapter;
            }

            if (r.Sub == 0)
            {
                chapter.Verses.Add(r.Verse);
            }
            else
            {
                chapter.Verses.Add(r.Verse);
                chapter.MaxSubverse[r.Verse] = Math.Max(chapter.MaxSubverse.GetValueOrDefault(r.Verse), r.Sub);
            }
        }

        foreach (var entry in Entries())
        {
            Add(entry.Greek);
        }

        var covered = new HashSet<(string, string, int)>();
        foreach (var span in Rows.SelectMany(r => r.English))
        {
            foreach (var r in ExpandWithCounts(span, englishCounts))
            {
                covered.Add((r.Book, r.Chapter, r.Verse));
            }
        }

        foreach (var book in LxxBookCodes.Tvtms)
        {
            var counts = englishCounts(book);
            for (var chapter = 1; counts is not null && chapter <= counts.Length; chapter++)
            {
                for (var verse = 1; verse <= counts[chapter - 1]; verse++)
                {
                    if (!covered.Contains((book, chapter.ToString(), verse)))
                    {
                        Add(new TvtmsRef(book, chapter.ToString(), verse, 0));
                    }
                }
            }
        }

        return chapters;
    }

    private static IEnumerable<TvtmsRef> ExpandWithCounts(TvtmsSpan span, Func<string, int[]?> counts)
    {
        if (!span.CrossesChapter || !int.TryParse(span.Start.Chapter, out var first) || !int.TryParse(span.End.Chapter, out var last)
            || counts(span.Start.Book) is not { } chapterCounts)
        {
            return span.Expand();
        }

        var result = new List<TvtmsRef>();
        for (var chapter = first; chapter <= last && chapter <= chapterCounts.Length; chapter++)
        {
            var from = chapter == first ? span.Start.Verse : 1;
            var to = chapter == last ? span.End.Verse : chapterCounts[chapter - 1];
            for (var verse = from; verse <= to; verse++)
            {
                result.Add(span.Start with { Chapter = chapter.ToString(), Verse = verse, Sub = 0 });
            }
        }

        return result;
    }

    private static TvtmsRef? Pick(List<TvtmsRef> list, int index, int count) =>
        list.Count == count ? list[index] : list.Count > 0 ? list[0] : null;
}

/// <summary>
/// Parser of the condensed table of TVTMS (<c>TVTMS - Translators Versification Traditions ... - STEPBible.org CC BY.txt</c>, CC BY 4.0,
/// downloaded by the STEP downloader; roadmap 0028.1). The table sits between <c>#DataStart(Condensed)</c> and <c>#DataEnd(Condensed)</c>.
/// <list type="bullet">
/// <item>A record starts with a <c>$Section</c> line. Its columns name the traditions (<c>English KJV</c>, <c>Hebrew</c>, <c>Latin</c>,
/// <c>Greek</c>, <c>Greek*</c> when the order differs, <c>Greek2</c> ...). The Psalms put the names in a <c>BIBLES</c> row instead. Column
/// order differs per record, so the columns are read per record. Only the columns named exactly <c>Greek</c>/<c>Greek*</c> (LXX), the English
/// one and <c>Hebrew</c> are used.</item>
/// <item>Alignment rows (<c>OneToOne</c>, <c>SubdividedVerse</c>, <c>LongVerse/LVExtra</c> ...) give one cell per column: a reference, a
/// range (<c>Psa.10:1-18</c>), a list (<c>Est.11:2-12; 12:1-6</c>, <c>Exo.37:1, 2.1</c>: an item without chapter or book continues the previous one), a subverse (<c>1Ki.2:35.1</c>, range <c>Exo.28:22.1-6</c>, <c>.0</c> is
/// the verse itself), <c>Absent</c> / <c>Absent [=ref]</c> (the tradition has no verse here; the bracket says where its text is),
/// <c>NoVerse</c>, or a Psalm <c>Title</c>. Text in <c>[...]</c> is a note and ignored. Rows whose first cell starts with <c>TEST</c>, <c>#</c>
/// or the test labels (<c>Hebrew + Greek</c>) are skipped.</item>
/// <item>Only differences are listed. A verse that no row mentions is identical in all traditions (see <see cref="TvtmsGreekTable.GreekVerses"/>).</item>
/// </list>
/// </summary>
public static partial class TvtmsGreekParser
{
    private const string StartMarker = "#DataStart(Condensed)";
    private const string EndMarker = "#DataEnd(Condensed)";

    [GeneratedRegex(@"^(?:(?<b>[1-3A-Za-z][A-Za-z0-9]{2})\.)?(?:(?<c>[0-9]+|[A-Z]):)?(?<v>[0-9]+|Title)(?:\.(?<s>[0-9]+))?(?:-(?:(?<c2>[0-9]+|[A-Z]):)?(?<v2>[0-9]+)(?:\.(?<s2>[0-9]+))?)?$")]
    private static partial Regex ItemPattern();

    [GeneratedRegex(@"\[[^\]]*\]")]
    private static partial Regex NotePattern();

    public static TvtmsGreekTable Parse(IEnumerable<string> lines)
    {
        var rows = new List<TvtmsGreekRow>();
        var unparsed = new List<string>();
        var inTable = false;
        string section = "";
        string[]? columns = null;

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd('\r').TrimStart('﻿');
            if (!inTable)
            {
                inTable = line.StartsWith(StartMarker, StringComparison.Ordinal);
                continue;
            }

            if (line.StartsWith(EndMarker, StringComparison.Ordinal))
            {
                break;
            }

            var cells = line.Split('\t').Select(c => c.Trim()).ToArray();
            if (line.StartsWith('$'))
            {
                section = cells[0].TrimStart('$').Trim();
                columns = cells.Length > 1 && cells[1].Length > 0 ? cells : null;
                continue;
            }

            var first = cells[0];
            if (first == "BIBLES")
            {
                columns = cells;
                continue;
            }

            if (columns is null || first.Length == 0 || first.StartsWith("TEST", StringComparison.Ordinal) || first[0] is '#' or '\'' or ',' || first.Contains(" + "))
            {
                continue;
            }

            var row = ReadRow(section, first, cells, columns, unparsed);
            if (row is not null)
            {
                rows.Add(row);
            }
        }

        return new TvtmsGreekTable(rows, unparsed);
    }

    private static TvtmsGreekRow? ReadRow(string section, string alignment, string[] cells, string[] columns, List<string> unparsed)
    {
        var english = Array.FindIndex(columns, 1, c => c.StartsWith("English", StringComparison.Ordinal));
        var hebrew = Array.FindIndex(columns, 1, c => c.TrimEnd('*') == "Hebrew");
        var greek = Array.FindIndex(columns, 1, c => c.TrimEnd('*') == "Greek");
        if (greek < 0 || english < 0 || Cell(cells, greek).Length == 0)
        {
            return null;
        }

        var greekCell = ParseCell(Cell(cells, greek), out var greekAbsent, out var greekOk);
        var englishCell = ParseCell(Cell(cells, english), out _, out var englishOk);
        var hebrewCell = hebrew >= 0 && Cell(cells, hebrew).Length > 0 ? ParseCell(Cell(cells, hebrew), out _, out _) : englishCell;

        foreach (var (ok, index) in new[] { (greekOk, greek), (englishOk, english) })
        {
            if (!ok)
            {
                unparsed.Add($"{section}: {Cell(cells, index)}");
            }
        }

        return greekOk ? new TvtmsGreekRow(section, alignment, englishCell, hebrewCell, greekCell, greekAbsent) : null;
    }

    private static string Cell(string[] cells, int index) => index < cells.Length ? cells[index] : "";

    /// <summary>Parses one cell. <c>absent</c> is true for <c>Absent</c> and <c>NoVerse</c>; <c>ok</c> is false when an item is unreadable.</summary>
    internal static List<TvtmsSpan> ParseCell(string text, out bool absent, out bool ok)
    {
        var spans = new List<TvtmsSpan>();
        var cleaned = NotePattern().Replace(text, "").Trim();
        absent = cleaned.StartsWith("Absent", StringComparison.Ordinal) || cleaned == "NoVerse";
        ok = true;
        if (absent || cleaned.Length == 0)
        {
            return spans;
        }

        string? book = null;
        string? chapter = null;
        foreach (var item in cleaned.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var match = ItemPattern().Match(item);
            if (!match.Success)
            {
                ok = false;
                continue;
            }

            book = match.Groups["b"].Success ? match.Groups["b"].Value : book;
            chapter = match.Groups["c"].Success ? match.Groups["c"].Value : chapter;
            if (book is null || chapter is null)
            {
                ok = false;
                continue;
            }

            spans.Add(ToSpan(book, chapter, match));
        }

        return spans;
    }

    private static TvtmsSpan ToSpan(string book, string chapter, Match match)
    {
        var verse = VerseNumber(match.Groups["v"].Value);
        var sub = match.Groups["s"].Success ? int.Parse(match.Groups["s"].Value) : 0;
        var start = new TvtmsRef(book, chapter, verse, sub);
        if (!match.Groups["v2"].Success)
        {
            return new TvtmsSpan(start, start);
        }

        var end2 = int.Parse(match.Groups["v2"].Value);
        if (match.Groups["c2"].Success)
        {
            var sub2 = match.Groups["s2"].Success ? int.Parse(match.Groups["s2"].Value) : 0;

            return new TvtmsSpan(start, new TvtmsRef(book, match.Groups["c2"].Value, end2, sub2));
        }

        // 'Exo.28:22.1-6' is a subverse range of verse 22; 'Psa.10:1-18' a verse range.
        return match.Groups["s"].Success && !match.Groups["s2"].Success
            ? new TvtmsSpan(start, start with { Sub = end2 })
            : new TvtmsSpan(start, new TvtmsRef(book, chapter, end2, match.Groups["s2"].Success ? int.Parse(match.Groups["s2"].Value) : 0));
    }

    private static int VerseNumber(string text) => text == "Title" ? 0 : int.Parse(text);
}
