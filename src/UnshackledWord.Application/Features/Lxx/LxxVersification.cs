using System.Globalization;
using System.Text.RegularExpressions;
using UnshackledWord.Domain.Models.BibleStructure.References;

namespace UnshackledWord.Application.Features.Lxx;

/// <summary>How a Rahlfs verse relates to the Hebrew/English text (roadmap 0028, <c>LxxVerses.MapKind</c>).</summary>
public static class LxxMapKinds
{
    /// <summary>One Greek verse, one Hebrew verse.</summary>
    public const string One = "one";

    /// <summary>The Greek verse covers several Hebrew verses; <c>HebRefId</c> is the first.</summary>
    public const string Merged = "merged";

    /// <summary>Several Greek verses share one Hebrew verse; <c>HebRefId</c> is that verse.</summary>
    public const string Split = "split";

    /// <summary>Greek text without Hebrew counterpart (Esther A-F, 3 Kgdms 2:35a-o, Dan 3:24-90, Swete's a/b/c pluses); shown after <c>AfterHebRefId</c>.</summary>
    public const string Addition = "addition";

    /// <summary>No mapping: an empty Swete verse, or Swete text the exception list marks as having no Rahlfs/TVTMS counterpart.</summary>
    public const string Absent = "absent";
}

/// <summary>Where the Rahlfs reference of a mapped verse comes from.</summary>
public static class LxxMapOrigins
{
    /// <summary>Swete's reference is the Rahlfs reference.</summary>
    public const string Identity = "identity";

    /// <summary>A Swete marker segment <c>[N]</c> becomes subverse N (by order) of its verse.</summary>
    public const string Marker = "marker";

    /// <summary>A row of <c>swete-to-rahlfs.csv</c>.</summary>
    public const string Exception = "exception";
}

/// <summary>
/// One verse (or split part) of Swete's text with its Rahlfs reference and its STEP/Elberfelder equivalents (roadmap 0028.2).
/// </summary>
/// <param name="Order">Running number in Swete's order (the reading order; Esther addition A precedes Est 1:1 although 1:1.1 sorts after 1:1).</param>
/// <param name="SweteRef">Swete's reference of the unit, e.g. <c>Jer.32:1</c>, <c>Est.1:1[18]</c>, <c>Gen.3:1#1-15</c>; joined units as <c>A + B</c>.</param>
/// <param name="Words">The words with their position in Swete's verse (headings and marker tokens included; see <see cref="LxxVersification.IsMarkerToken"/>).</param>
/// <param name="RahlfsRefId"><c>book·10⁸ + chapter·10⁵ + verse·10² + subverse</c>.</param>
/// <param name="LxxRefId">English (STEP) counting as <c>book·10⁶ + chapter·10³ + verse</c>; a Psalm title is verse 0 as in STEP's Hebrew words.</param>
/// <param name="HebRefId">Hebrew counting (Elberfelder/<c>Elb1871Verses</c>), same encoding.</param>
/// <param name="AfterHebRefId">For additions and absent text: the Hebrew verse it is shown after.</param>
/// <param name="TvtmsFix">The TVTMS row used is a <see cref="TvtmsGreekFixes"/> correction.</param>
/// <param name="ExceptionLine">Line in the exception list when <see cref="Origin"/> is <see cref="LxxMapOrigins.Exception"/>.</param>
public sealed record LxxVerseMapping(
    int Order,
    string SweteRef,
    string SweteBook,
    int SweteChapter,
    int SweteVerse,
    IReadOnlyList<SweteWord> Words,
    int BibleBookId,
    string RahlfsBook,
    string TextForm,
    int Chapter,
    int Verse,
    int Subverse,
    long RahlfsRefId,
    int? LxxRefId,
    int? HebRefId,
    int? AfterHebRefId,
    string MapKind,
    string Origin,
    bool TvtmsFix,
    int? ExceptionLine,
    string? Note)
{
    /// <summary>Swete keeps the number of a verse its Greek lacks, as one blank word (1Ki 14:1-20, Jer 46:4-13 ...).</summary>
    public bool IsEmpty => LxxVersification.IsBlank(Words);

    public string RahlfsRef => LxxVersification.Format(RahlfsBook, Chapter, Verse, Subverse);
}

/// <summary>A Rahlfs/TVTMS Greek verse that has no text in Swete (gap row of the exception list).</summary>
public sealed record LxxGapRef(int BibleBookId, string TextForm, TvtmsRef Rahlfs, int Line, string Note);

public sealed record LxxVersificationResult(
    IReadOnlyList<LxxVerseMapping> Verses,
    IReadOnlyList<LxxGapRef> Gaps,
    IReadOnlyList<string> Errors,
    TvtmsGreekTable Table);

/// <summary>
/// Swete → Rahlfs → STEP (<c>LxxRefId</c>) / Elberfelder (<c>HebRefId</c>) for the 39 protocanonical books (roadmap 0028.2, rules in
/// <c>docs/roadmaps/0028-lxx-rahlfs/03-versification.md</c>). Pure code: takes the parsed Swete verses, the exception list and the TVTMS Greek
/// table.
/// <list type="number">
/// <item><b>Units.</b> Each Swete verse is one unit. A verse with marker tokens (<c>[1]</c>, <c>⸂[1]</c>: Swete's printed a, b, c ...) is cut at
/// the markers: the words before the first marker keep the verse, the k-th marker segment becomes subverse k. Word-range rows
/// (<c>#A-B</c>) cut a verse at word positions instead; a whole-verse row keeps it in one piece.</item>
/// <item><b>Rahlfs reference.</b> Exception rows assign their units pairwise or join them into one verse; every other unit keeps Swete's
/// reference (TVTMS book code, <c>TextForm</c> <c>og</c> for <c>Dan</c>, <c>th</c> for <c>Dat</c>).</item>
/// <item><b>TVTMS.</b> The Greek column (with <see cref="TvtmsGreekFixes"/>) gives English and Hebrew. A Greek verse that no row names and
/// whose English number no row covers is identical in every tradition. A subverse that TVTMS lacks, or whose only TVTMS row points back
/// to its own main verse, is an addition; so is a verse without Hebrew.</item>
/// </list>
/// </summary>
public static partial class LxxVersification
{
    [GeneratedRegex(@"^\W*\[([0-9]+)\]\W*$")]
    private static partial Regex MarkerPattern();

    /// <summary>True for Swete's subverse marker tokens (<c>[3]</c>, <c>⸂[1]</c>). 0028.3 keeps them out of the word stream.</summary>
    public static bool IsMarkerToken(string text) => MarkerPattern().IsMatch(text);

    /// <summary>True when no word has text: Swete's placeholder for a verse number without Greek text (one blank word).</summary>
    public static bool IsBlank(IEnumerable<SweteWord> words) => words.All(w => string.IsNullOrWhiteSpace(w.Text));

    /// <summary><c>book·10⁸ + chapter·10⁵ + verse·10² + subverse</c> (subverses go up to 30 in Est 4:17, so a single digit is not enough).</summary>
    public static long RahlfsRefId(int bookId, int chapter, int verse, int subverse) =>
        bookId * 100_000_000L + chapter * 100_000L + verse * 100L + subverse;

    /// <summary>The STEP/Elberfelder <c>RefId</c> (<c>BibleReference.RefId</c>): <c>book·10⁶ + chapter·10³ + verse</c>.</summary>
    public static int RefId(int bookId, int chapter, int verse) => bookId * 1_000_000 + chapter * 1000 + verse;

    public static string Format(string book, int chapter, int verse, int subverse) =>
        $"{book}.{chapter}:{verse}{(subverse > 0 ? "." + subverse.ToString(CultureInfo.InvariantCulture) : "")}";

    /// <summary>Formats a <see cref="RefId"/> as <c>Book.C:V</c> with the TVTMS book code.</summary>
    public static string FormatRefId(int? refId)
    {
        if (refId is not { } id)
        {
            return "-";
        }

        var book = id / 1_000_000;
        var code = book is >= 1 and <= 39 ? LxxBookCodes.Tvtms[book - 1] : book.ToString(CultureInfo.InvariantCulture);
        var verse = id % 1000;

        return $"{code}.{id / 1000 % 1000}:{(verse == 0 ? "Title" : verse.ToString(CultureInfo.InvariantCulture))}";
    }

    public static LxxVersificationResult Map(IReadOnlyList<SweteVerse> verses, IReadOnlyList<SweteRahlfsException> exceptions, TvtmsGreekTable table)
    {
        var errors = new List<string>();
        var fixedTable = TvtmsGreekFixes.Apply(table);
        var index = new TvtmsIndex(fixedTable);

        var units = BuildUnits(verses, exceptions, errors);
        AssignExceptions(units, exceptions, errors);
        var groups = Group(units, errors);
        var mapped = Resolve(groups, index);

        var gaps = new List<LxxGapRef>();
        foreach (var row in exceptions.Where(e => e.Swete is null))
        {
            foreach (var r in row.Rahlfs)
            {
                gaps.Add(new LxxGapRef(LxxBookCodes.BookIdFromTvtms(r.Book)!.Value, row.TextForm, r, row.Line, row.Note));
            }
        }

        return new LxxVersificationResult(mapped, gaps, errors, fixedTable);
    }

    /// <summary>
    /// The Greek verses per (TVTMS book, chapter) of the fixed table, without <see cref="TvtmsGreekFixes.PhantomGreekVerses"/>.
    /// </summary>
    public static IReadOnlyDictionary<(string Book, string Chapter), TvtmsGreekChapter> GreekVerses(TvtmsGreekTable fixedTable)
    {
        var greek = fixedTable.GreekVerses(code => LxxBookCodes.BookIdFromTvtms(code) is { } id ? VerseTables.EnglishCounts(id) : null);
        foreach (var phantom in TvtmsGreekFixes.PhantomGreekVerses.Keys)
        {
            if (greek.TryGetValue((phantom.Book, phantom.Chapter), out var chapter))
            {
                chapter.Verses.Remove(phantom.Verse);
            }
        }

        return greek;
    }

    // ---- units ---------------------------------------------------------------------------------------------------------------------

    private sealed class Unit
    {
        public required int Order { get; init; }
        public required SweteVerse Verse { get; init; }
        public required List<SweteWord> Words { get; init; }
        public required string SweteRef { get; init; }
        public int? Marker { get; init; }
        public int Ordinal { get; init; }
        public int? WordFrom { get; init; }
        public string TextForm { get; set; } = "";
        public TvtmsRef? Target { get; set; }
        public bool AbsentByRow { get; set; }
        public string Origin { get; set; } = LxxMapOrigins.Identity;
        public int? Line { get; set; }
        public string? Note { get; set; }
    }

    private static List<Unit> BuildUnits(IReadOnlyList<SweteVerse> verses, IReadOnlyList<SweteRahlfsException> exceptions, List<string> errors)
    {
        var rowsByVerse = new Dictionary<(string, int, int), List<SweteRahlfsException>>();
        foreach (var row in exceptions.Where(e => e.Swete is not null))
        {
            for (var v = row.Swete!.FromVerse; v <= row.Swete.ToVerse; v++)
            {
                var key = (row.Swete.Book, row.Swete.Chapter, v);
                if (!rowsByVerse.TryGetValue(key, out var list))
                {
                    rowsByVerse[key] = list = [];
                }

                list.Add(row);
            }
        }

        var units = new List<Unit>();
        foreach (var verse in verses)
        {
            var tvtmsBook = LxxBookCodes.TvtmsFromSwete(verse.BookCode);
            if (tvtmsBook is null)
            {
                continue;
            }

            var textForm = verse.BookCode == "Dat" ? "th" : verse.BookCode == "Dan" ? "og" : "";
            var rows = rowsByVerse.GetValueOrDefault((verse.BookCode, verse.Chapter, verse.Verse)) ?? [];
            var wordRows = rows.Where(r => r.Swete!.IsWordRange).OrderBy(r => r.Swete!.FromWord).ToList();

            void Add(List<SweteWord> words, string reference, int? marker = null, int ordinal = 0, int? wordFrom = null) =>
                units.Add(new Unit { Order = units.Count, Verse = verse, Words = words, SweteRef = reference, Marker = marker, Ordinal = ordinal, WordFrom = wordFrom, TextForm = textForm });

            if (wordRows.Count > 0)
            {
                var expected = 1;
                foreach (var row in wordRows)
                {
                    var from = row.Swete!.FromWord!.Value;
                    var to = row.Swete.ToWord ?? verse.Words.Count;
                    if (from != expected || to < from || to > verse.Words.Count)
                    {
                        errors.Add($"line {row.Line}: word range {row.Swete} does not continue at word {expected} of {verse.Words.Count}.");
                    }

                    Add(verse.Words.Where(w => w.Position >= from && w.Position <= to).ToList(), row.Swete.ToString(), wordFrom: from);
                    expected = to + 1;
                }

                if (expected != verse.Words.Count + 1)
                {
                    errors.Add($"{verse.Reference}: word ranges cover words 1-{expected - 1} of {verse.Words.Count}.");
                }

                continue;
            }

            var markers = verse.Words.Where(w => IsMarkerToken(w.Text)).ToList();
            if (markers.Count == 0 || rows.Any(r => r.Swete!.IsWholeVerse))
            {
                Add(verse.Words.ToList(), verse.Reference);
                continue;
            }

            var before = verse.Words.Where(w => w.Position < markers[0].Position).ToList();
            if (before.Count > 0)
            {
                Add(before, verse.Reference);
            }

            for (var k = 0; k < markers.Count; k++)
            {
                var start = markers[k].Position;
                var end = k + 1 < markers.Count ? markers[k + 1].Position : int.MaxValue;
                var number = int.Parse(MarkerPattern().Match(markers[k].Text).Groups[1].Value, CultureInfo.InvariantCulture);
                Add(verse.Words.Where(w => w.Position >= start && w.Position < end).ToList(), $"{verse.Reference}[{number}]", number, k + 1);
            }
        }

        return units;
    }

    private static void AssignExceptions(List<Unit> units, IReadOnlyList<SweteRahlfsException> exceptions, List<string> errors)
    {
        var byVerse = units.GroupBy(u => (u.Verse.BookCode, u.Verse.Chapter, u.Verse.Verse)).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var row in exceptions.Where(e => e.Swete is not null))
        {
            var selector = row.Swete!;
            var selected = new List<Unit>();
            for (var v = selector.FromVerse; v <= selector.ToVerse; v++)
            {
                if (!byVerse.TryGetValue((selector.Book, selector.Chapter, v), out var verseUnits))
                {
                    errors.Add($"line {row.Line}: Swete has no protocanonical verse {selector.Book}.{selector.Chapter}:{v}.");
                    continue;
                }

                selected.AddRange(selector switch
                {
                    { IsMarkerRange: true } => verseUnits.Where(u => u.Marker >= selector.FromMarker && u.Marker <= selector.ToMarker),
                    { IsWordRange: true } => verseUnits.Where(u => u.WordFrom == selector.FromWord),
                    _ => verseUnits,
                });
            }

            if (selected.Count == 0)
            {
                errors.Add($"line {row.Line}: {selector} selects no Swete text.");
                continue;
            }

            if (selected.FirstOrDefault(u => u.Line is not null) is { } taken)
            {
                errors.Add($"line {row.Line}: {taken.SweteRef} is already assigned by line {taken.Line}.");
                continue;
            }

            if (row.Rahlfs.Count != 0 && row.Rahlfs.Count != 1 && row.Rahlfs.Count != selected.Count)
            {
                errors.Add($"line {row.Line}: {selected.Count} Swete units but {row.Rahlfs.Count} Rahlfs references (same count, or one to join).");
                continue;
            }

            for (var i = 0; i < selected.Count; i++)
            {
                var unit = selected[i];
                unit.Line = row.Line;
                unit.Note = row.Note;
                unit.Origin = LxxMapOrigins.Exception;
                unit.AbsentByRow = row.Rahlfs.Count == 0;
                unit.Target = row.Rahlfs.Count switch
                {
                    0 => null,
                    1 => row.Rahlfs[0],
                    _ => row.Rahlfs[i],
                };
            }
        }

        foreach (var unit in units.Where(u => u.Line is null))
        {
            unit.Target = new TvtmsRef(LxxBookCodes.TvtmsFromSwete(unit.Verse.BookCode)!, unit.Verse.Chapter.ToString(CultureInfo.InvariantCulture),
                unit.Verse.Verse, unit.Ordinal);
            unit.Origin = unit.Marker is null ? LxxMapOrigins.Identity : LxxMapOrigins.Marker;
        }
    }

    /// <summary>Joins units with the same Rahlfs verse; only exception rows may join (a default reference given twice is an error).</summary>
    private static List<List<Unit>> Group(List<Unit> units, List<string> errors)
    {
        var groups = new List<List<Unit>>();
        var byTarget = new Dictionary<(string, TvtmsRef), List<Unit>>();
        foreach (var unit in units)
        {
            if (unit.Target is not { } target)
            {
                groups.Add([unit]);
                continue;
            }

            if (byTarget.TryGetValue((unit.TextForm, target), out var group))
            {
                if (group[0].Line is null || unit.Line is null)
                {
                    errors.Add($"Rahlfs {target} ({unit.TextForm}) is given twice: {group[0].SweteRef} and {unit.SweteRef}.");
                }

                group.Add(unit);
                continue;
            }

            byTarget[(unit.TextForm, target)] = group = [unit];
            groups.Add(group);
        }

        return groups;
    }

    // ---- TVTMS resolution ----------------------------------------------------------------------------------------------------------

    private static List<LxxVerseMapping> Resolve(List<List<Unit>> groups, TvtmsIndex index)
    {
        var result = new List<LxxVerseMapping>(groups.Count);
        var lastHeb = new Dictionary<(string, string), int>();
        foreach (var group in groups)
        {
            var first = group[0];
            var words = group.SelectMany(u => u.Words).ToList();
            var sweteRef = string.Join(" + ", group.Select(u => u.SweteRef));
            var book = first.Target?.Book ?? LxxBookCodes.TvtmsFromSwete(first.Verse.BookCode)!;
            var bookId = LxxBookCodes.BookIdFromTvtms(book)!.Value;
            var key = (book, first.TextForm);
            var after = lastHeb.TryGetValue(key, out var previous) ? previous : (int?)null;

            var (chapter, verse, sub) = first.Target is { } t ? (int.Parse(t.Chapter, CultureInfo.InvariantCulture), t.Verse, t.Sub) : (0, 0, 0);
            int? lxx = null, heb = null, afterHeb = null;
            var fix = false;
            string kind;
            if (first.Target is null)
            {
                kind = LxxMapKinds.Absent;
                afterHeb = IsBlank(words) ? null : after;
            }
            else if (IsBlank(words))
            {
                kind = LxxMapKinds.Absent;
            }
            else
            {
                var resolved = index.Resolve(first.Target.Value);
                (lxx, heb, kind, fix) = (resolved.LxxRefId, resolved.HebRefId, resolved.Kind, resolved.Fix);
                if (kind == LxxMapKinds.Addition)
                {
                    afterHeb = sub > 0 && index.Resolve(first.Target.Value with { Sub = 0 }) is { Kind: not LxxMapKinds.Addition and not LxxMapKinds.Absent, HebRefId: { } parent }
                        ? parent
                        : after;
                }
                else if (kind == LxxMapKinds.Absent)
                {
                    afterHeb = after;
                }
            }

            if (heb is { } h)
            {
                lastHeb[key] = h;
            }

            result.Add(new LxxVerseMapping(result.Count, sweteRef, first.Verse.BookCode, first.Verse.Chapter, first.Verse.Verse, words, bookId, book,
                first.TextForm, chapter, verse, sub, first.Target is null ? 0 : RahlfsRefId(bookId, chapter, verse, sub), lxx, heb, afterHeb, kind,
                first.Origin, fix, first.Line, first.Note));
        }

        return result;
    }

    private sealed record Candidate(TvtmsRef? English, TvtmsRef? Hebrew, string Kind, bool Fix);

    /// <summary>The result of looking a Greek reference up in TVTMS. <c>Found</c> is false when TVTMS does not know the reference.</summary>
    public sealed record TvtmsResolution(int? LxxRefId, int? HebRefId, string Kind, bool Fix, bool Found, TvtmsRef? English, TvtmsRef? Hebrew);

    /// <summary>Greek reference → English/Hebrew candidates of the (fixed) TVTMS table.</summary>
    public sealed class TvtmsIndex
    {
        private readonly Dictionary<TvtmsRef, List<Candidate>> _candidates = new();
        private readonly HashSet<(string, string, int)> _englishCovered = new();

        public TvtmsIndex(TvtmsGreekTable fixedTable)
        {
            foreach (var row in fixedTable.Rows)
            {
                foreach (var span in row.English)
                {
                    foreach (var r in ExpandWithCounts(span))
                    {
                        _englishCovered.Add((r.Book, r.Chapter, r.Verse));
                    }
                }

                if (row.GreekAbsent)
                {
                    continue;
                }

                var greek = row.Greek.SelectMany(s => s.Expand()).ToList();
                var english = row.English.SelectMany(s => s.Expand()).ToList();
                var hebrew = row.Hebrew.SelectMany(s => s.Expand()).ToList();
                var kind = english.Count == 0 || english.Count == greek.Count ? LxxMapKinds.One
                    : greek.Count > english.Count ? LxxMapKinds.Split : LxxMapKinds.Merged;
                // A LongVerse (1Ki.2:35.0-14): the main verse is the Hebrew verse itself, only its subverses share it.
                var longVerse = greek.Count > 1 && greek.All(g => g with { Sub = 0 } == greek[0] with { Sub = 0 });
                var fix = row.Section.StartsWith(TvtmsGreekFixes.SectionPrefix, StringComparison.Ordinal);
                for (var i = 0; i < greek.Count; i++)
                {
                    var e = english.Count == greek.Count ? english[i] : english.Count > 0 ? english[0] : (TvtmsRef?)null;
                    var h = hebrew.Count == greek.Count ? hebrew[i] : hebrew.Count > 0 ? hebrew[0] : (TvtmsRef?)null;
                    if (!_candidates.TryGetValue(greek[i], out var list))
                    {
                        _candidates[greek[i]] = list = [];
                    }

                    list.Add(new Candidate(e, h, longVerse && greek[i].Sub == 0 ? LxxMapKinds.One : kind, fix));
                }
            }
        }

        /// <summary>True when the TVTMS Greek column names the reference, or the reference is the same in every tradition.</summary>
        public bool Knows(TvtmsRef greek) => Resolve(greek).Found;

        public TvtmsResolution Resolve(TvtmsRef greek)
        {
            if (!_candidates.TryGetValue(greek, out var candidates))
            {
                if (greek.Sub > 0)
                {
                    return new TvtmsResolution(null, null, LxxMapKinds.Addition, false, false, null, null);
                }

                if (_englishCovered.Contains((greek.Book, greek.Chapter, greek.Verse)))
                {
                    return new TvtmsResolution(null, null, LxxMapKinds.Absent, false, false, null, null);
                }

                var id = Id(greek);

                return new TvtmsResolution(id, id, LxxMapKinds.One, false, true, greek, greek);
            }

            TvtmsRef? parentEnglish = null;
            if (greek.Sub > 0)
            {
                var parent = Resolve(greek with { Sub = 0 });
                parentEnglish = parent.English is { } p ? p with { Sub = 0 } : null;
            }

            var chosen = candidates.FirstOrDefault(c => greek.Sub > 0 && c.English is { } e && e with { Sub = 0 } != parentEnglish && c.Kind == LxxMapKinds.One)
                ?? candidates.FirstOrDefault(c => greek.Sub > 0 && c.English is { } e && e with { Sub = 0 } != parentEnglish)
                ?? candidates.FirstOrDefault(c => c.Kind == LxxMapKinds.One)
                ?? candidates[0];

            var isAddition = chosen.Hebrew is null
                || (greek.Sub > 0 && chosen.English is { } english && english with { Sub = 0 } == parentEnglish && chosen.Kind != LxxMapKinds.One);
            if (isAddition)
            {
                return new TvtmsResolution(null, null, LxxMapKinds.Addition, chosen.Fix, true, chosen.English, chosen.Hebrew);
            }

            return new TvtmsResolution(chosen.English is { } en ? Id(en) : null, Id(chosen.Hebrew!.Value), chosen.Kind, chosen.Fix, true, chosen.English, chosen.Hebrew);
        }

        private static int? Id(TvtmsRef r) =>
            LxxBookCodes.BookIdFromTvtms(r.Book) is { } book && int.TryParse(r.Chapter, NumberStyles.None, CultureInfo.InvariantCulture, out var chapter)
                ? RefId(book, chapter, r.Verse)
                : null;

        private static IEnumerable<TvtmsRef> ExpandWithCounts(TvtmsSpan span)
        {
            if (!span.CrossesChapter || !int.TryParse(span.Start.Chapter, out var first) || !int.TryParse(span.End.Chapter, out var last)
                || LxxBookCodes.BookIdFromTvtms(span.Start.Book) is not { } book || VerseTables.EnglishCounts(book) is not { } counts)
            {
                return span.Expand();
            }

            var result = new List<TvtmsRef>();
            for (var chapter = first; chapter <= last && chapter <= counts.Length; chapter++)
            {
                var from = chapter == first ? span.Start.Verse : 1;
                var to = chapter == last ? span.End.Verse : counts[chapter - 1];
                for (var verse = from; verse <= to; verse++)
                {
                    result.Add(span.Start with { Chapter = chapter.ToString(CultureInfo.InvariantCulture), Verse = verse, Sub = 0 });
                }
            }

            return result;
        }
    }

    /// <summary>Builds the TVTMS index of a table (applies <see cref="TvtmsGreekFixes"/> first).</summary>
    public static TvtmsIndex BuildIndex(TvtmsGreekTable table) => new(TvtmsGreekFixes.Apply(table));
}
